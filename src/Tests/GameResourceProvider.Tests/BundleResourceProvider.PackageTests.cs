using System.Net;
using System.Text.Json;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Models.Options;
using Waves.Core.Services;
using Waves.Core.Services.GameResourceProvider;
using Waves.Core.GameContext.KruoGameContextBaseV2.Common;

namespace Project.Test;

public sealed partial class BundleResourceProviderTests
{
    [TestMethod]
    public async Task LivePackageSizesMatchOfficialSnapshotWithoutInstallingFiles()
    {
        if (Environment.GetEnvironmentVariable("HAIYU_LIVE_PACKAGE_TEST") != "1")
            Assert.Inconclusive("显式开启资源量联网测试后运行。");
        var root = Path.Combine(Path.GetTempPath(), "haiyu-package-live-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var local = new GameLocalConfig(Path.Combine(root, "settings.json"));
        await local.SaveConfigsAsync(new Dictionary<string, string>
        {
            [GameLocalSettingName.GameLauncherBassFolder] = root,
            [GameLocalSettingName.LocalGameVersion] = "3.7.1"
        });
        using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
        client.Timeout = TimeSpan.FromSeconds(90);
        var provider = new BundleGameResourceProvider(client);
        provider.SetConfig(local, Waves.Core.Models.CoreApi.KuroGameApiConfig.MainAPiConfig);
        foreach (var tier in new[] { "UHD", "HD", "SD" })
        {
            var full = await provider.GetInstallGameResourceAsync(new() { BundleName = tier, PackageType = GamePackageType.Full });
            var minimal = await provider.GetInstallGameResourceAsync(new() { BundleName = tier, PackageType = GamePackageType.Minimal });
            Assert.IsTrue(minimal.DownloadSize < full.DownloadSize);
            Assert.IsFalse(minimal.DefaultResource.Any(x => x.Dest.StartsWith("Client/Saved/Resources/", StringComparison.OrdinalIgnoreCase)));
            if (tier == "UHD" && full.NewGameVersion == "3.8.0")
            {
                Assert.AreEqual(109473034636L, full.DownloadSize);
                Assert.AreEqual(80372964398L, minimal.DownloadSize);
            }
            Console.WriteLine($"{tier}: full={full.DownloadSize}, minimal={minimal.DownloadSize}");
        }
        var update = await provider.GetUpdateGameResourceAsync(new() { BundleName = "UHD", PackageType = GamePackageType.Minimal });
        Assert.IsFalse(update.PatchResources.Any(x => x.IsSelected && x.IsGroup));
        Assert.IsFalse(File.Exists(Path.Combine(root, provider.ApiConfig.PackageStatePath!)));
        Assert.IsNull(await local.GetConfigAsync(GameLocalSettingName.GetPackageTypeKey("UHD")));
    }

    private const string TaggedIndex = """
        {"resource":[{"dest":"core.bin","size":10},{"dest":"Client/Saved/Resources/story.bin","size":20}],
        "tagGroups":{"min":{"exclude_prefixes":["Client/Saved/Resources/"]}}}
        """;

    private static void EnablePackages(Fixture f, string index = TaggedIndex)
    {
        f.Provider.SupportsPackageType = true;
        f.Provider.ApiConfig.PackageStatePath = "bridge/state.json";
        foreach (var path in new[] { "/Common-index", "/HD-index", "/common-patch", "/hd-patch" })
            f.Handler.Indexes[path] = index;
    }

    [DataTestMethod]
    [DataRow(GamePackageType.Full, 4, 60L)]
    [DataRow(GamePackageType.Minimal, 2, 20L)]
    public async Task PackageChoiceFiltersInstallAndVerification(GamePackageType type, int count, long size)
    {
        using var f = await Fixture.Create();
        EnablePackages(f);
        var parameter = new GameResourceParameter { BundleName = "HD", PackageType = type };
        var plan = await f.Provider.GetInstallGameResourceAsync(parameter);
        Assert.AreEqual(count, plan.DefaultResource.Count);
        Assert.AreEqual(size, plan.DownloadSize);
        Assert.AreEqual(size, plan.TargetSize);
        Assert.IsTrue(plan.SyncPackageTypeToGame);
        var verification = await f.Provider.GetVerificationResourceAsync("2", parameter);
        Assert.AreEqual(count, verification.DefaultResource.Count);
        Assert.IsNull(await f.Local.GetConfigAsync(GameLocalSettingName.GetPackageTypeKey("HD")));
        Assert.IsFalse(File.Exists(Path.Combine(f.Game, "bridge/state.json")));
    }

    [TestMethod]
    public async Task BundleListLoadsMinimalSizesOnceAndSwitchesWithoutNewRequests()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        var settings = await File.ReadAllTextAsync(f.Local.SettingPath);
        var items = new[]
        {
            new BunleResourceSize(300) { BunleName = "HD" },
            new BunleResourceSize(150) { BunleName = "SD" }
        };
        await f.Provider.LoadPackageSizesAsync(items);
        Assert.AreEqual(20L, items[0].MinimalSize);
        Assert.AreEqual(20L, items[1].MinimalSize);
        Assert.AreEqual(4, f.Handler.Requests.Count);
        Assert.AreEqual(1, f.Handler.Requests.Count(x => x.AbsolutePath == "/Common-index"));
        foreach (var item in items)
            item.PackageType = GamePackageType.Minimal;
        Assert.AreEqual(4, f.Handler.Requests.Count);
        Assert.AreEqual(settings, await File.ReadAllTextAsync(f.Local.SettingPath));
    }

