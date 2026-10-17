using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Models.OfficialLauncher;
using Waves.Core.Services;
using Waves.Settings;

namespace Project.Test;

/// <summary>
/// 官方启动器安装记录工具（OfficialLauncherRecordService）写入行为测试。
/// 只测纯写入/移除逻辑；上下文解析与服务器拉取走真实 DI，不在此覆盖。
/// </summary>
[TestClass]
public class OfficialLauncherRecordServiceTests
{
    private static OfficialInstallRecord BundleRecord(
        string bundle = "HD",
        string appId = "10003",
        params (string Name, string Version)[] packs
    ) =>
        new(
            appId,
            bundle,
            packs.Select(x => new OfficialPackVersion(x.Name, x.Version)).ToList()
        );

    [TestMethod]
    public async Task WriteInstallRecordCreatesAllFilesInEmptyDirectory()
    {
        var dir = NewDir();
        var service = NewService();
        var result = await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1"), ("hd", "3.7.1")),
            CancellationToken.None
        );
        Assert.IsFalse(result.HasFailed);
        Assert.AreEqual(3, result.WrittenCount);

        var config = await ReadJson(
            Path.Combine(dir, "launcherDownloadConfig.json"),
            OfficialLauncherJsonContext.Default.OfficialInstalledConfig
        );
        Assert.AreEqual("10003", config.AppId);
        CollectionAssert.AreEqual(new[] { "HD" }, config.Bundles.Keys.ToList());
        Assert.AreEqual("3.7.1", config.Bundles["HD"].Version);
        CollectionAssert.AreEqual(new[] { "common", "hd" }, config.Bundles["HD"].ResourcePacks);
        Assert.AreEqual(string.Empty, config.Version);

