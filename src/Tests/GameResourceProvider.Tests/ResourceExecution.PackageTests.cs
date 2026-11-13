using Waves.Core.Models;
using Waves.Core.Models.Enums;
using Waves.Core.Models.Options;
using Waves.Core.Services;
using Waves.Core.Services.GameResourceProvider;

namespace Project.Test;

public sealed partial class ResourceExecutionTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MinimalPredownloadSnapshotSurvivesPreferenceChangeAndInstallsCorrectSource(bool released)
    {
        await using var f = await Fixture.Create(packages: true);
        var parameter = new GameResourceParameter { BundleName = "HD", PackageType = GamePackageType.Minimal };
        Assert.IsTrue(await f.Context.StartProdDownloadGameResourceAsync(f.Cache, parameter));
        await f.WaitUntil(() => Task.FromResult(!f.Context.IsResourceOperationActive));
        Assert.IsTrue(await f.VersionIs("1"));
        Assert.IsFalse(File.Exists(Path.Combine(f.Game, "bridge/state.json")));
        Assert.AreEqual("min", await f.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPackageType));
        Assert.AreEqual("True", await f.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadFolderDone));
        var provider = (BundleGameResourceProvider)f.Context.GameResourceProvider;
        await provider.SavePackageTypeAsync("HD", GamePackageType.Full);
        Assert.IsFalse(await provider.MatchesPredownloadSelectionAsync("HD"));
        Assert.IsFalse(await provider.MatchesPredownloadSelectionAsync("UHD"));
        if (released)
        {
            f.Handler.OfficialVersion = "3";
            await f.Context.StartInstallGameResource(InstallOption.CreateProdownlad());
        }
        else
        {
            // 提前安装入口只允许当前正式版本已安装，缓存目标为下一版本。
            await f.Context.GameLocalConfig.SaveConfigAsync(GameLocalSettingName.LocalGameVersion, "2");
            await f.Context.AdvanceInstallGameResourceAsync();
        }
        await f.WaitUntil(() => Task.FromResult(!f.Context.IsResourceOperationActive));
        Assert.IsTrue(await f.VersionIs("3"));
        Assert.AreEqual(GamePackageType.Minimal, await GamePackageStateService.ReadAsync(f.Game, "bridge/state.json", "HD"));
        Assert.IsFalse(File.Exists(Path.Combine(f.Game, "Client/Saved/Resources/story.bin")));
        Assert.AreEqual("", await f.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadPackageType));
        Assert.AreEqual("False", await f.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadFolderDone));
        Assert.AreEqual(released ? "False" : "True", await f.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdIsAdvance));
    }

    [TestMethod]
    public async Task MinimalRepairPreservesExcludedResourcesAndUserSettings()
    {
        await using var f = await Fixture.Create(packages: true);
        var provider = (BundleGameResourceProvider)f.Context.GameResourceProvider;
        await provider.SavePackageTypeAsync("HD", GamePackageType.Minimal);
        var userSettings = Path.Combine(f.Game, "Client/Saved/Config/GameUserSettings.ini");
        var oldStory = Path.Combine(f.Game, "Client/Saved/Resources/story.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(userSettings)!);
        Directory.CreateDirectory(Path.GetDirectoryName(oldStory)!);
        await File.WriteAllTextAsync(userSettings, "keep-user-settings");
        await File.WriteAllTextAsync(oldStory, "keep-story");
        Assert.IsTrue(await f.Context.RepairGameAsync(isDelete: true, parameter: new() { BundleName = "HD" }));
        Assert.AreEqual("keep-user-settings", await File.ReadAllTextAsync(userSettings));
        Assert.AreEqual("keep-story", await File.ReadAllTextAsync(oldStory));
        Assert.AreEqual(GamePackageType.Minimal, await GamePackageStateService.ReadAsync(f.Game, "bridge/state.json", "HD"));
    }

    [TestMethod]
    public async Task CancelledMinimalPredownloadDoesNotWriteGameCompletionState()
    {
        await using var f = await Fixture.Create(packages: true);
        await GamePackageStateService.WriteAsync(f.Game, "bridge/state.json", "HD", GamePackageType.Full);
        f.Handler.DelayDownloads = true;
        Assert.IsTrue(await f.Context.StartProdDownloadGameResourceAsync(f.Cache,
            new() { BundleName = "HD", PackageType = GamePackageType.Minimal }));
        await f.Handler.DownloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await f.Context.StopCannelTaskAsync();
        await f.WaitUntil(() => Task.FromResult(!f.Context.IsResourceOperationActive));
        Assert.IsTrue(await f.VersionIs("1"));
        Assert.AreEqual("False", await f.Context.GameLocalConfig.GetConfigAsync(GameLocalSettingName.ProdDownloadFolderDone));
        Assert.AreEqual(GamePackageType.Full, await GamePackageStateService.ReadAsync(f.Game, "bridge/state.json", "HD"));
    }
}