    [TestMethod]
    public async Task ReadingLocalSelectionDoesNotWaitForRemoteConfiguration()
    {
        using var f = await Fixture.Create();
        await f.Local.SaveConfigAsync(GameLocalSettingName.BunleName, "HD");
        f.Handler.FailFirstCdn = true;
        var parameter = await f.Provider.GetLocalGameReosurceParamter();
        Assert.AreEqual("HD", parameter.BundleName);
        Assert.AreEqual(0, f.Handler.Requests.Count);
    }

    [DataTestMethod]
    [DataRow(GamePackageType.Full, 4)]
    [DataRow(GamePackageType.Minimal, 2)]
    public async Task ManualImportVerificationUsesExplicitChoiceRatherThanExistingState(GamePackageType choice, int count)
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        var previous = choice == GamePackageType.Full ? GamePackageType.Minimal : GamePackageType.Full;
        await f.Provider.SavePackageTypeAsync("HD", previous);
        await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", "HD", previous);
        var state = await File.ReadAllTextAsync(Path.Combine(f.Game, "bridge/state.json"));
        var plan = await f.Provider.GetVerificationResourceAsync("2", new()
        {
            BundleName = "HD",
            PackageType = choice
        });
        Assert.AreEqual(choice, plan.PackageType);
        Assert.AreEqual(count, plan.DefaultResource.Count);
        Assert.AreEqual(previous, await f.Provider.ReadPackageTypeAsync("HD"));
        Assert.AreEqual(state, await File.ReadAllTextAsync(Path.Combine(f.Game, "bridge/state.json")));
    }

    [TestMethod]
    public async Task PackagePreferencesArePerTierAndKeepExistingSettings()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Local.SaveConfigAsync("FutureSetting", "keep-me");
        await f.Provider.SavePackageTypeAsync("HD", GamePackageType.Minimal);
        Assert.AreEqual(GamePackageType.Minimal, await f.Provider.ReadPackageTypeAsync("HD"));
        Assert.AreEqual(GamePackageType.Full, await f.Provider.ReadPackageTypeAsync("UHD"));
        Assert.AreEqual("keep-me", await f.Local.GetConfigAsync("FutureSetting"));
        Assert.AreEqual("1", await f.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
        var summary = await f.Provider.GetResourceSummaryAsync(new() { BundleName = "HD" });
        Assert.AreEqual(1, f.Handler.Requests.Count);
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(2, plan.DefaultResource.Count);
        Assert.IsTrue(plan.SyncPackageTypeToGame);
    }

    [TestMethod]
    public async Task MissingCapabilityKeepsOldBundleProtocolEvenWithMinimalParameter()
    {
        using var f = await Fixture.Create();
        f.Handler.Indexes["/Common-index"] = TaggedIndex;
        var plan = await f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD", PackageType = GamePackageType.Minimal });
        Assert.AreEqual(3, plan.DefaultResource.Count);
        Assert.IsNull(plan.PackageType);
        Assert.IsFalse(plan.SyncPackageTypeToGame);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UpdateWithEqualPackageDownloadSizeDoesNotSyncGameState(bool minimal)
    {
        using var f = await Fixture.Create();
        EnablePackages(f, """{"resource":[{"dest":"core.bin","size":10}],"tagGroups":{"min":{"prefixes":[""]}}}""");
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD", PackageType = minimal ? GamePackageType.Minimal : GamePackageType.Full });
        Assert.IsFalse(plan.SyncPackageTypeToGame);
    }

    [DataTestMethod]
    [DataRow("missing", false)]
    [DataRow("source", true)]
    [DataRow("cached-diff", true)]
    [DataRow("cached-target", false)]
    [DataRow("wrong-source-size", false)]
    public async Task PackageGroupFallbackHonorsCacheAndSourcePriority(string scenario, bool diff)
    {
        using var f = await Fixture.Create();
        EnablePackages(f, """
            {"resource":[{"dest":"delta.krpdiff","size":3}],
            "groupInfos":[{"dest":"delta.krpdiff","srcFiles":[{"dest":"old.bin","size":2}],"dstFiles":[{"dest":"new.bin","size":7}]}],
            "tagGroups":{"min":{"exclude_prefixes":["story/"]}}}
            """);
        f.Handler.Indexes["/Common-index"] = PlainIndex;
        f.Handler.Indexes["/HD-index"] = PlainIndex;
        var cache = Path.Combine(f.Game, "custom-cache");
        async Task Save(string path, int length)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, new byte[length]);
        }
        if (scenario == "source") await Save(Path.Combine(f.Game, "old.bin"), 2);
        if (scenario == "wrong-source-size") await Save(Path.Combine(f.Game, "old.bin"), 1);
        if (scenario == "cached-diff") await Save(Path.Combine(cache, "patchGroup/delta.krpdiff"), 3);
        if (scenario == "cached-target")
        {
            await Save(Path.Combine(cache, "resources/new.bin"), 7);
            await Save(Path.Combine(cache, "patchGroup/delta.krpdiff"), 3);
        }
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD", PackageType = GamePackageType.Minimal, DownloadFolder = cache });
        Assert.AreEqual(diff ? 2 : 0, plan.PatchResources.Count);
        Assert.AreEqual(diff ? 0 : 2, plan.DefaultResource.Count);
        Assert.AreEqual(diff ? 6L : 14L, plan.DownloadSize);
        if (!diff) Assert.AreEqual("https://one.example/full/common/new.bin", plan.DefaultResource[0].Url);
    }

    [TestMethod]
    public void TagFilterKeepsWholeCarriersAndFiltersDeleteList()
    {
        const string text = """
            {"resource":[{"dest":"mixed.krzip","size":10},{"dest":"story.krdiff","size":20},{"dest":"empty.krzip","size":1},{"dest":"/CLIENT\\Saved/Resources/story.bin","size":1}],
             "groupResource":[{"dest":"group.krpdiff","size":30}],"applyTypes":["group"],
             "zipInfos":[{"dest":"mixed.krzip","entries":[{"dest":"core.bin"},{"dest":"Client/Saved/Resources/story.bin"}]},{"dest":"empty.krzip"}],
             "patchInfos":[{"dest":"story.krdiff","entries":[{"dest":"Client/Saved/Resources/story.bin"}]}],
             "groupInfos":[{"dest":"group.krpdiff","dstFiles":[{"dest":"core.bin"},{"dest":"Client/Saved/Resources/story.bin"}]}],
             "deleteFiles":["old.bin","Client/Saved/Resources/old-story.bin"],
             "tagGroups":{"min":{"exclude_prefixes":["Client/Saved/Resources/"],"exact":["Client/Saved/Resources/story.bin"]}}}
            """;
        var original = JsonSerializer.Deserialize(text, PathIndexGameResourceContext.Default.PatchIndexGameResource)!;
        var filtered = BundleIndexFilter.Apply(original, GamePackageType.Minimal);
        Assert.AreEqual(2, filtered.Resource.Count);
        Assert.AreEqual(2, filtered.ZipFileInfos.Count);
        Assert.AreEqual(2, filtered.ZipFileInfos[0].Entries.Count);
        Assert.AreEqual(0, filtered.PatchInfos.Count);
        Assert.AreEqual(1, filtered.GroupInfos.Count);
        Assert.AreEqual(2, filtered.GroupInfos[0].DstFiles.Count);
        CollectionAssert.AreEqual(new[] { "old.bin" }, filtered.DeleteFiles);
        Assert.AreEqual(4, original.Resource.Count);
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"other\":{\"prefixes\":[\"core\"]}}")]
    public void UndeclaredMinimalTagUsesFullIndex(string tags)
    {
        var text = "{\"resource\":[{\"dest\":\"core.bin\"}],\"tagGroups\":" + tags + "}";
        var index = JsonSerializer.Deserialize(text, PathIndexGameResourceContext.Default.PatchIndexGameResource)!;
        Assert.AreSame(index, BundleIndexFilter.Apply(index, GamePackageType.Minimal));
    }

    [TestMethod]
    public void TagRulesMatchNormalizedPrefixesAndExactPathsCaseInsensitively()
    {
        var index = new PatchIndexGameResource
        {
            Resource = [new() { Dest = "/CORE\\one.bin" }, new() { Dest = "Exact.bin" }, new() { Dest = "extra.bin" }],
            TagGroups = new() { ["MIN"] = new() { Prefixes = ["core/"], Exact = ["/EXACT.BIN"] } }
        };
        var filtered = BundleIndexFilter.Apply(index, GamePackageType.Minimal);
        Assert.AreEqual(2, filtered.Resource.Count);
        Assert.AreEqual("Exact.bin", filtered.Resource[1].Dest);
    }

    [TestMethod]
    public async Task LaunchUsesGameStateRatherThanLauncherPreferenceAndPreservesOtherTiers()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Local.SaveConfigAsync(GameLocalSettingName.BunleName, "HD");
        await f.Provider.SavePackageTypeAsync("HD", GamePackageType.Full);
        Assert.AreEqual("min", await f.Provider.ResolveLaunchPackageTypeAsync(f.Game));
        await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", "UHD", GamePackageType.Full);
        await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", "HD", GamePackageType.Full);
        await f.Provider.SavePackageTypeAsync("HD", GamePackageType.Minimal);
        Assert.AreEqual("max", await f.Provider.ResolveLaunchPackageTypeAsync(f.Game));
        Assert.AreEqual(GamePackageType.Full, await GamePackageStateService.ReadAsync(f.Game, "bridge/state.json", "UHD"));
        await File.WriteAllTextAsync(Path.Combine(f.Game, "bridge/state.json"), "broken-json");
        Assert.AreEqual("min", await f.Provider.ResolveLaunchPackageTypeAsync(f.Game));
    }

    [TestMethod]
    public async Task PredownloadInstallUsesRecordedChoiceAndLegacyCacheMeansFull()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Provider.SavePackageTypeAsync("HD", GamePackageType.Minimal);
        var legacy = await f.Provider.ReadPredownloadParameterAsync(new() { BundleName = "HD", PackageType = GamePackageType.Minimal });
        Assert.AreEqual(GamePackageType.Full, legacy.PackageType);
        await f.Local.SaveConfigsAsync(new Dictionary<string, string>
        {
            [GameLocalSettingName.ProdDownloadBundleName] = "HD",
            [GameLocalSettingName.ProdDownloadPackageType] = "min",
            [GameLocalSettingName.ProdDownloadPath] = Path.Combine(f.Game, "old-cache")
        });
        var recorded = await f.Provider.ReadPredownloadParameterAsync(new() { BundleName = "SD", PackageType = GamePackageType.Full });
        Assert.AreEqual("HD", recorded.BundleName);
        Assert.AreEqual(GamePackageType.Minimal, recorded.PackageType);
        Assert.AreEqual(Path.Combine(f.Game, "old-cache"), recorded.DownloadFolder);
    }

    [TestMethod]
    public async Task CompletionSyncKeepsUnrelatedSettingsAndGameFields()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Local.SaveConfigAsync("UnknownPreviewKey", "keep");
        Directory.CreateDirectory(Path.Combine(f.Game, "bridge"));
        await File.WriteAllTextAsync(Path.Combine(f.Game, "bridge/state.json"), """{"schemaVersion":1,"futureField":42,"tiers":{"UHD":{"packageType":"max"}}}""");
        var plan = await f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD", PackageType = GamePackageType.Minimal });
        f.Provider.ApiConfig.GameExeName = "core.bin";
        var logger = new LoggerService();
        logger.InitLogger(Path.Combine(f.Game, "test.log"), Serilog.RollingInterval.Infinite);
        var writer = new WriteGameResourceConfig(f.Local, plan.NewGameVersion, f.Provider.ApiConfig, logger);
        await writer.WriteDownloadAndUpDateResultAsync(new(), plan: plan);
        Assert.AreEqual("keep", await f.Local.GetConfigAsync("UnknownPreviewKey"));
        Assert.AreEqual("min", await f.Local.GetConfigAsync(GameLocalSettingName.GetPackageTypeKey("HD")));
        Assert.AreEqual(GamePackageType.Minimal, await GamePackageStateService.ReadAsync(f.Game, "bridge/state.json", "HD"));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(f.Game, "bridge/state.json")));
        Assert.AreEqual(42, json.RootElement.GetProperty("futureField").GetInt32());
        Assert.AreEqual("max", json.RootElement.GetProperty("tiers").GetProperty("UHD").GetProperty("packageType").GetString());
    }
}
