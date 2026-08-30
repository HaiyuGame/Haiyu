using ABI.Models;
using LibreHardwareMonitor.Hardware;

namespace Haiyu.ABI.Common;

public sealed class MonitorCounter : IDisposable
{
    private readonly Computer _computer;
    private bool _disposed;


    /// <summary>全部硬件数据刷新后的输出回调。</summary>
    public Action<MonitorRecord>? MonitorOutput { get; set; }

    /// <summary>
    /// 最近一次采集到的 CPU 数据。
    /// </summary>
    public IReadOnlyList<CPUData> CurrentCPUs { get; private set; } = [];

    /// <summary>兼容单路调用端，返回最近采集结果中的第一颗物理 CPU。</summary>
    public CPUData? CurrentCPUData => CurrentCPUs.FirstOrDefault();
    public MonitorRecord? CurrentData { get; private set; }

    public MonitorCounter()
    {
        _computer = new Computer()
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsNetworkEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true
        };
    }

    /// <summary>持续采集并在当前任务中传递结果，取消后释放硬件句柄。</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _computer.Open();
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var hardware in _computer.Hardware)
                    UpdateRecursive(hardware);
                var cpus = new List<CPUData>();
                var gpus = new List<GPUData>();
                MemoryData? memory = null;
                VirtualMemoryData? virtualMemory = null;
                var networks = new List<NetworkData>();

                foreach (var printHardwawre in _computer.Hardware)
                {
                    if (printHardwawre.HardwareType == HardwareType.Cpu)
                    {
                        cpus.Add(GetCPUData(printHardwawre));
                    }
                    else if (printHardwawre.HardwareType is HardwareType.GpuAmd
                             or HardwareType.GpuIntel or HardwareType.GpuNvidia)
                        gpus.Add(GetGPUData(printHardwawre));
                    else if (printHardwawre.HardwareType == HardwareType.Memory)
                    {
                        ReadMemoryData(printHardwawre, ref memory, ref virtualMemory);
                    }
                    else if (printHardwawre.HardwareType == HardwareType.Network)
                        networks.Add(GetNetworkData(printHardwawre));
                }

                if (cpus.Count > 0)
                {
                    CurrentCPUs = cpus;
                    CurrentData = new MonitorRecord(cpus, gpus,
                        memory, virtualMemory, networks);
                    MonitorOutput?.Invoke(CurrentData);
                }
            }
        }
        finally
        {
            _computer.Close();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _computer.Close();
    }

    private CPUData GetCPUData(IHardware printHardwawre)
    {
        ISensor[] sensors = EnumerateSensors(printHardwawre)
            .Where(sensor => sensor.Value.HasValue)
            .ToArray();

        var temperatureSensor = FindBestSensor(
            sensors,
            SensorType.Temperature,
            ["CPU Package", "Core (Tctl/Tdie)", "CPU (Tctl/Tdie)", "Tctl/Tdie", "CPU Core"]
        );

        return new CPUData
        {
            Hardware = GetHardwareInfo(printHardwawre),
            Voltages = CaptureSensors(sensors, SensorType.Voltage),
            Temperature = Finite(temperatureSensor?.Value ?? 0d),
            Load = CaptureSensors(sensors, SensorType.Load),
            Clock = CaptureSensors(sensors, SensorType.Clock),
        };
    }

    private GPUData GetGPUData(IHardware hardware)
    {
        ISensor[] sensors = EnumerateSensors(hardware).Where(x => x.Value.HasValue).ToArray();
        return new GPUData
        {
            Hardware = GetHardwareInfo(hardware),
            Voltages = CaptureSensors(sensors, SensorType.Voltage),
            Temperatures = CaptureSensors(sensors, SensorType.Temperature),
            Load = CaptureSensors(sensors, SensorType.Load),
            Clock = CaptureSensors(sensors, SensorType.Clock),
            Fans = CaptureSensors(sensors, SensorType.Fan),
            Power = CaptureSensors(sensors, SensorType.Power),
            Memory = CaptureSensors(sensors, SensorType.SmallData),
            Throughput = CaptureSensors(sensors, SensorType.Throughput),
            Controls = CaptureSensors(sensors, SensorType.Control),
            Factors = CaptureSensors(sensors, SensorType.Factor),
            Sensors = CaptureAllSensors(sensors),
        };
    }

    private void ReadMemoryData(
        IHardware hardware,
        ref MemoryData? memory,
        ref VirtualMemoryData? virtualMemory)
    {
        ISensor[] sensors = EnumerateSensors(hardware).Where(x => x.Value.HasValue).ToArray();
        double Value(SensorType type, params string[] names) =>
            Finite(FindBestSensor(sensors, type, names)?.Value ?? 0d);

        var info = GetHardwareInfo(hardware);

        bool isVirtualNode = hardware.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase);
        bool isTotalNode = hardware.Name.Contains("Total Memory", StringComparison.OrdinalIgnoreCase)
                           || hardware.Name.Contains("Generic Memory", StringComparison.OrdinalIgnoreCase);
        bool hasLegacyVirtualSensors = sensors.Any(x =>
            x.Name.StartsWith("Virtual Memory", StringComparison.OrdinalIgnoreCase));
        if (isTotalNode)
        {
            double used = Value(SensorType.Data, "Memory Used");
            double available = Value(SensorType.Data, "Memory Available");
            memory = new MemoryData
            {
                Hardware = info, Used = used, Available = available,
                Total = used + available, Load = Value(SensorType.Load, "Memory")
            };
        }

        if (isVirtualNode || hasLegacyVirtualSensors)
        {
            double used = isVirtualNode
                ? Value(SensorType.Data, "Memory Used")
                : Value(SensorType.Data, "Virtual Memory Used");
            double available = isVirtualNode
                ? Value(SensorType.Data, "Memory Available")
                : Value(SensorType.Data, "Virtual Memory Available");
            virtualMemory = new VirtualMemoryData
            {
                Hardware = info, Used = used, Available = available,
                Total = used + available,
                Load = isVirtualNode
                    ? Value(SensorType.Load, "Memory")
                    : Value(SensorType.Load, "Virtual Memory")
            };
        }
    }

    private NetworkData GetNetworkData(IHardware hardware)
    {
        ISensor[] sensors = EnumerateSensors(hardware).Where(x => x.Value.HasValue).ToArray();
        double Value(SensorType type, params string[] names) =>
            Finite(FindBestSensor(sensors, type, names)?.Value ?? 0d);

        return new NetworkData
        {
            Hardware = GetHardwareInfo(hardware),
            UploadSpeed = Value(SensorType.Throughput, "Upload Speed", "Upload"),
            DownloadSpeed = Value(SensorType.Throughput, "Download Speed", "Download"),
            TotalUploaded = Value(SensorType.Data, "Data Uploaded", "Total Uploaded"),
            TotalDownloaded = Value(SensorType.Data, "Data Downloaded", "Total Downloaded"),
            Utilization = Value(SensorType.Load, "Network Utilization", "Utilization"),
            Sensors = CaptureAllSensors(sensors),
        };
    }

    private static HardwareInfo GetHardwareInfo(IHardware hardware) =>
        new(hardware.Name, hardware.Identifier.ToString(), hardware.HardwareType.ToString(),
            new Dictionary<string, string>(hardware.Properties, StringComparer.OrdinalIgnoreCase));

    private static Dictionary<string, double> CaptureAllSensors(IEnumerable<ISensor> sensors)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (ISensor sensor in sensors.Where(x => x.Value.HasValue))
            result[$"{sensor.SensorType}: {sensor.Name} ({sensor.Identifier})"] =
                Finite(sensor.Value!.Value);
        return result;
    }

    private static Dictionary<string, double> CaptureSensors(
        IEnumerable<ISensor> sensors,
        SensorType sensorType)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (ISensor sensor in sensors.Where(sensor =>
                     sensor.SensorType == sensorType && sensor.Value.HasValue))
        {
            string key = sensor.Name;
            if (result.ContainsKey(key))
                key = $"{sensor.Name} ({sensor.Identifier})";

            result[key] = Finite(sensor.Value!.Value);
        }

        return result;
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        foreach (ISensor sensor in hardware.Sensors)
            yield return sensor;

        foreach (IHardware subHardware in hardware.SubHardware)
        foreach (ISensor sensor in EnumerateSensors(subHardware))
            yield return sensor;
    }

    private static ISensor? FindBestSensor(
        IEnumerable<ISensor> source,
        SensorType type,
        string[] preferredNames)
    {
        var sensors = source
            .Where(x =>
                x.SensorType == type &&
                x.Value.HasValue)
            .ToArray();

        if (sensors.Length == 0)
            return null;

        foreach (var preferred in preferredNames)
        {
            var exact = sensors.FirstOrDefault(x =>
                x.Name.Equals(
                    preferred,
                    StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
                return exact;
        }

        foreach (var preferred in preferredNames)
        {
            var contains = sensors.FirstOrDefault(x =>
                x.Name.Contains(
                    preferred,
                    StringComparison.OrdinalIgnoreCase));

            if (contains is not null)
                return contains;
        }

        // 尽量别选完全静止的 0 值传感器
        var nonZero = sensors.FirstOrDefault(x =>
            Math.Abs(x.Value!.Value) > 0.0001f);

        return nonZero ?? sensors[0];
    }

    /// <summary>硬件驱动偶尔返回 NaN/Infinity；ABI 数据统一归一化为有效 JSON 数值。</summary>
    private static double Finite(double value) => double.IsFinite(value) ? value : 0d;

    /// <summary>
    /// 更新计数器
    /// </summary>
    /// <param name="hardware"></param>
    private void UpdateRecursive(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
            UpdateRecursive(subHardware);
    }
}
