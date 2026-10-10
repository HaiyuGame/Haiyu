using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Models.CoreApi;
using Waves.Core.Models.Options;
using Waves.Core.Services.GameResourceProvider;

namespace Project.Test;

[TestClass]
public sealed class BundleResourceProviderTests
{
    private const string PlainIndex = """
        {"resource":[{"dest":"game.bin","size":10,"md5":"file-hash","chunkInfos":[{"start":0,"end":9,"md5":"chunk-hash"}]}]}
        """;
    private const string MixedIndex = """
        {"resource":[{"dest":"direct.bin","size":10,"md5":"a","fromFolder":"/shared/"},{"dest":"a.krzip","size":20,"md5":"z"},{"dest":"a.krdiff","size":30,"md5":"p"}],"groupResource":[{"dest":"a.krpdiff","size":40,"md5":"g"}],"applyTypes":["group"],"zipInfos":[{"dest":"a.krzip","entries":[{"dest":"out.bin","size":50,"md5":"out"}]}],"patchInfos":[{"dest":"a.krdiff","entries":[{"dest":"patched.bin","size":60,"md5":"patched"}]}],"groupInfos":[{"dest":"a.krpdiff","srcFiles":[{"dest":"old.bin","size":60,"md5":"old"}],"dstFiles":[{"dest":"new.bin","size":70,"md5":"new"}]}],"deleteFiles":["obsolete.bin"]}
        """;

