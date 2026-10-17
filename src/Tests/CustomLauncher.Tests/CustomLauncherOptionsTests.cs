using Waves.Core.Models;
using Waves.Core.Models.Options;

namespace CustomLauncher.Tests;

[TestClass]
public sealed class CustomLauncherOptionsTests
{
    private string directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        directory = Path.Combine(Path.GetTempPath(), "Haiyu launcher tests " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(directory, true);

    [TestMethod]
    public async Task ExistingConfigurationsKeepCustomLaunchingDisabled()
    {
        var config = new GameLocalConfig(Path.Combine(directory, "Settings.json"));
        await config.SaveConfigAsync(GameLocalSettingName.StartGameArguments, "-DX12");
        var options = await CustomLauncherOptions.LoadAsync(config);
        Assert.IsFalse(options.Enabled);
        Assert.AreEqual(string.Empty, options.TargetPath);
        Assert.AreEqual(string.Empty, options.Arguments);
        Assert.IsNull(options.Validate());
    }

    [TestMethod]
    public async Task SettingsPersistPerChannelWithoutReplacingGameArguments()
    {
        var path = Path.Combine(directory, "Settings.json");
        var config = new GameLocalConfig(path);
        await config.SaveConfigAsync(GameLocalSettingName.StartGameArguments, "-DX12 -RTX");
        var expected = new CustomLauncherOptions { Enabled = true, TargetPath = Path.Combine(directory, "mod.exe"), Arguments = "--profile \"my mods\"" };
        await expected.SaveAsync(config);
        var reloaded = new GameLocalConfig(path);
        var actual = await CustomLauncherOptions.LoadAsync(reloaded);
        Assert.IsTrue(actual.Enabled);
        Assert.AreEqual(expected.TargetPath, actual.TargetPath);
        Assert.AreEqual(expected.Arguments, actual.Arguments);
        Assert.AreEqual("-DX12 -RTX", await reloaded.GetConfigAsync(GameLocalSettingName.StartGameArguments));
        Assert.IsFalse((await CustomLauncherOptions.LoadAsync(new GameLocalConfig(Path.Combine(directory, "OtherChannel.json")))).Enabled);
    }

    [TestMethod]
    public async Task DisablingRetainsTheSelectedLauncherForNextTime()
    {
        var config = new GameLocalConfig(Path.Combine(directory, "Settings.json"));
        var options = new CustomLauncherOptions { Enabled = false, TargetPath = Path.Combine(directory, "mod.exe"), Arguments = "--custom" };
        await options.SaveAsync(config);
        var actual = await CustomLauncherOptions.LoadAsync(new GameLocalConfig(config.SettingPath));
        Assert.IsFalse(actual.Enabled);
        Assert.AreEqual(options.TargetPath, actual.TargetPath);
        Assert.AreEqual(options.Arguments, actual.Arguments);
    }

    [TestMethod]
    public void RejectsRelativeMissingAndNonExecutableTargets()
    {
        var script = Path.Combine(directory, "launcher.bat");
        File.WriteAllText(script, string.Empty);
        foreach (var target in new[] { string.Empty, "relative.exe", script, Path.Combine(directory, "missing.exe") })
        {
            var options = new CustomLauncherOptions { Enabled = true, TargetPath = target };
            Assert.IsNotNull(options.Validate());
            Assert.ThrowsException<InvalidOperationException>(() => options.CreateStartInfo("-DX12"));
        }
    }

    [TestMethod]
    public void PreservesQuotedArgumentsAndUsesTheLauncherDirectory()
    {
        var target = Path.Combine(directory, "mod loader (test).exe");
        File.WriteAllText(target, string.Empty);
        var options = new CustomLauncherOptions { Enabled = true, TargetPath = target, Arguments = "  --profile \"two words\"  " };
        var info = options.CreateStartInfo("  -DX12 -RTX -krqlv=uhd  ");
        Assert.AreEqual(target, info.FileName);
        Assert.AreEqual(directory, info.WorkingDirectory);
        Assert.AreEqual("-DX12 -RTX -krqlv=uhd --profile \"two words\"", info.Arguments);
        Assert.IsTrue(info.UseShellExecute);
        Assert.AreEqual(string.Empty, info.Verb);
    }

    [TestMethod]
    public void MissingTargetAtLaunchDoesNotFallBackToTheGame()
    {
        var target = Path.Combine(directory, "mod.exe");
        File.WriteAllText(target, string.Empty);
        var options = new CustomLauncherOptions { Enabled = true, TargetPath = target };
        Assert.IsNull(options.Validate());
        File.Delete(target);
        Assert.ThrowsException<InvalidOperationException>(() => options.CreateStartInfo("-DX12"));
    }

    [TestMethod]
    public void CustomArgumentsExcludeOnlyTheGameBootstrapSelector()
    {
        var options = StartGameOption.BuildWavesGameOption(true, false, "Client --profile \"my mods\"", "Wuthering Waves.exe");
        Assert.AreEqual(" -dx11 -slno Client --profile \"my mods\"", options.GetGameArguments());
        Assert.AreEqual("Client " + options.GetGameArguments(), options.ToString());
        Assert.AreEqual("Client  -DX12", StartGameOption.BuildWavesGameOption(false, false, "-DX12", "Wuthering Waves.exe").ToString());
    }

    [TestMethod]
    public void PunishArgumentsArePassedThroughWithoutStrippingUserTokens()
    {
        var options = StartGameOption.BuildPunishGameOption("Client --custom");
        Assert.AreEqual("Client --custom", options.GetGameArguments());
        Assert.AreEqual(options.ToString(), options.GetGameArguments());
    }

    [TestMethod]
    public void EmptyExtraArgumentsDoNotAddASeparator()
    {
        var target = Path.Combine(directory, "mod.exe");
        File.WriteAllText(target, string.Empty);
        var options = new CustomLauncherOptions { Enabled = true, TargetPath = target };
        Assert.AreEqual("-DX12", options.CreateStartInfo("-DX12").Arguments);
        Assert.AreEqual(string.Empty, options.CreateStartInfo(string.Empty).Arguments);
        options.Arguments = "--custom";
        Assert.AreEqual("--custom", options.CreateStartInfo(string.Empty).Arguments);
    }
}
