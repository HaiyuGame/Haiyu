using Waves.Core.Models;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Project.Test;

public sealed partial class BundleResourceProviderTests
{
    [DataTestMethod]
    [DataRow("HD", GamePackageType.Full, "-krqlv=hd -krpkg=max")]
    [DataRow("HD", GamePackageType.Minimal, "-krqlv=hd -krpkg=min")]
    [DataRow("SD", GamePackageType.Full, "-krqlv=sd -krpkg=max")]
    [DataRow("SD", GamePackageType.Minimal, "-krqlv=sd -krpkg=min")]
    [DataRow("UHD", GamePackageType.Full, "-krqlv=uhd -krpkg=max")]
    [DataRow("UHD", GamePackageType.Minimal, "-krqlv=uhd -krpkg=min")]
    public async Task LaunchBuildsTierArgumentsWithoutPersistingThem(string bundle, GamePackageType type, string expected)
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Local.SaveConfigAsync(GameLocalSettingName.BunleName, bundle);
        await f.Local.SaveConfigAsync(GameLocalSettingName.StartGameArguments, "--profile custom");
        await f.Provider.SavePackageTypeAsync(bundle, type == GamePackageType.Full ? GamePackageType.Minimal : GamePackageType.Full);
        await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", bundle, type);
        var settings = await File.ReadAllTextAsync(f.Local.SettingPath);
        var state = await File.ReadAllTextAsync(Path.Combine(f.Game, "bridge/state.json"));
        Assert.AreEqual("--profile custom " + expected,
            await f.Provider.ResolveLaunchArgumentsAsync(f.Game, "--profile custom"));
        Assert.AreEqual(settings, await File.ReadAllTextAsync(f.Local.SettingPath));
        Assert.AreEqual(state, await File.ReadAllTextAsync(Path.Combine(f.Game, "bridge/state.json")));
        Assert.AreEqual(0, f.Handler.Requests.Count);
    }

    [TestMethod]
    public async Task LaunchReplacesStaleManagedArgumentsAndPreservesQuotedUserText()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Local.SaveConfigAsync(GameLocalSettingName.BunleName, "UHD");
        var custom = @"--profile ""two words"" -KRQLV=""sd"" -krqlv=hd -KRPKG=max -krpkg=""min""";
        Assert.AreEqual("""--profile "two words" -krqlv=uhd -krpkg=min""",
            await f.Provider.ResolveLaunchArgumentsAsync(f.Game, custom));
        var quoted = @"--note=""literal -krpkg=max""";
        Assert.AreEqual(quoted + " -krqlv=uhd -krpkg=min", await f.Provider.ResolveLaunchArgumentsAsync(f.Game, quoted));
    }

    [TestMethod]
    public async Task MissingGameStateUsesBaselineWithoutWritingAFile()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Local.SaveConfigAsync(GameLocalSettingName.BunleName, "HD");
        Assert.AreEqual("-krqlv=hd -krpkg=min", await f.Provider.ResolveLaunchArgumentsAsync(f.Game, null));
        Assert.IsFalse(File.Exists(Path.Combine(f.Game, "bridge/state.json")));
    }

    [TestMethod]
    public async Task LaunchReadsLatestInstalledTierAndStateEachTime()
    {
        using var f = await Fixture.Create(); EnablePackages(f);
        await f.Local.SaveConfigAsync(GameLocalSettingName.BunleName, "HD");
        Assert.AreEqual("-krqlv=hd -krpkg=min", await f.Provider.ResolveLaunchArgumentsAsync(f.Game, ""));
        await f.Local.SaveConfigAsync(GameLocalSettingName.BunleName, "SD");
        await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", "SD", GamePackageType.Full);
        Assert.AreEqual("-krqlv=sd -krpkg=max", await f.Provider.ResolveLaunchArgumentsAsync(f.Game, ""));
    }

    [TestMethod]
    public async Task OlderBundleChannelAddsOnlyQualityArgument()
    {
        using var f = await Fixture.Create();
        f.Provider.ApiConfig.BunleDefaultBundleName = "SD";
        Assert.AreEqual("-dx11 -krqlv=sd", await f.Provider.ResolveLaunchArgumentsAsync(f.Game, "-dx11 -krpkg=max -krqlv=uhd"));
        Assert.AreEqual(0, f.Handler.Requests.Count);
    }
}
