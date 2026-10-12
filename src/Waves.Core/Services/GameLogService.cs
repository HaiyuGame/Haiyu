namespace Waves.Core.Services;

/// <summary>
/// 鸣潮客户端日志工具：定位 Client\Saved\Logs 下的 Client*.log 并解密 ELog 加密内容
/// </summary>
public class GameLogService : IGameLogService
{
    /// <summary>
    /// 鸣潮客户端日志目录（相对游戏根目录）
    /// </summary>
    public const string LogsRelativeFolder = "Client\\Saved\\Logs";

    /// <summary>
    /// 只处理 Client 前缀日志（Client.log 与 Client-backup-*.log）；
    /// cgsdk_.log 未加密且每次启动被游戏覆盖、Logs\NGX 为 NVIDIA 配置，均不处理
    /// </summary>
    public const string LogSearchPattern = "Client*.log";

    /// <summary>
    /// 解密输出文件名后缀
    /// </summary>
    public const string DecryptedSuffix = ".decrypted.log";

    public GameLogService(
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        AppSettings appSettings,
        IServiceProvider serviceProvider
    )
    {
        LoggerService = loggerService;
        AppSettings = appSettings;
        ServiceProvider = serviceProvider;
    }

    public LoggerService LoggerService { get; }
    public AppSettings AppSettings { get; }
    public IServiceProvider ServiceProvider { get; }

    public async Task<GameLogLocateResult> LocateGameLogsAsync(CancellationToken token = default)
    {
        // 读取最近一次在游戏页选择的鸣潮上下文（与游戏页共享 GameLauncherBassFolder 配置）
        var contextName = await AppSettings.GetWavesAutoOpenContextAsync(token);
        if (string.IsNullOrWhiteSpace(contextName))
        {
            contextName = nameof(WavesMainGameContextV2);
        }
        var context = ServiceProvider.GetRequiredKeyedService<IGameContextV2>(contextName);
        var gameFolder = await context.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.GameLauncherBassFolder,
            token
        );
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            return new GameLogLocateResult { State = GameLogLocateState.GameFolderMissing };
        }

        var logsFolder = Path.Combine(gameFolder, LogsRelativeFolder);
        if (!Directory.Exists(logsFolder))
        {
            return new GameLogLocateResult { State = GameLogLocateState.LogsFolderMissing };
        }

        var logFiles = new DirectoryInfo(logsFolder)
            .EnumerateFiles(LogSearchPattern, SearchOption.TopDirectoryOnly)
            .OrderByDescending(x => x.LastWriteTime)
            .Select(x => x.FullName)
            .ToList();
        if (logFiles.Count == 0)
        {
            return new GameLogLocateResult { State = GameLogLocateState.NoLogFiles };
        }

        return new GameLogLocateResult { State = GameLogLocateState.Ready, LogFiles = logFiles };
    }

    public async Task<GameLogDecryptResult> DecryptGameLogsAsync(
        IReadOnlyList<string> logFiles,
        string outputFolder,
        CancellationToken token = default
    )
    {
        ArgumentNullException.ThrowIfNull(logFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

        var decryptedCount = 0;
        var copiedCount = 0;
        var failedCount = 0;
        foreach (var logFile in logFiles)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var input = await File.ReadAllBytesAsync(logFile, token);
                var outputPath = Path.Combine(
                    outputFolder,
                    Path.GetFileName(logFile) + DecryptedSuffix
                );

                // UTF-8 BOM 开头即明文（-closeELog 生效时生成），原样复制；否则按 ELog 双 key 异或解密
                var encrypted = KrKeyHelper.IsELogEncrypted(input);
                var output = encrypted ? KrKeyHelper.TransformELog(input) : input;
                await File.WriteAllBytesAsync(outputPath, output, token);

                if (encrypted)
                    decryptedCount++;
                else
                    copiedCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedCount++;
                LoggerService.WriteError($"解密日志失败 {Path.GetFileName(logFile)}: {ex.Message}");
            }
        }
        return new GameLogDecryptResult
        {
            DecryptedCount = decryptedCount,
            CopiedCount = copiedCount,
            FailedCount = failedCount,
        };
    }
}
