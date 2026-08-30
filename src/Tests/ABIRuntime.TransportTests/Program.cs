using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using ABI.Models;
using ABIRuntime;
using ABIRuntime.Abstractions;
using ABIRuntime.Runtime;
using Haiyu.ABI.Services;
using MemoryPack;

namespace ABIRuntime.TransportTests;

internal static class Program
{
    private static readonly List<string> Results = [];
    private static int _lastJsonMonitorSize;
    private static int _lastMemoryPackMonitorSize;

    private static async Task<int> Main(string[] args)
    {
        ABIMemoryPack.EnsureFormatters();
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("MonitorRecord JSON/MemoryPack AOT 序列化", TestMonitorSerializationAsync),
            ("64 MiB 分帧与双向完整性", TestLargeMessagesAsync),
            ("并发独立请求管道", TestConcurrentPipesAsync),
            ("请求 ID 关联取消", TestCancellationMessageAsync),
            ("ComputerMonitorService 实际采样", TestDirectMonitorAsync),
            ("ComputerMonitorService 管道进度链", TestMonitorServicePipeAsync),
            ("控制管道与独立请求管道完整调用", TestElevatedHostRoutingAsync),
        };

        int failures = 0;
        foreach ((string name, Func<Task> run) in tests)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await run();
                Results.Add($"PASS  {name}  {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception exception)
            {
                failures++;
                Results.Add($"FAIL  {name}\n{exception}");
            }
        }

        if (args.Length > 0)
        {
            try
            {
                await TestPublishedNativeDllAsync(Path.GetFullPath(args[0]));
                Results.Add("PASS  已部署 NativeAOT DLL 实际采样");
            }
            catch (Exception exception)
            {
                failures++;
                Results.Add($"FAIL  已部署 NativeAOT DLL 实际采样\n{exception}");
            }
        }

        foreach (string result in Results)
            Console.WriteLine(result);
        if (_lastJsonMonitorSize > 0 && _lastMemoryPackMonitorSize > 0)
        {
            double reduction = 1d - (double)_lastMemoryPackMonitorSize / _lastJsonMonitorSize;
            Console.WriteLine($"MONITOR PAYLOAD: JSON={_lastJsonMonitorSize} bytes, " +
                              $"MemoryPack={_lastMemoryPackMonitorSize} bytes, " +
                              $"reduction={reduction:P1}");
        }
        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"FAILED: {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static Task TestMonitorSerializationAsync()
    {
        CMonitorProgress source = CreateMonitorProgress(512);
        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(
            source, ABIJsonContext.Default.CMonitorProgress);
        CMonitorProgress restored = JsonSerializer.Deserialize(
            utf8, ABIJsonContext.Default.CMonitorProgress)
            ?? throw new InvalidDataException("反序列化结果为空。");

        byte[] binary = MemoryPackSerializer.Serialize(source, ABIMemoryPack.Options);
        CMonitorProgress binaryRestored = MemoryPackSerializer.Deserialize<CMonitorProgress>(
            binary, ABIMemoryPack.Options)
            ?? throw new InvalidDataException("MemoryPack 反序列化结果为空。");

        Equal(2, restored.data.Cpus.Count);
        Equal(source.data.Cpus[0].Hardware.Name, restored.data.Cpus[0].Hardware.Name);
        Equal(source.data.Cpus[0].Load.Count, restored.data.Cpus[0].Load.Count);
        Equal(source.data.Gpus![0].Sensors.Count, restored.data.Gpus![0].Sensors.Count);
        Equal(2, binaryRestored.data.Cpus.Count);
        Equal(source.data.Cpus[0].Hardware.Name, binaryRestored.data.Cpus[0].Hardware.Name);
        Equal(source.data.Cpus[0].Load.Count, binaryRestored.data.Cpus[0].Load.Count);
        Equal(source.data.Gpus[0].Sensors.Count, binaryRestored.data.Gpus![0].Sensors.Count);
        return Task.CompletedTask;
    }

    private static async Task TestLargeMessagesAsync()
    {
        foreach (int payloadSize in new[] { 1024, 4 * 1024 * 1024 + 123, 16 * 1024 * 1024,
                     48 * 1024 * 1024 })
        {
            await RoundTripPipeAsync(payloadSize, $"large-{payloadSize}");
        }
    }

