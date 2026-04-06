using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Haiyu.Plugin.Services;
using Haiyu.RpcClient;
using KuroGameDownloadProgram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Waves.Api.Models;
using Waves.Api.Models.GameWikiiClient;
using Waves.Core;
using Waves.Core.Common;
using Waves.Core.Contracts;
using Waves.Core.GameContext;
using Waves.Core.GameContext.Contexts;
using Waves.Core.GameContext.ContextsV2;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Waves.Core.Settings;

GameContextFactory.GameBassPath =
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Waves";

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddGameContext();
        services.AddSingleton<CloudGameService>();
        services.AddSingleton<CloudConfigManager>(
            (s) =>
            {
                return new CloudConfigManager(AppSettings.CloudFolderPath);
            }
        );
        services.AddSingleton<IHttpClientService, HttpClientService>();
        services.AddSingleton<AppSettings>();
        services.AddKeyedSingleton<LoggerService>(
            "AppLog",
            (s, e) =>
            {
                var logger = new LoggerService();
                logger.InitLogger(AppSettings.LogPath, Serilog.RollingInterval.Day);
                return logger;
            }
        );
    })
    .Build();

var v2 = host.Services.GetRequiredKeyedService<IGameContextV2>(nameof(WavesMainGameContextV2));
await v2.InitAsync();
v2.ProgressState.OnProgressChanged += (t) =>
{
    int oldLeft = Console.CursorLeft;
    int oldTop = Console.CursorTop;
    try
    {
        Console.SetCursorPosition(0, 0);
        double percentage = t.Percentage;
        string progressBar = "[";
        int filledTabs = (int)(percentage / 2);
        progressBar += new string('=', filledTabs);
        progressBar += new string(' ', 50 - filledTabs);
        progressBar += $"] {percentage,6:F2}%";
        Console.Write(progressBar.PadRight(Console.WindowWidth - 1));
        Console.SetCursorPosition(0, 1);
        string stepInfo =
            $"IsProd:{t.Prod} Step {t.CurrentStepIndex + 1}/{t.TotalSteps}: {t.StepName} | Action: {t.CurrentAction} | {t.CurrentStepTip}";
        Console.Write(stepInfo.PadRight(Console.WindowWidth - 1));
        Console.SetCursorPosition(0, 2);
        string currentFile =
            $"Current File: {System.IO.Path.GetFileName(t.FilePath)} | {GameProgressTracker.FormatBytes(t.FileCurrentSize)} / {GameProgressTracker.FormatBytes(t.FileTotalSize)}";
        Console.Write(currentFile.PadRight(Console.WindowWidth - 1));
        Console.SetCursorPosition(0, 3);
        string speedInfo =
            $"Speed: {t.GetSpeedText()} | Bytes: {GameProgressTracker.FormatBytes(t.CurrentBytes)} / {GameProgressTracker.FormatBytes(t.TotalBytes)}";
        Console.Write(speedInfo.PadRight(Console.WindowWidth - 1));
        int line = 4;
        Console.SetCursorPosition(0, line++);
        Console.Write("Active Files:".PadRight(Console.WindowWidth - 1));
        var activeSnap = t.ActiveFiles.ToArray();
        foreach (var file in activeSnap)
        {
            Console.SetCursorPosition(0, line++);
            string fileInfo =
                $" -> {file.Key}: {GameProgressTracker.FormatBytes(file.Value.Current)} / {GameProgressTracker.FormatBytes(file.Value.Total)}";
            Console.Write(fileInfo.PadRight(Console.WindowWidth - 1));
        }
        for (int i = 0; i < 5; i++)
        {
            Console.SetCursorPosition(0, line + i);
            Console.Write(new string(' ', Console.WindowWidth - 1));
        }
    }
    catch { }
    finally
    {
        Console.SetCursorPosition(0, 15);
    }
};

Console.Clear();
Console.SetCursorPosition(0, 15);
await v2.StartInstallGameResource(true);
while (true)
{
    Console.WriteLine("Q停止，P暂停，R恢复，输入数字设定下载速度（MB）,回车确认");
    var input = Console.ReadLine();
    switch (input)
    {
        case "Q":
            await v2.StopCannelTaskAsync();
            return;
        case "P":
            await v2.PauseDownloadAsync();
            break;
        case "R":
            await v2.ResumeDownloadAsync();
            break;
        case var s when long.TryParse(s, out long speed):
            await v2.SetDownloadSpeedAsync(speed);
            break;
    }
}


public class TestContext : KuroGameContextBaseV2
{
    public TestContext(KuroGameApiConfig config, string contextName)
        : base(config, contextName) { }

    public override string GameContextNameKey => throw new NotImplementedException();

    public override GameType GameType => throw new NotImplementedException();

    public override Type ContextType => throw new NotImplementedException();
}
