using Waves.Core.Models.Enums;
using Waves.Core.Models.CoreApi;
using Waves.Core.Services;

namespace Project.Test;

public sealed partial class BundleResourceProviderTests
{
    [DataTestMethod]
    [DataRow("official")]
    [DataRow("bilibili")]
    [DataRow("global")]
    public async Task AllWavesChannelsSupportPackageFilteringAndLaunchState(string channel)
    {
        using var f = await Fixture.Create();
        EnablePackages(f);
        var config = channel switch
        {
            "official" => KuroGameApiConfig.MainAPiConfig,
            "bilibili" => KuroGameApiConfig.BilibiliConfig,
            _ => KuroGameApiConfig.GlobalConfig
        };
        Waves.Core.GameContext.KuroGameContextBaseV2 context = channel switch
        {
            "official" => new Waves.Core.GameContext.ContextsV2.Waves.WavesMainGameContextV2(config, "test", new PackageSwitchCircuitBreaker(), new GrayExtension.GrayOpenService()),
            "bilibili" => new Waves.Core.GameContext.ContextsV2.Waves.WavesBiliBiliGameContextV2(config, "test", new PackageSwitchCircuitBreaker(), new GrayExtension.GrayOpenService()),
            _ => new Waves.Core.GameContext.ContextsV2.Waves.WavesGlobalGameContextV2(config, "test", new PackageSwitchCircuitBreaker(), new GrayExtension.GrayOpenService())
        };
        f.Provider.SupportsPackageType = context.SupportsPackageType;
        config.BunleConfigUrl = "https://config.example/config";
        f.Provider.SetConfig(f.Local, config);
        Assert.IsTrue(f.Provider.SupportsPackageType);
        Assert.AreEqual("Client/Saved/LauncherBridge/PackageState.json", config.PackageStatePath);
        var plan = await f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD", PackageType = GamePackageType.Minimal });
        Assert.AreEqual(20L, plan.DownloadSize);
        Assert.AreEqual(GamePackageType.Minimal, plan.PackageType);
        await f.Local.SaveConfigAsync(Waves.Core.Models.GameLocalSettingName.BunleName, "HD");
        await GamePackageStateService.WriteAsync(f.Game, config.PackageStatePath!, "HD", GamePackageType.Minimal);
        Assert.AreEqual("-krqlv=hd -krpkg=min", await f.Provider.ResolveLaunchArgumentsAsync(f.Game, ""));
    }

    [TestMethod]
    public async Task ExplicitSwitchControlsFeatureIndependentlyOfStatePath()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        f.Provider.SupportsPackageType = false;
        Assert.IsFalse(f.Provider.SupportsPackageType);
        var disabled = await f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD", PackageType = GamePackageType.Minimal });
        Assert.AreEqual(4, disabled.DefaultResource.Count);
        Assert.IsNull(disabled.PackageType);
        f.Provider.SupportsPackageType = true;
        Assert.IsTrue(f.Provider.SupportsPackageType);
        var enabled = await f.Provider.GetInstallGameResourceAsync(new() { BundleName = "HD", PackageType = GamePackageType.Minimal });
        Assert.AreEqual(2, enabled.DefaultResource.Count);
        Assert.AreEqual(GamePackageType.Minimal, enabled.PackageType);
    }

    [TestMethod]
    public void PunishChannelsKeepLegacyPackageBehavior()
    {
        Waves.Core.GameContext.KuroGameContextBaseV2[] contexts =
        [
            new Waves.Core.GameContext.ContextsV2.Punish.PunishMainGameContextV2(KuroGameApiConfig.MainBGRConfig, "test", new PackageSwitchCircuitBreaker(), new GrayExtension.GrayOpenService()),
            new Waves.Core.GameContext.ContextsV2.Punish.PunishBiliBiliGameContextV2(KuroGameApiConfig.BiliBiliBGRConfig, "test", new PackageSwitchCircuitBreaker(), new GrayExtension.GrayOpenService()),
            new Waves.Core.GameContext.ContextsV2.Punish.PunishGlobalGameContextV2(KuroGameApiConfig.GlobalBGRConfig, "test", new PackageSwitchCircuitBreaker(), new GrayExtension.GrayOpenService()),
            new Waves.Core.GameContext.ContextsV2.Punish.PunishTwGameContextV2(KuroGameApiConfig.TWBGRConfig, "test", new PackageSwitchCircuitBreaker(), new GrayExtension.GrayOpenService())
        ];
        foreach (var context in contexts)
        {
            Assert.IsFalse(context.SupportsPackageType);
            Assert.IsNull(context.Config.PackageStatePath);
        }
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task ChangedGamePackageStateRequiresReselection(bool predownload, bool preferMinimal)
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        if (predownload)
        {
            f.EnablePredownload();
            foreach (var path in new[] { "/next-Common-index", "/next-HD-index", "/next-Common-patch", "/next-HD-patch" })
                f.Handler.Indexes[path] = TaggedIndex;
        }
        await f.Provider.SavePackageTypeAsync("HD", preferMinimal ? GamePackageType.Minimal : GamePackageType.Full);
        await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", "HD",
            preferMinimal ? GamePackageType.Full : GamePackageType.Minimal);
        var state = await File.ReadAllTextAsync(Path.Combine(f.Game, "bridge/state.json"));
        var plan = predownload
            ? await f.Provider.GetGameProdownloadResourceAsync(new() { BundleName = "HD" })
            : await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.IsTrue(plan.NeedsPackageTypeConfirmation);
        Assert.AreEqual(state, await File.ReadAllTextAsync(Path.Combine(f.Game, "bridge/state.json")));
    }

    [DataTestMethod]
    [DataRow("equal-download")]
    [DataRow("matching-state")]
    [DataRow("missing-preference")]
    [DataRow("missing-game-state")]
    public async Task NoResourceChangeWarningWithoutApplicableStateDifference(string scenario)
    {
        using var f = await Fixture.Create();
        EnablePackages(f, scenario == "equal-download"
            ? """{"resource":[{"dest":"core.bin","size":10}],"tagGroups":{"min":{"prefixes":[""]}}}"""
            : TaggedIndex);
        if (scenario != "missing-preference")
            await f.Provider.SavePackageTypeAsync("HD", GamePackageType.Full);
        if (scenario != "missing-game-state")
            await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", "HD",
                scenario == "matching-state" ? GamePackageType.Full : GamePackageType.Minimal);
        var plan = await f.Provider.GetUpdateGameResourceAsync(new() { BundleName = "HD" });
        Assert.IsFalse(plan.NeedsPackageTypeConfirmation);
        if (scenario == "equal-download")
            Assert.IsFalse(plan.SyncPackageTypeToGame);
    }
}