    private static Task TestConcurrentPipesAsync() => Task.WhenAll(
        Enumerable.Range(0, 8).Select(index =>
            RoundTripPipeAsync(8 * 1024 * 1024 + index * 31, $"parallel-{index}")));

    private static async Task RoundTripPipeAsync(int payloadSize, string operation)
    {
        string pipeName = $"ABIRuntime.Test.{Guid.NewGuid():N}";
        byte[] payload = CreateDeterministicPayload(payloadSize);
        string expectedHash = Convert.ToHexString(SHA256.HashData(payload));
        Guid requestId = Guid.NewGuid();

        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var client = new NamedPipeClientStream(".", pipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        Task connectClient = client.ConnectAsync(timeout.Token);
        await server.WaitForConnectionAsync(timeout.Token);
        await connectClient;

        var sent = new PipeMessage(PipeMessageKind.Progress, requestId,
            PipeProtocolVersion.Current,
            operation, payload, 50, "large payload");
        Task writer = PipeProtocol.WriteAsync(server, sent, timeout.Token).AsTask();
        PipeMessage received = await PipeProtocol.ReadAsync(client, timeout.Token);
        await writer;

        Equal(requestId, received.RequestId);
        Equal(payloadSize, received.Payload.Length);
        Equal(expectedHash, Convert.ToHexString(SHA256.HashData(received.Payload)));

        Task echoWriter = PipeProtocol.WriteAsync(client, received, timeout.Token).AsTask();
        PipeMessage echoed = await PipeProtocol.ReadAsync(server, timeout.Token);
        await echoWriter;
        if (!payload.AsSpan().SequenceEqual(echoed.Payload))
            throw new InvalidDataException("回传 Payload 内容不一致。");
    }

    private static async Task TestCancellationMessageAsync()
    {
        string pipeName = $"ABIRuntime.CancelTest.{Guid.NewGuid():N}";
        Guid id = Guid.NewGuid();
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var client = new NamedPipeClientStream(".", pipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task connection = client.ConnectAsync(timeout.Token);
        await server.WaitForConnectionAsync(timeout.Token);
        await connection;

        Task writer = PipeProtocol.WriteAsync(client,
            new PipeMessage(PipeMessageKind.Cancel, id, PipeProtocolVersion.Current,
                "cancel.test", Array.Empty<byte>()),
            timeout.Token).AsTask();
        PipeMessage message = await PipeProtocol.ReadAsync(server, timeout.Token);
        await writer;
        Equal(PipeMessageKind.Cancel, message.Kind);
        Equal(id, message.RequestId);
    }

    private static async Task TestDirectMonitorAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        int count = 0;
        int largestPayload = 0;
        var progress = new InlineProgress<CMonitorProgress>(value =>
        {
            byte[] jsonPayload = JsonSerializer.SerializeToUtf8Bytes(
                value, ABIJsonContext.Default.CMonitorProgress);
            byte[] payload = MemoryPackSerializer.Serialize(value, ABIMemoryPack.Options);
            CMonitorProgress roundTrip = MemoryPackSerializer.Deserialize<CMonitorProgress>(
                payload, ABIMemoryPack.Options)
                ?? throw new InvalidDataException("监控进度反序列化为空。");
            if (roundTrip.data.Cpus.Count == 0 || roundTrip.data.Cpus[0].Hardware is null)
                throw new InvalidDataException("CPU 硬件数据为空。");
            largestPayload = Math.Max(largestPayload, payload.Length);
            _lastJsonMonitorSize = Math.Max(_lastJsonMonitorSize, jsonPayload.Length);
            _lastMemoryPackMonitorSize = Math.Max(_lastMemoryPackMonitorSize, payload.Length);
            if (Interlocked.Increment(ref count) >= 3)
                timeout.Cancel();
        });

        try
        {
            await new ComputerMonitorService().ExecuteAsync(
                new CMonitorRequest(), progress, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
        }

        if (count < 3 || largestPayload == 0)
            throw new InvalidDataException($"监控采样不足：{count}，数据长度：{largestPayload}。");
    }

    private static async Task TestMonitorServicePipeAsync()
    {
        string pipeName = $"ABIRuntime.ServiceTest.{Guid.NewGuid():N}";
        Guid requestId = Guid.NewGuid();
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var client = new NamedPipeClientStream(".", pipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task connection = client.ConnectAsync(timeout.Token);
        await server.WaitForConnectionAsync(timeout.Token);
        await connection;

        var registry = new PrivilegedServiceRegistry();
        registry.Add(new ComputerMonitorService());
        if (!registry.TryGet(Contract.ComputerMonitorContract.Operation,
                out IServiceInvoker invoker))
        {
            throw new InvalidOperationException("监控服务注册失败。");
        }

        byte[] requestPayload = MemoryPackSerializer.Serialize(
            new CMonitorRequest(), ABIMemoryPack.Options);
        var request = new PipeMessage(PipeMessageKind.Request, requestId,
            PipeProtocolVersion.Current,
            Contract.ComputerMonitorContract.Operation, requestPayload);
        using var serviceCancellation = new CancellationTokenSource();

        Task hostTask = Task.Run(async () =>
        {
            try
            {
                await invoker.InvokeAsync(server, request, serviceCancellation.Token);
            }
            catch (OperationCanceledException) when (serviceCancellation.IsCancellationRequested)
            {
                await PipeProtocol.WriteAsync(server,
                    new PipeMessage(PipeMessageKind.Cancelled, requestId,
                        PipeProtocolVersion.Current, request.Operation, Array.Empty<byte>()),
                    CancellationToken.None);
            }
        });

        int progressCount = 0;
        while (true)
        {
            PipeMessage message = await PipeProtocol.ReadAsync(client, timeout.Token);
            Equal(requestId, message.RequestId);
            if (message.Kind == PipeMessageKind.Progress)
            {
                CMonitorProgress value = MemoryPackSerializer.Deserialize<CMonitorProgress>(
                    message.Payload, ABIMemoryPack.Options)
                    ?? throw new InvalidDataException("管道监控进度为空。");
                if (value.data.Cpus.Count == 0 || value.data.Cpus[0].Hardware is null)
                    throw new InvalidDataException("管道 CPU 数据为空。");
                if (++progressCount == 3)
                    serviceCancellation.Cancel();
            }
            else if (message.Kind == PipeMessageKind.Cancelled)
            {
                break;
            }
        }

        await hostTask.WaitAsync(timeout.Token);
        Equal(3, progressCount);
    }

    private static async Task TestElevatedHostRoutingAsync()
    {
        string controlName = $"ABIRuntime.ControlTest.{Guid.NewGuid():N}";
        string controlSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var control = new NamedPipeServerStream(controlName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var registry = new PrivilegedServiceRegistry();
        registry.Add(new ComputerMonitorService());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        Task<int> hostTask = ElevatedHost.RunForTestAsync(controlName, controlSecret, registry);
        await control.WaitForConnectionAsync(timeout.Token);
        PipeMessage controlHandshake = await PipeProtocol.ReadAsync(control, timeout.Token);
        Equal(OperationNames.Handshake, controlHandshake.Operation);
        if (!Convert.FromHexString(controlSecret).AsSpan()
                .SequenceEqual(controlHandshake.Payload))
            throw new InvalidDataException("控制管道密钥不匹配。");

        Guid requestId = Guid.NewGuid();
        string requestName = $"ABIRuntime.RequestTest.{Guid.NewGuid():N}";
        string requestSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var requestPipe = new NamedPipeServerStream(requestName,
            PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        byte[] openPayload = MemoryPackSerializer.Serialize(
            new OpenRequestMessage(requestName, requestSecret), ABIMemoryPack.Options);

        Task openWriter = PipeProtocol.WriteAsync(control,
            new PipeMessage(PipeMessageKind.Request, requestId,
                PipeProtocolVersion.Current,
                OperationNames.OpenRequest, openPayload), timeout.Token).AsTask();
        await requestPipe.WaitForConnectionAsync(timeout.Token);
        await openWriter;
        PipeMessage requestHandshake = await PipeProtocol.ReadAsync(requestPipe, timeout.Token);
        Equal(requestId, requestHandshake.RequestId);
        Equal(OperationNames.RequestHandshake, requestHandshake.Operation);
        if (!Convert.FromHexString(requestSecret).AsSpan()
                .SequenceEqual(requestHandshake.Payload))
            throw new InvalidDataException("请求管道密钥不匹配。");

        byte[] requestPayload = MemoryPackSerializer.Serialize(
            new CMonitorRequest(), ABIMemoryPack.Options);
        await PipeProtocol.WriteAsync(requestPipe,
            new PipeMessage(PipeMessageKind.Request, requestId,
                PipeProtocolVersion.Current,
                Contract.ComputerMonitorContract.Operation, requestPayload), timeout.Token);

        int samples = 0;
        while (true)
        {
            PipeMessage message = await PipeProtocol.ReadAsync(requestPipe, timeout.Token);
            if (message.Kind == PipeMessageKind.Progress)
            {
                CMonitorProgress progress = MemoryPackSerializer.Deserialize<CMonitorProgress>(
                    message.Payload, ABIMemoryPack.Options)
                    ?? throw new InvalidDataException("宿主路由进度为空。");
                if (progress.data.Cpus.Count == 0 || progress.data.Cpus[0].Hardware is null)
                    throw new InvalidDataException("宿主路由 CPU 数据为空。");
                if (++samples == 3)
                {
                    Task cancelWriter = PipeProtocol.WriteAsync(requestPipe,
                        new PipeMessage(PipeMessageKind.Cancel, requestId,
                            PipeProtocolVersion.Current,
                            Contract.ComputerMonitorContract.Operation, Array.Empty<byte>()),
                        timeout.Token).AsTask();
                    await cancelWriter;
                }
            }
            else if (message.Kind == PipeMessageKind.Cancelled)
            {
                break;
            }
            else if (message.Kind == PipeMessageKind.Error)
            {
                throw new InvalidOperationException(message.Message);
            }
        }

        Equal(3, samples);
        control.Dispose();
        Equal(0, await hostTask.WaitAsync(timeout.Token));
    }

    private static async Task TestPublishedNativeDllAsync(string dllPath)
    {
        if (!File.Exists(dllPath))
            throw new FileNotFoundException("NativeAOT DLL 不存在。", dllPath);

        string report = Path.Combine(Path.GetTempPath(),
            $"Haiyu.ABI.NativeTest.{Guid.NewGuid():N}.txt");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"\"{dllPath}\",MonitorSelfTest \"{report}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("rundll32 启动失败。");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            string result = File.Exists(report) ? await File.ReadAllTextAsync(report) : string.Empty;
            if (process.ExitCode != 0 || !result.StartsWith("PASS|", StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"NativeAOT 自检失败。ExitCode={process.ExitCode}, Report={result}");
        }
        finally
        {
            File.Delete(report);
        }
    }

    private static CMonitorProgress CreateMonitorProgress(int sensors)
    {
        var values = Enumerable.Range(0, sensors)
            .ToDictionary(index => $"Sensor-{index:D5}", index => index / 10d);
        var hardware = new HardwareInfo("Synthetic CPU", "/cpu/0", "Cpu",
            new Dictionary<string, string> { ["Vendor"] = "Fixture" });
        var cpu = new CPUData
        {
            Hardware = hardware,
            Voltages = new(values),
            Temperature = 56.5,
            Load = new(values),
            Clock = new(values),
        };
        var gpu = new GPUData
        {
            Hardware = new HardwareInfo("Synthetic GPU", "/gpu/0", "GpuNvidia",
                new Dictionary<string, string>()),
            Voltages = new(values), Temperatures = new(values), Load = new(values),
            Clock = new(values), Fans = new(values), Power = new(values),
            Memory = new(values), Throughput = new(values), Controls = new(values),
            Factors = new(values), Sensors = new(values),
        };
        var secondCpu = new CPUData
        {
            Hardware = new HardwareInfo("Synthetic CPU 2", "/cpu/1", "Cpu",
                new Dictionary<string, string> { ["Vendor"] = "Fixture" }),
            Voltages = new(values), Temperature = 54.5,
            Load = new(values), Clock = new(values),
        };
        return new CMonitorProgress(new MonitorRecord([cpu, secondCpu], [gpu]));
    }

    private static byte[] CreateDeterministicPayload(int size)
    {
        byte[] payload = GC.AllocateUninitializedArray<byte>(size);
        for (int index = 0; index < payload.Length; index++)
            payload[index] = (byte)(index * 31 + 17);
        return payload;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidDataException($"断言失败。Expected={expected}, Actual={actual}");
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