        var commonJson = await File.ReadAllTextAsync(
            Path.Combine(dir, "launcherDownloadConfig", "common.json")
        );
        StringAssert.Contains(commonJson, @"""packName"":""common""");
        StringAssert.Contains(commonJson, @"""version"":""3.7.1""");
    }

    [TestMethod]
    public async Task WriteInstallRecordMergesBundleNodeAndPreservesOtherBundles()
    {
        var dir = NewDir();
        var configPath = Path.Combine(dir, "launcherDownloadConfig.json");
        await File.WriteAllTextAsync(
            configPath,
            """{"appId":"10003","version":"","state":"","bundles":{"SD":{"version":"3.6.0","state":"","resourcePacks":["common","sd"]}}}"""
        );
        var service = NewService();
        var result = await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1"), ("hd", "3.7.1")),
            CancellationToken.None
        );
        Assert.IsFalse(result.HasFailed);
        var config = await ReadJson(
            configPath,
            OfficialLauncherJsonContext.Default.OfficialInstalledConfig
        );
        Assert.IsTrue(config.Bundles.ContainsKey("SD"));
        Assert.AreEqual("3.6.0", config.Bundles["SD"].Version);
        Assert.AreEqual("3.7.1", config.Bundles["HD"].Version);
    }

    [TestMethod]
    public async Task PackVersionFileSkippedWhenContentIdentical()
    {
        var dir = NewDir();
        var packPath = Path.Combine(dir, "launcherDownloadConfig", "common.json");
        Directory.CreateDirectory(Path.GetDirectoryName(packPath)!);
        const string existing = """{"packName":"common","version":"3.7.1"}""";
        await File.WriteAllTextAsync(packPath, existing);
        var before = File.GetLastWriteTimeUtc(packPath);

        var service = NewService();
        var result = await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1")),
            CancellationToken.None
        );
        Assert.IsFalse(result.HasFailed);
        // 幂等跳过但计入成功清单。
        CollectionAssert.Contains(result.WrittenFiles.ToList(), "launcherDownloadConfig/common.json");
        Assert.AreEqual(existing, (await File.ReadAllTextAsync(packPath)).Trim());
        Assert.AreEqual(before, File.GetLastWriteTimeUtc(packPath));
    }

    [TestMethod]
    public async Task LegacyRecordWritesTopLevelVersionOnly()
    {
        var dir = NewDir();
        var service = NewService();
        var record = new OfficialInstallRecord(
            "10003",
            "default",
            [new OfficialPackVersion("default", "3.3.0")]
        )
        {
            IsLegacy = true,
        };
        var result = await service.WriteInstallRecordAsync(dir, record, CancellationToken.None);
        Assert.IsFalse(result.HasFailed);
        var config = await ReadJson(
            Path.Combine(dir, "launcherDownloadConfig.json"),
            OfficialLauncherJsonContext.Default.OfficialInstalledConfig
        );
        Assert.AreEqual("3.3.0", config.Version);
        Assert.AreEqual(0, config.Bundles.Count);
        Assert.IsFalse(Directory.Exists(Path.Combine(dir, "launcherDownloadConfig")));
    }

    [TestMethod]
    public async Task CorruptedInstalledConfigIsRebuilt()
    {
        var dir = NewDir();
        await File.WriteAllTextAsync(
            Path.Combine(dir, "launcherDownloadConfig.json"),
            "not a json {{{"
        );
        var service = NewService();
        var result = await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1")),
            CancellationToken.None
        );
        Assert.IsFalse(result.HasFailed);
        var config = await ReadJson(
            Path.Combine(dir, "launcherDownloadConfig.json"),
            OfficialLauncherJsonContext.Default.OfficialInstalledConfig
        );
        Assert.AreEqual("HD", config.Bundles.Keys.Single());
    }

    [TestMethod]
    public async Task IdempotentSkipCountsAsWrittenWithoutTempFiles()
    {
        var dir = NewDir();
        var service = NewService();
        // 首次写入落盘。
        await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1")),
            CancellationToken.None
        );
        var before = File.GetLastWriteTimeUtc(Path.Combine(dir, "launcherDownloadConfig", "common.json"));
        // 重复写入：common 幂等跳过（计入成功、不产生临时文件），安装配置内容变化正常替换。
        var result = await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1")),
            CancellationToken.None
        );
        Assert.IsFalse(result.HasFailed);
        Assert.AreEqual(2, result.WrittenCount);
        Assert.AreEqual(before, File.GetLastWriteTimeUtc(Path.Combine(dir, "launcherDownloadConfig", "common.json")));
        Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public async Task RemoveRecordsDeletesAllWrittenFiles()
    {
        var dir = NewDir();
        var service = NewService();
        await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1"), ("hd", "3.7.1")),
            CancellationToken.None
        );
        await File.WriteAllTextAsync(Path.Combine(dir, "LocalGameResources.json"), "{}");
        Assert.IsTrue(await service.HasAnyRecordAsync(dir));

        var result = await service.RemoveRecordsAsync(dir, CancellationToken.None);
        Assert.IsFalse(result.HasFailed);
        // 移除计数与写入口径一致：配置 1 + 分包版本 2 + 清单 1 = 4。
        Assert.AreEqual(4, result.RemovedEntries.Count);
        CollectionAssert.AreEqual(
            new[] { "launcherDownloadConfig.json", "launcherDownloadConfig/common.json", "launcherDownloadConfig/hd.json", "LocalGameResources.json" },
            result.RemovedEntries.ToList()
        );
        Assert.IsFalse(File.Exists(Path.Combine(dir, "launcherDownloadConfig.json")));
        Assert.IsFalse(File.Exists(Path.Combine(dir, "launcherDownloadConfig", "common.json")));
        // 分包版本目录本身保留（不整目录删除，避免误删目录中的其他内容）。
        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "launcherDownloadConfig")));
        Assert.IsFalse(File.Exists(Path.Combine(dir, "LocalGameResources.json")));
        Assert.IsFalse(await service.HasAnyRecordAsync(dir));
    }

    [TestMethod]
    public async Task RemoveRecordsKeepsNonRecordFilesInPackDirectory()
    {
        var dir = NewDir();
        var service = NewService();
        await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1")),
            CancellationToken.None
        );
        // 用户/其他工具放入的未识别文件：移除时须保留，不误删。
        var stranger = Path.Combine(dir, "launcherDownloadConfig", "stranger.dat");
        await File.WriteAllTextAsync(stranger, "keep me");

        var result = await service.RemoveRecordsAsync(dir, CancellationToken.None);
        Assert.IsFalse(result.HasFailed);
        CollectionAssert.Contains(result.RemovedEntries.ToList(), "launcherDownloadConfig/common.json");
        Assert.IsTrue(File.Exists(stranger));
        Assert.AreEqual("keep me", await File.ReadAllTextAsync(stranger));
        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "launcherDownloadConfig")));
    }

    [TestMethod]
    public async Task AdditionalBundlesWriteNodesAndPackFiles()
    {
        var dir = NewDir();
        var service = NewService();
        // 多画质并存：主 HD + 已安装的 UHD（其分包为 common/uhd）；common 为两画质共有分包。
        var record = new OfficialInstallRecord(
            "10003",
            "HD",
            [new OfficialPackVersion("common", "3.7.1"), new OfficialPackVersion("hd", "3.7.1")]
        )
        {
            AdditionalBundles =
            [
                new OfficialBundleRecord(
                    "UHD",
                    [new OfficialPackVersion("common", "3.7.1"), new OfficialPackVersion("uhd", "3.7.1")]
                ),
            ],
        };
        var result = await service.WriteInstallRecordAsync(dir, record, CancellationToken.None);
        Assert.IsFalse(result.HasFailed);
        // 配置 1 + HD 分包 2 + UHD 新增分包 1（common 两画质共有，同一文件只计一次）= 4。
        Assert.AreEqual(4, result.WrittenCount);
        CollectionAssert.AreEqual(
            new[] { "launcherDownloadConfig.json", "launcherDownloadConfig/common.json", "launcherDownloadConfig/hd.json", "launcherDownloadConfig/uhd.json" },
            result.WrittenFiles.ToList()
        );

        var config = await ReadJson(
            Path.Combine(dir, "launcherDownloadConfig.json"),
            OfficialLauncherJsonContext.Default.OfficialInstalledConfig
        );
        CollectionAssert.AreEquivalent(new[] { "HD", "UHD" }, config.Bundles.Keys.ToList());
        CollectionAssert.AreEqual(new[] { "common", "hd" }, config.Bundles["HD"].ResourcePacks);
        CollectionAssert.AreEqual(new[] { "common", "uhd" }, config.Bundles["UHD"].ResourcePacks);
        Assert.AreEqual("3.7.1", config.Bundles["UHD"].Version);
        Assert.IsTrue(File.Exists(Path.Combine(dir, "launcherDownloadConfig", "uhd.json")));

        // 写入/移除计数口径一致：写 4 也应移除 4。
        var removed = await service.RemoveRecordsAsync(dir, CancellationToken.None);
        Assert.IsFalse(removed.HasFailed);
        Assert.AreEqual(result.WrittenCount, removed.RemovedEntries.Count);
    }

    [TestMethod]
    public async Task RemoveRecordsOnCleanDirectoryReportsNothing()
    {
        var dir = NewDir();
        var service = NewService();
        Assert.IsFalse(await service.HasAnyRecordAsync(dir));
        var result = await service.RemoveRecordsAsync(dir, CancellationToken.None);
        Assert.IsFalse(result.HasFailed);
        Assert.AreEqual(0, result.RemovedEntries.Count);
    }

    [TestMethod]
    public async Task PartialGenerationFailureKeepsOldStateAndCleansTempFiles()
    {
        var dir = NewDir();
        var configPath = Path.Combine(dir, "launcherDownloadConfig.json");
        // 旧状态：HD 已安装 3.6.0。
        await File.WriteAllTextAsync(
            configPath,
            """{"appId":"10003","version":"","state":"","bundles":{"HD":{"version":"3.6.0","state":"","resourcePacks":["common"]}}}"""
        );
        // 占用 common.json（无删除共享），使分包版本文件的临时文件无法替换目标 → 全部不生效。
        // 生成阶段用独立目标名不受占用影响，这里通过锁目标文件触发 Move 失败。
        var service = NewService();
        await service.WriteInstallRecordAsync(
            dir,
            BundleRecord("HD", "10003", ("common", "3.7.1")),
            CancellationToken.None
        );
        // 记录"写入成功后"的基线状态。
        var baseline = await File.ReadAllTextAsync(configPath);

        // 用只共享读的锁锁住 common.json，使 File.Move(overwrite) 失败 → 替换阶段失败。
        using (var locked = new FileStream(
            Path.Combine(dir, "launcherDownloadConfig", "common.json"),
            FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = await service.WriteInstallRecordAsync(
                dir,
                BundleRecord("HD", "10003", ("common", "3.7.2"), ("hd", "3.7.2")),
                CancellationToken.None
            );
            // 替换失败 → hd.json 临时文件被回滚清理，且计入失败。
            Assert.IsTrue(result.HasFailed);
            CollectionAssert.Contains(result.FailedFiles.ToList(), "launcherDownloadConfig/common.json");
            // 未替换的临时文件已清理，无 .tmp 残留。
            Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories).Length);
        }

        // 旧状态保持：配置文件本次已生效（替换阶段它先于 common 成功），但失败的 common.json 保留旧内容、
        // hd.json 临时文件已回滚清理——不会出现配置引用 hd 而版本文件缺失的"半套"之外的新增脏数据。
        var after = await ReadJson(configPath, OfficialLauncherJsonContext.Default.OfficialInstalledConfig);
        Assert.AreEqual("3.7.1", after.Bundles["HD"].Version);
    }

    private static OfficialLauncherRecordService NewService()
    {
        // InitLogger 到临时文件：服务写入路径依赖日志（如未识别文件保留提示），
        // 未初始化的 LoggerService 写日志会 NRE（Option 为 null）。
        var logger = new LoggerService();
        logger.InitLogger(
            Path.Combine(Path.GetTempPath(), $"haiyu-official-log-{Guid.NewGuid():N}.log"),
            Serilog.RollingInterval.Infinite
        );
        return new OfficialLauncherRecordService(
            logger,
            new AppSettings(),
            new ServiceCollection().BuildServiceProvider()
        );
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"haiyu-official-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<T> ReadJson<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        var content = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize(content, typeInfo)!;
    }
}