    [TestMethod]
    public async Task DeleteOnePackReportsProgressAndClearsOnlyItsVersion()
    {
        using var f = await Fixture.Create();
        f.Handler.Indexes["/HD-full-index"] = "{\"resource\":[{\"dest\":\"hd.bin\",\"size\":1},{\"dest\":\"missing.bin\",\"size\":1}]}";
        // 使用 fixture 的实际索引地址。
        f.Handler.Config.ResourcePacks["HD"].IndexFile = "/HD-full-index";
        await f.Local.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, f.Game);
        var hd = Path.Combine(f.Game, "hd.bin");
        var common = Path.Combine(f.Game, "common.bin");
        await File.WriteAllTextAsync(hd, "hd");
        await File.WriteAllTextAsync(common, "common");
        var values = new List<double>();
        Assert.AreEqual(1, await f.Provider.DeleteResourcePackFilesAsync("hd", new InlineProgress(values)));
        Assert.IsFalse(File.Exists(hd));
        Assert.IsTrue(File.Exists(common));
        Assert.AreEqual(0d, values.First());
        Assert.AreEqual(100d, values.Last());
        Assert.AreEqual("", await f.Local.GetConfigAsync(GameLocalSettingName.BunlePackVersion_HD));
        Assert.AreEqual("1", await f.Local.GetConfigAsync(GameLocalSettingName.BunlePackVersion_Common));
        Assert.AreEqual(2, f.Handler.Requests.Count);
    }

    private sealed class InlineProgress(List<double> values) : IProgress<double>
    {
        public void Report(double value) => values.Add(value);
    }

    [TestMethod]
    public async Task SummaryReadsOnlyConfigAndSumsSelectedPacks()
    {
        using var f = await Fixture.Create();
        var summary = await f.Provider.GetResourceSummaryAsync(new() { BundleName = "HD" });
        Assert.AreEqual("2", summary.OfficialVersion);
        Assert.AreEqual(300L, summary.Install.DownloadSize);
        Assert.AreEqual(900L, summary.Install.TargetSize);
        Assert.AreEqual(30L, summary.Update.DownloadSize);
        Assert.AreEqual(2, summary.HistoricalVersions.Count);
        Assert.AreEqual(1, f.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task InstallIncludesSelectedPacksOnlyAndKeepsIndependentCacheIdentities()
    {
        using var f = await Fixture.Create();
        var plan = await f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual("2", plan.ResourceVersion);
        Assert.AreEqual("HD", plan.BundleName);
        Assert.AreEqual(2, plan.ResourcePacks.Count);
        Assert.AreEqual(2, plan.DefaultResource.Count);
        Assert.AreEqual("https://one.example/full/common/game.bin", plan.DefaultResource[0].Url);
        Assert.AreEqual("Common/2/game.bin", plan.DefaultResource[0].CacheRelativePath);
        Assert.AreEqual("HD/7/game.bin", plan.DefaultResource[1].CacheRelativePath);
        Assert.AreEqual("game.bin", plan.DefaultResource[1].Dest);
        Assert.AreEqual("chunk-hash", plan.DefaultResource[0].Chunks.Single().Hash);
        Assert.AreEqual(2, plan.DefaultResource[0].UrlCandidates.Count);
        Assert.IsFalse(f.Handler.Requests.Any(x => x.AbsolutePath.Contains("SD")));
    }

    [TestMethod]
    public async Task LocalVersionsAreMatchedPerPackNotPrimaryVersion()
    {
        using var f = await Fixture.Create();
        f.Record("Common", "2");
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual("2", plan.OldGameVersion);
        Assert.AreEqual("2", plan.NewGameVersion);
        Assert.AreEqual(GameResourceAvailability.Ready, plan.Availability);
        Assert.AreEqual(1, plan.DefaultResource.Count);
        Assert.AreEqual("HD", plan.DefaultResource[0].ResourcePackName);
        Assert.AreEqual("https://one.example/patch/hd/game.bin", plan.DefaultResource[0].Url);
        Assert.IsTrue(await f.Provider.CheckUpdateAsync(new() { BundleName = "HD" }));
    }

    [TestMethod]
    public async Task UnmatchedPatchUsesOfficialFullPackIndexWithoutChangingLocalVersion()
    {
        using var f = await Fixture.Create();
        f.Record("HD", "5");
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(GameResourceAvailability.Ready, plan.Availability);
        Assert.AreEqual(GameResourcePackKind.Origin, plan.ResourcePacks[1].Kind);
        Assert.AreEqual("/HD-index", f.Handler.Requests.Last().AbsolutePath);
        Assert.AreEqual(2, plan.DefaultResource.Count);
        Assert.AreEqual("1", await f.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
    }

    [TestMethod]
    public async Task MissingNewPackUsesFullIndexAndNeverCopiesMainVersionToIt()
    {
        using var f = await Fixture.Create();
        f.Record("Common", "2");
        f.RemoveRecord("HD");
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(GameResourcePackKind.Origin, plan.ResourcePacks[1].Kind);
        Assert.AreEqual("", plan.ResourcePacks[1].LocalVersion);
        Assert.AreEqual("/HD-index", f.Handler.Requests.Last().AbsolutePath);
        Assert.AreEqual(200L, plan.DownloadSize);
    }

    [TestMethod]
    public async Task AllCurrentOrNewerPacksSkipIndexesAndPreventDowngrade()
    {
        using var f = await Fixture.Create();
        f.Record("Common", "2.0.0"); f.Record("HD", "8");
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(GameResourceAvailability.AlreadyCurrent, plan.Availability);
        Assert.IsFalse(await f.Provider.CheckUpdateAsync(new() { BundleName = "HD" }));
        Assert.AreEqual(2, f.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task PredownloadUsesIndependentBundlesPacksAndCdn()
    {
        using var f = await Fixture.Create();
        f.EnablePredownload();
        var plan = await f.Provider.GetGameProdownloadResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual("3", plan.NewGameVersion);
        Assert.AreEqual("8", plan.ResourcePacks[1].TargetVersion);
        Assert.AreEqual("https://pre.example/next-patch/common/game.bin", plan.DefaultResource[0].Url);
        Assert.IsTrue(f.Handler.Requests.Skip(1).All(x => x.Host == "pre.example"));
        Assert.AreEqual("1", await f.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
    }

    [TestMethod]
    public async Task PredownloadFallsBackToOfficialCdnWhenItsListIsEmpty()
    {
        using var f = await Fixture.Create(); f.EnablePredownload();
        f.Handler.Config.Predownload!.CdnList.Clear();
        var plan = await f.Provider.GetGameProdownloadResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual("one.example", new Uri(plan.DefaultResource[0].Url).Host);
        Assert.AreEqual(2, plan.CdnCandidates.Count);
    }

    [TestMethod]
    public async Task PredownloadSwitchAndVersionGateDoNotRequestIndexes()
    {
        using var f = await Fixture.Create(); f.EnablePredownload();
        f.Handler.Config.Config.PredownloadSwitch = 0;
        Assert.AreEqual(GameResourceAvailability.NoPredownload, (await f.Provider.GetGameProdownloadResourceAsync(new() { BundleName = "HD" })).Availability);
        f.Handler.Config.Config.PredownloadSwitch = 1;
        f.Handler.Config.Predownload!.ResourcePacks["Common"].Version = "2";
        f.Handler.Config.Predownload.ResourcePacks["HD"].Version = "7";
        Assert.AreEqual(GameResourceAvailability.NoPredownload, (await f.Provider.GetGameProdownloadResourceAsync(new() { BundleName = "HD" })).Availability);
        Assert.AreEqual(2, f.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task PredownloadUnmatchedPackAlsoUsesFullIndex()
    {
        using var f = await Fixture.Create(); f.EnablePredownload(); f.Record("HD", "5");
        var plan = await f.Provider.GetGameProdownloadResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(GameResourceAvailability.Ready, plan.Availability);
        Assert.AreEqual("/next-HD-index", f.Handler.Requests.Last().AbsolutePath);
        Assert.AreEqual(GameResourcePackKind.Origin, plan.ResourcePacks[1].Kind);
    }

    [TestMethod]
    public async Task BundleConfigurationCanOverridePredownloadSwitch()
    {
        using var f = await Fixture.Create(); f.EnablePredownload();
        f.Handler.Config.Config.PredownloadSwitch = 0;
        f.Handler.Config.Bundles["HD"].Config.PredownloadSwitch = 1;
        Assert.IsTrue((await f.Provider.GetResourceSummaryAsync(new() { BundleName = "HD" })).PredownloadEnabled);
    }

    [TestMethod]
    public async Task VerificationUsesFullTargetIndexesNotZipOrDelta()
    {
        using var f = await Fixture.Create(); f.EnablePredownload();
        f.Handler.Config.Predownload!.ResourcePacks["Common"].ZipConfig = Patch("", "/zip-index", "/zip", "assets", 40);
        var plan = await f.Provider.GetVerificationResourceAsync("3", new() { BundleName = "HD" });
        Assert.AreEqual("/next-Common-index", f.Handler.Requests[1].AbsolutePath);
        Assert.AreEqual("https://pre.example/next/common/game.bin", plan.DefaultResource[0].Url);
        Assert.AreEqual(0, plan.ZipResources.Count);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Provider.GetVerificationResourceAsync("99", new() { BundleName = "HD" }));
    }

    [TestMethod]
    public async Task OriginZipConfigIsPreferredForNewInstallationButNotVerification()
    {
        using var f = await Fixture.Create();
        f.Handler.Config.ResourcePacks["Common"].ZipConfig = Patch("", "/zip-index", "/compressed", "zip-folder", 40);
        f.Handler.Indexes["/zip-index"] = MixedIndex;
        var plan = await f.Provider.GetInstallGameResourceAsync();
        Assert.AreEqual(GameResourcePackKind.Zip, plan.ResourcePacks[0].Kind);
        Assert.AreEqual(40L, plan.DownloadSize);
        Assert.AreEqual("https://one.example/compressed/zip-folder/a.krzip", plan.ZipResources[0].Url);
        Assert.AreEqual("https://one.example/full/common/out.bin", plan.ZipResources[0].Entries.Single().Url);
        Assert.AreEqual("https://one.example/shared/direct.bin", plan.DefaultResource.Single().Url);
    }

    [TestMethod]
    public async Task GroupSelectionPreservesNormalDescriptorsAndMapsSrcDst()
    {
        using var f = await Fixture.Create(); f.Provider.ApplyMethod = "group";
        f.Handler.Indexes["/common-patch"] = MixedIndex;
        var plan = await f.Provider.GetUpdateGameResourceAsync();
        Assert.AreEqual(2, plan.PatchResources.Count);
        Assert.IsFalse(plan.PatchResources.Single(x => !x.IsGroup).IsSelected);
        var group = plan.PatchResources.Single(x => x.IsGroup);
        Assert.IsTrue(group.IsSelected);
        Assert.AreEqual("old.bin", group.SrcFiles.Single().Dest);
        Assert.AreEqual("new.bin", group.DstFiles.Single().Dest);
        Assert.AreEqual("https://one.example/full/common/new.bin", group.DstFiles.Single().Url);
        Assert.AreEqual("obsolete.bin", plan.DeleteFiles.Single());
    }

    [TestMethod]
    public async Task ApplyEvaluationControlsSummaryAndIndexCarrierSelection()
    {
        using var f = await Fixture.Create();
        var patch = f.Handler.Config.ResourcePacks["Common"].PatchConfig[0];
        patch.Ext = new BundlePatchExtension { ApplyEvaluations = [new() { Name = "group", Size = 55, RequiredDiskSpace = 66, DeltaSize = 7, MaxFileSize = 8 }] };
        f.Handler.Config.Config.Experiment.Apply.ApplyMethodFeature = "group";
        var summary = await f.Provider.GetResourceSummaryAsync();
        Assert.AreEqual(55L, summary.Update.DownloadSize);
        Assert.AreEqual(121L, summary.Update.RequiredSpace);
        var plan = await f.Provider.GetUpdateGameResourceAsync();
        Assert.AreEqual(7L, plan.ResourcePacks[0].DeltaSize);
    }

    [TestMethod]
    public async Task VerificationExpandsCarrierTargetsAndDropsDeleteInstructions()
    {
        using var f = await Fixture.Create(); f.Handler.Indexes["/Common-index"] = MixedIndex;
        var plan = await f.Provider.GetVerificationResourceAsync("2");
        CollectionAssert.AreEquivalent(new[] { "direct.bin", "out.bin", "patched.bin" }, plan.DefaultResource.Select(x => x.Dest).ToArray());
        Assert.AreEqual(0, plan.PatchResources.Count);
        Assert.AreEqual(0, plan.ZipResources.Count);
        Assert.AreEqual(0, plan.DeleteFiles.Count);
    }

    [TestMethod]
    public async Task SameCarrierNamesInDifferentPacksAreNotMerged()
    {
        using var f = await Fixture.Create();
        f.Handler.Indexes["/Common-index"] = MixedIndex; f.Handler.Indexes["/HD-index"] = MixedIndex;
        var plan = await f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(2, plan.ZipResources.Count);
        Assert.AreEqual(2, plan.ZipResources.Select(x => x.CacheRelativePath).Distinct().Count());
    }

    [TestMethod]
    public async Task BadIndexHashRetriesOtherCdn()
    {
        using var f = await Fixture.Create();
        f.Handler.Config.ResourcePacks["Common"].IndexFileMd5 = Hash(PlainIndex);
        f.Handler.CorruptFirstCdn = true;
        var plan = await f.Provider.GetInstallGameResourceAsync();
        Assert.AreEqual(1, plan.DefaultResource.Count);
        CollectionAssert.AreEqual(new[] { "config.example", "one.example", "two.example" }, f.Handler.Requests.Select(x => x.Host).ToArray());
        Assert.AreEqual(Hash(PlainIndex), plan.ResourcePacks[0].IndexHash);
    }

    [TestMethod]
    public async Task ExhaustedIndexHashValidationNeverReturnsSuccessfulManifest()
    {
        using var f = await Fixture.Create();
        f.Handler.Config.ResourcePacks["Common"].IndexFileMd5 = new string('0', 32);
        await Assert.ThrowsExceptionAsync<IOException>(() => f.Provider.GetInstallGameResourceAsync());
        Assert.AreEqual("1", await f.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
    }

    [TestMethod]
    public async Task UnknownBundleAndInvalidPackReferencesFailBeforeIndexes()
    {
        using var f = await Fixture.Create();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Provider.GetInstallGameResourceAsync(new() { BundleName = "missing" }));
        f.Handler.Config.Bundles["HD"].ResourcePacks.Add("missing");
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD" }));
        Assert.AreEqual(2, f.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task PackVersionKeysUseCanonicalNames()
    {
        Assert.AreEqual("BunlePackVersion_Common", GameLocalSettingName.GetBunlePackVersionKey("common"));
        Assert.AreEqual("BunlePackVersion_HD", GameLocalSettingName.GetBunlePackVersionKey("hd"));
        Assert.AreEqual("BunlePackVersion_SD", GameLocalSettingName.GetBunlePackVersionKey("sd"));
        Assert.AreEqual("BunlePackVersion_UHD", GameLocalSettingName.GetBunlePackVersionKey("uhd"));
        using var f = await Fixture.Create();
        Assert.AreEqual("1", await f.Local.GetConfigAsync("BunlePackVersion_Common"));
        Assert.AreEqual("6", await f.Local.GetConfigAsync("BunlePackVersion_HD"));
    }

    [TestMethod]
    public async Task ProviderUsesCurrentInjectedClientRatherThanDisposedInstance()
    {
        using var f = await Fixture.Create();
        var service = new ClientService(f.Handler);
        var provider = new BundleGameResourceProvider(service);
        provider.SetConfig(f.Local, new KuroGameApiConfig { ConfigUrl = "https://old.example/legacy", BunleConfigUrl = "https://config.example/config" });
        service.BuildClient();
        Assert.AreEqual("2", (await provider.GetResourceSummaryAsync()).OfficialVersion);
        service.HttpClient.Dispose();
    }

    [TestMethod]
    public async Task CancellationPropagatesWithoutIndexRequests()
    {
        using var f = await Fixture.Create();
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => f.Provider.GetInstallGameResourceAsync(token: cts.Token));
        Assert.AreEqual(0, f.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task IndexTraversalIsRejected()
    {
        using var f = await Fixture.Create();
        f.Handler.Indexes["/Common-index"] = "{\"resource\":[{\"dest\":\"../outside.bin\",\"size\":10}]}";
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => f.Provider.GetInstallGameResourceAsync());
    }

    [TestMethod]
    public void ConfigAndPackManifestUseSourceGeneratedJsonOnly()
    {
        Assert.IsFalse(JsonSerializer.IsReflectionEnabledByDefault);
        var config = Config(); config.ResourcePacks["Common"].ZipConfig = Patch("", "/zip", "/compressed", "folder", 5);
        var json = JsonSerializer.Serialize(config, GameLauncherBunleSourceContext.Default.GameLauncherBunleSource);
        var decoded = JsonSerializer.Deserialize(json, GameLauncherBunleSourceContext.Default.GameLauncherBunleSource)!;
        Assert.AreEqual("folder", decoded.ResourcePacks["Common"].ZipConfig!.Folder);
        var manifest = new GameVersionInfo { BundleName = "HD", ResourcePacks = [new() { Name = "Common", TargetVersion = "2" }],
            DefaultResource = [new() { ResourcePackName = "Common", CacheRelativePath = "Common/2/game.bin" }] };
        var roundtrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(manifest, GameResourceJsonContext.Default.GameVersionInfo), GameResourceJsonContext.Default.GameVersionInfo)!;
        Assert.AreEqual("Common", roundtrip.ResourcePacks.Single().Name);
        Assert.AreEqual("Common/2/game.bin", roundtrip.DefaultResource.Single().CacheRelativePath);
    }

    [TestMethod]
    public async Task PreviouslySuppliedOfficialJsonMapsActualHdAndSdBundles()
    {
        using var f = await Fixture.Create();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OfficialBundleConfig.json"));
        f.Handler.Config = JsonSerializer.Deserialize(bytes, GameLauncherBunleSourceContext.Default.GameLauncherBunleSource)!;
        var summary = await f.Provider.GetResourceSummaryAsync(new() { BundleName = "HD" });
        Assert.AreEqual("3.7.0", summary.OfficialVersion);
        Assert.AreEqual(f.Handler.Config.ResourcePacks["common"].Size + f.Handler.Config.ResourcePacks["hd"].Size,
            summary.Install.DownloadSize);
        Assert.AreEqual(1, f.Handler.Requests.Count);
        var sd = await f.Provider.GetResourceSummaryAsync(new() { BundleName = "SD" });
        Assert.AreEqual(f.Handler.Config.ResourcePacks["common"].Size + f.Handler.Config.ResourcePacks["sd"].Size,
            sd.Install.DownloadSize);
        Assert.AreEqual(2, f.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task MultipleProfilesWithoutDefaultRequireExplicitSelection()
    {
        using var f = await Fixture.Create();
        f.Handler.Config.Bundles.Remove("default");
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Provider.GetInstallGameResourceAsync());
        f.Provider.CurrentBundleName = "SD";
        var plan = await f.Provider.GetInstallGameResourceAsync();
        Assert.AreEqual("SD", plan.BundleName);
        Assert.AreEqual("SD", plan.ResourcePacks[1].Name);
    }

    [TestMethod]
    public async Task NewPackUsesZipConfigAndMatchedPatchOverridesZipConfig()
    {
        using var f = await Fixture.Create();
        f.Handler.Config.ResourcePacks["HD"].ZipConfig = Patch("", "/hd-zip", "/compressed", "hd", 80);
        var patch = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(GameResourcePackKind.Patch, patch.ResourcePacks[1].Kind);
        Assert.AreEqual("/hd-patch", f.Handler.Requests.Last().AbsolutePath);
        f.RemoveRecord("HD");
        var origin = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.AreEqual(GameResourcePackKind.Zip, origin.ResourcePacks[1].Kind);
        Assert.AreEqual("/hd-zip", f.Handler.Requests.Last().AbsolutePath);
    }

    [TestMethod]
    public async Task HttpFailureAndInvalidJsonRetryOtherCdn()
    {
        using var f = await Fixture.Create();
        f.Handler.FailFirstCdn = true;
        Assert.AreEqual(1, (await f.Provider.GetInstallGameResourceAsync()).DefaultResource.Count);
        f.Handler.FailFirstCdn = false; f.Handler.CorruptFirstCdn = true;
        Assert.AreEqual(1, (await f.Provider.GetInstallGameResourceAsync()).DefaultResource.Count);
        Assert.AreEqual(2, f.Handler.Requests.Count(x => x.Host == "two.example"));
    }
    [TestMethod]
    public async Task SamePrimaryVersionRequiresPackSnapshotForUnambiguousVerification()
    {
        using var f = await Fixture.Create(); f.EnablePredownload();
        f.Handler.Config.Predownload!.ResourcePacks["Common"].Version = "2";
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Provider.GetVerificationResourceAsync("2", new() { BundleName = "HD" }));
        var plan = await f.Provider.GetVerificationResourceAsync("2", new()
        {
            BundleName = "HD", TargetPackVersions = new Dictionary<string, string> { ["Common"] = "2", ["HD"] = "8" }
        });
        Assert.AreEqual("8", plan.ResourcePacks[1].TargetVersion);
        Assert.AreEqual("pre.example", new Uri(plan.DefaultResource[0].Url).Host);
    }

    [TestMethod]
    public async Task ChangedTargetPackSnapshotFailsBeforeIndexRequests()
    {
        using var f = await Fixture.Create(); f.EnablePredownload();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Provider.GetVerificationResourceAsync("3", new()
        {
            BundleName = "HD", TargetPackVersions = new Dictionary<string, string> { ["Common"] = "3", ["HD"] = "99" }
        }));
        Assert.AreEqual(1, f.Handler.Requests.Count);
    }
    [TestMethod]
    public async Task SummaryDisplayAndAvailabilityUsePackVersionsNotLegacyLocalVersion()
    {
        using var f = await Fixture.Create();
        await f.Local.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, "1");
        f.Record("Common", "2"); f.Record("HD", "7");
        var summary = await f.Provider.GetResourceSummaryAsync(new() { BundleName = "HD" });
        Assert.AreEqual("2", summary.LocalVersion);
        Assert.AreEqual(GameResourceAvailability.AlreadyCurrent, summary.Update.Availability);
        f.Record("HD", "6");
        summary = await f.Provider.GetResourceSummaryAsync(new() { BundleName = "HD" });
        Assert.AreEqual("2", summary.LocalVersion);
        Assert.AreEqual(GameResourceAvailability.Ready, summary.Update.Availability);
        Assert.AreEqual("1", await f.Local.GetConfigAsync(GameLocalSettingName.LocalGameVersion));
    }
    private static string Hash(string value) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value)));
    private static BundlePatchConfig Patch(string version, string index, string baseUrl, string folder, long size) =>
        new() { Version = version, IndexFile = index, BaseUrl = baseUrl, Folder = folder, Size = size, UnCompressSize = size * 2 };
    private static BundleResourcePack Pack(string name, string version, long size) => new()
    {
        Version = version, IndexFile = $"/{name}-index", BaseUrl = "/full", Folder = name.ToLowerInvariant(),
        Size = size, UnCompressSize = size * 3,
        PatchConfig = [Patch(name == "Common" ? "1" : "6", name == "Common" ? "/common-patch" : "/hd-patch", "/patch", name.ToLowerInvariant(), size / 10),
            Patch("0.5", "/old-patch", "/old", "", 1)]
    };
    private static GameLauncherBunleSource Config() => new()
    {
        CdnList = [new() { Url = "https://one.example", P = 1, K1 = 1, K2 = 1 }, new() { Url = "https://two.example", P = 2 }],
        ResourcePacks = new() { ["Common"] = Pack("Common", "2", 100), ["HD"] = Pack("HD", "7", 200), ["SD"] = Pack("SD", "7", 50) },
        Bundles = new() { ["default"] = new() { ResourcePacks = ["Common"] }, ["HD"] = new() { ResourcePacks = ["Common", "HD"] }, ["SD"] = new() { ResourcePacks = ["Common", "SD"] } }
    };

    private sealed class Handler : HttpMessageHandler
    {
        public GameLauncherBunleSource Config = BundleResourceProviderTests.Config();
        public List<Uri> Requests = [];
        public Dictionary<string, string> Indexes = [];
        public bool CorruptFirstCdn;
        public bool FailFirstCdn;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = request.RequestUri!; Requests.Add(uri);
            if (FailFirstCdn && uri.Host == "one.example") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var text = uri.AbsolutePath == "/config" ? JsonSerializer.Serialize(Config, GameLauncherBunleSourceContext.Default.GameLauncherBunleSource)
                : CorruptFirstCdn && uri.Host == "one.example" ? "corrupt" : Indexes.GetValueOrDefault(uri.AbsolutePath, PlainIndex);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class ClientService(Handler handler) : IHttpClientService
    {
        public HttpClient HttpClient { get; private set; } = new(handler, false);
        public HttpClient GameDownloadClient => HttpClient;
        public void BuildClient() { HttpClient.Dispose(); HttpClient = new(handler, false); }
    }
    private sealed class Fixture : IDisposable
    {
        public string Game { get; } = Path.Combine(Path.GetTempPath(), "haiyu-bundle-" + Guid.NewGuid().ToString("N"));
        public Handler Handler { get; } = new();
        public GameLocalConfig Local { get; private set; } = null!;
        public BundleGameResourceProvider Provider { get; private set; } = null!;
        private HttpClient Client = null!;
        private readonly List<string> files = [];
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); Directory.CreateDirectory(f.Game);
            f.Local = new GameLocalConfig(Path.Combine(f.Game, "settings.json")); f.files.Add(f.Local.SettingPath);
            await f.Local.SaveConfigAsync(GameLocalSettingName.GameLauncherBassFolder, f.Game);
            await f.Local.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, "1");
            f.Client = new HttpClient(f.Handler);
            f.Provider = new BundleGameResourceProvider(f.Client);
            f.Provider.SetConfig(f.Local, new KuroGameApiConfig { ConfigUrl = "https://old.example/legacy", BunleConfigUrl = "https://config.example/config" });
            f.Record("Common", "1"); f.Record("HD", "6");
            return f;
        }
        public void Record(string name, string version) => Local.SaveConfigAsync(
            GameLocalSettingName.GetBunlePackVersionKey(name), version).GetAwaiter().GetResult();
        public void RemoveRecord(string name) => Record(name, "");
        public void EnablePredownload()
        {
            Handler.Config.Config.PredownloadSwitch = 1;
            Handler.Config.Predownload = new BundlePredownload
            {
                CdnList = [new() { Url = "https://pre.example", P = 1 }],
                Bundles = new() { ["HD"] = new() { ResourcePacks = ["Common", "HD"] } },
                ResourcePacks = new() { ["Common"] = Pack("Common", "3", 150), ["HD"] = Pack("HD", "8", 250) }
            };
            foreach (var (name, pack) in Handler.Config.Predownload.ResourcePacks)
            {
                pack.BaseUrl = "/next"; pack.IndexFile = $"/next-{name}-index";
                pack.PatchConfig[0].BaseUrl = "/next-patch";
                pack.PatchConfig[0].IndexFile = $"/next-{name}-patch";
            }
        }
        public void Dispose()
        {
            Client.Dispose();
            foreach (var path in files.Distinct()) if (File.Exists(path)) File.Delete(path);
        }
    }
}
