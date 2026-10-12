using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Waves.Core;
using Waves.Core.Common;
using Waves.Core.GameContext;
using Waves.Core.GameContext.ContextsV2.Waves;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Waves.Settings;

namespace Project.Test;

/// <summary>
/// 鸣潮日志解密工具测试：
/// 在临时目录构造假的游戏目录结构（不依赖真实游戏安装），
/// 覆盖日志定位的前置分支与解密导出的有效性校验。
/// </summary>
[TestClass]
public class GameLogServiceTests
{
    private string _tempRoot = null!;
    private LoggerService _logger = null!;
    private AppSettings _appSettings = null!;
    private GameLogService _service = null!;

    [TestInitialize]
    public async Task SetupAsync()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Haiyu.GameLogTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        // GameContextFactory.GameBassPath 是静态属性，指向它以让 GameLocalConfig 落在临时目录
        _originalGameBassPath = GameContextFactory.GameBassPath;
        GameContextFactory.GameBassPath = _tempRoot;

        _logger = new LoggerService();
        var logDir = Path.Combine(_tempRoot, "appLogs");
        Directory.CreateDirectory(logDir);
        _logger.InitLogger(Path.Combine(logDir, "test.log"), Serilog.RollingInterval.Infinite);

        _appSettings = new AppSettings();
        // 真实容器：定位逻辑按 WavesAutoOpenContext 配置解析 keyed IGameContextV2（默认 WavesMainGameContextV2）。
        // GameLocalConfig 在 InitAsync 中创建，必须初始化默认上下文（GameBassPath 已指向临时目录，无网络请求）。
        _provider = new ServiceCollection()
            .AddSingleton<AppSettings>()
            .AddGameContext()
            .BuildServiceProvider();
        var defaultContext = _provider.GetRequiredKeyedService<IGameContextV2>(
            nameof(WavesMainGameContextV2)
        );
        await defaultContext.InitAsync();
        _service = new GameLogService(_logger, _appSettings, _provider);
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        GameContextFactory.GameBassPath = _originalGameBassPath;
        if (_provider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _provider?.Dispose();
        }
        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string? _originalGameBassPath;
    private ServiceProvider _provider = null!;

    #region 定位前置分支

    [TestMethod]
    public async Task Locate_WithoutGameFolder_ReturnsGameFolderMissing()
    {
        // AppSettings 默认无 WavesAutoOpenContext → 走默认 WavesMainGameContextV2，
        // 其 GameLocalConfig 从未保存过游戏目录 → 目录未设置
        var result = await _service.LocateGameLogsAsync();

        Assert.AreEqual(GameLogLocateState.GameFolderMissing, result.State);
        Assert.AreEqual(0, result.LogFiles.Count);
    }

    [TestMethod]
    public async Task Locate_GameFolderSetButLogsMissing_ReturnsLogsFolderMissing()
    {
        var gameFolder = Path.Combine(_tempRoot, "Game");
        Directory.CreateDirectory(gameFolder);
        await SaveGameFolderToDefaultContextAsync(gameFolder);

        var result = await _service.LocateGameLogsAsync();

        Assert.AreEqual(GameLogLocateState.LogsFolderMissing, result.State);
    }

    [TestMethod]
    public async Task Locate_LogsFolderExistsWithoutClientLogs_ReturnsNoLogFiles()
    {
        var logsFolder = CreateLogsFolder();
        // 只有 cgsdk_.log（不匹配 Client*.log）
        await File.WriteAllTextAsync(Path.Combine(logsFolder, "cgsdk_.log"), "sdk log");

        var result = await _service.LocateGameLogsAsync();

        Assert.AreEqual(GameLogLocateState.NoLogFiles, result.State);
    }

    [TestMethod]
    public async Task Locate_WithClientLogs_ReturnsReadyAndSortedByWriteTime()
    {
        var logsFolder = CreateLogsFolder();
        var newer = Path.Combine(logsFolder, "Client.log");
        var older = Path.Combine(logsFolder, "Client-backup-20260101.log");
        WriteEncryptedLog(newer, "newest");
        WriteEncryptedLog(older, "older");
        new FileInfo(older).LastWriteTime = DateTime.Now.AddHours(-2);

        var result = await _service.LocateGameLogsAsync();

        Assert.AreEqual(GameLogLocateState.Ready, result.State);
        Assert.AreEqual(2, result.LogFiles.Count);
        Assert.AreEqual(newer, result.LogFiles[0]);
        Assert.AreEqual(older, result.LogFiles[1]);
    }

    #endregion

    #region 解密导出

    [TestMethod]
    public async Task Decrypt_EncryptedLog_ProducesPlainText()
    {
        var logsFolder = CreateLogsFolder();
        var log = Path.Combine(logsFolder, "Client.log");
        // 真实明文日志固定以 BOM 开头（游戏写入时携带）
        WriteEncryptedLog(log, "\uFEFFLog file open, 2026-01-01 12:00:00\n[Info] 你好 Wuthering Waves\n");
        var output = NewOutputFolder();

        var result = await _service.DecryptGameLogsAsync([log], output);

        Assert.AreEqual(1, result.DecryptedCount);
        Assert.AreEqual(0, result.CopiedCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(0, result.KeyMismatchCount);

        var exported = File.ReadAllBytes(Path.Combine(output, "Client.log.decrypted.log"));
        Assert.IsTrue(exported.AsSpan().StartsWith("\uFEFF"u8), "解密结果应以 UTF-8 BOM 开头");
        StringAssert.Contains(
            new UTF8Encoding(false).GetString(exported),
            "[Info] 你好 Wuthering Waves"
        );
    }

    [TestMethod]
    public async Task Decrypt_PlainLog_IsCopiedUnchanged()
    {
        var logsFolder = CreateLogsFolder();
        var log = Path.Combine(logsFolder, "Client.log");
        // 游戏关闭日志加密时生成：BOM 开头的明文
        var plain = "\uFEFFLog file open, 2026-01-01\n[Info] plain\n"u8.ToArray();
        await File.WriteAllBytesAsync(log, plain);
        var output = NewOutputFolder();

        var result = await _service.DecryptGameLogsAsync([log], output);

        Assert.AreEqual(0, result.DecryptedCount);
        Assert.AreEqual(1, result.CopiedCount);

        var exported = File.ReadAllBytes(Path.Combine(output, "Client.log.decrypted.log"));
        CollectionAssert.AreEqual(plain, exported);
    }

    [TestMethod]
    public async Task Decrypt_KeyChanged_DetectsMismatchAndSkipsExport()
    {
        var logsFolder = CreateLogsFolder();
        var log = Path.Combine(logsFolder, "Client.log");
        // 用错误的双 key（0x11/0x33）加密模拟游戏更换 key 后的密文
        WriteEncryptedLogWithKeys(log, "Log file open, 2026-01-01\n[Info] content\n", 0x11, 0x33);
        var output = NewOutputFolder();

        var result = await _service.DecryptGameLogsAsync([log], output);

        // 校验失败：不导出、单独计数，不算解密成功也不算处理失败
        Assert.AreEqual(0, result.DecryptedCount);
        Assert.AreEqual(1, result.KeyMismatchCount);
        Assert.AreEqual(0, result.FailedCount);
        Assert.IsFalse(File.Exists(Path.Combine(output, "Client.log.decrypted.log")), "校验失败的文件不应导出");
    }

    [TestMethod]
    public async Task Decrypt_MixedFiles_CountsEachCategory()
    {
        var logsFolder = CreateLogsFolder();
        var encrypted = Path.Combine(logsFolder, "Client.log");
        var plain = Path.Combine(logsFolder, "Client-backup-20260101.log");
        WriteEncryptedLog(encrypted, "\uFEFFLog file open\n[Info] enc\n");
        await File.WriteAllBytesAsync(plain, "\uFEFFLog file open\n[Info] plain\n"u8.ToArray());
        var locked = Path.Combine(logsFolder, "Client-locked.log");
        await File.WriteAllTextAsync(locked, "placeholder");
        using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // 文件被独占占用 → 写入失败 → FailedCount
            var output = NewOutputFolder();
            var result = await _service.DecryptGameLogsAsync([encrypted, plain, locked], output);

            Assert.AreEqual(1, result.DecryptedCount);
            Assert.AreEqual(1, result.CopiedCount);
            Assert.AreEqual(1, result.FailedCount);
            Assert.AreEqual(0, result.KeyMismatchCount);
        }
    }

    [TestMethod]
    public async Task Decrypt_CanceledToken_ThrowsOperationCanceled()
    {
        var logsFolder = CreateLogsFolder();
        var log = Path.Combine(logsFolder, "Client.log");
        WriteEncryptedLog(log, "Log file open\n");
        var output = NewOutputFolder();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            async () =>
                await _service.DecryptGameLogsAsync(
                    [log],
                    output,
                    new CancellationToken(canceled: true)
                )
        );
    }

    #endregion

    #region 有效性校验纯函数

    [TestMethod]
    public void IsValidDecryptedLog_ValidPlainText_ReturnsTrue()
    {
        var plain = "\uFEFFLog file open, 2026-01-01\n[Info] 你好世界 mixed 中文 English\n"u8.ToArray();
        Assert.IsTrue(KrKeyHelper.IsValidDecryptedLog(plain));
    }

    [TestMethod]
    public void IsValidDecryptedLog_WrongKeyGarbage_ReturnsFalse()
    {
        // 用错误 key 解密正确加密的内容（模拟 key 变更），必须是乱码被判无效
        var plain = "\uFEFFLog file open, 2026-01-01\n[Info] content that is long enough to break utf8\n"u8.ToArray();
        var encrypted = KrKeyHelper.TransformELog(plain);
        var wrongDecrypted = DecryptWithKeys(encrypted, 0x11, 0x33);
        Assert.IsFalse(KrKeyHelper.IsValidDecryptedLog(wrongDecrypted), "错误 key 的解密结果必须被判为无效");
    }

    [TestMethod]
    public void IsValidDecryptedLog_ForgedBomButBrokenUtf8_ReturnsFalse()
    {
        // BOM 后跟非法 UTF-8 序列（孤立延续字节），伪造 BOM 也不能通过
        var forged = new byte[] { 0xEF, 0xBB, 0xBF, 0x80, 0x81, 0x82 };
        Assert.IsFalse(KrKeyHelper.IsValidDecryptedLog(forged));
    }

    [TestMethod]
    public void IsValidDecryptedLog_TooShort_ReturnsFalse()
    {
        Assert.IsFalse(KrKeyHelper.IsValidDecryptedLog([0xEF, 0xBB, 0xBF]));
        Assert.IsFalse(KrKeyHelper.IsValidDecryptedLog([]));
    }

    [TestMethod]
    public void IsValidDecryptedLog_NoBom_ReturnsFalse()
    {
        var noBom = "Log file open\n"u8.ToArray();
        Assert.IsFalse(KrKeyHelper.IsValidDecryptedLog(noBom));
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 向默认鸣潮上下文的本地配置写入游戏目录
    /// </summary>
    private async Task SaveGameFolderToDefaultContextAsync(string gameFolder)
    {
        var configPath = Path.Combine(
            GameContextFactory.GameBassPath,
            "MainConfig",
            "Settings.bat"
        );
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var config = new GameLocalConfig(configPath);
        await config.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, gameFolder);
    }

    private string CreateLogsFolder()
    {
        var gameFolder = Path.Combine(_tempRoot, "Game");
        var logsFolder = Path.Combine(gameFolder, "Client", "Saved", "Logs");
        Directory.CreateDirectory(logsFolder);
        SaveGameFolderToDefaultContextAsync(gameFolder).GetAwaiter().GetResult();
        return logsFolder;
    }

    private string NewOutputFolder()
    {
        var output = Path.Combine(_tempRoot, "Output_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(output);
        return output;
    }

    /// <summary>
    /// 以当前 ELog key（明文偶数字节 0xA5、奇数字节 0xEF）加密内容并写入
    /// </summary>
    private static void WriteEncryptedLog(string path, string plainText)
    {
        WriteEncryptedLogWithKeys(path, plainText, 0xA5, 0xEF);
    }

    /// <summary>
    /// 以指定双 key 加密内容并写入（错误 key 模拟游戏更换加密）
    /// </summary>
    private static void WriteEncryptedLogWithKeys(
        string path,
        string plainText,
        byte keyEven,
        byte keyOdd
    )
    {
        var plain = new UTF8Encoding(false).GetBytes(plainText);
        var encrypted = new byte[plain.Length];
        for (var i = 0; i < plain.Length; i++)
        {
            encrypted[i] = (byte)(
                plain[i] ^ ((plain[i] & 1) == 0 ? keyEven : keyOdd)
            );
        }
        File.WriteAllBytes(path, encrypted);
    }

    /// <summary>
    /// 用指定双 key 解密（密文偶数字节用 keyOdd、奇数字节用 keyEven，与加密互逆）
    /// </summary>
    private static byte[] DecryptWithKeys(byte[] data, byte keyEven, byte keyOdd)
    {
        var result = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ ((data[i] & 1) == 0 ? keyOdd : keyEven));
        }
        return result;
    }
}

#endregion
