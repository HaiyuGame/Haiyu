using System.IO;
using Project.WPFSetup.Common.Setups;
using Project.WPFSetup.Resources;

namespace Project.WPFSetup.Common;

public class SetupProperty
{
    public string ProductId => "{60733E4A-A296-4181-85B8-2554EA40246A}";

    public string InstallPath { get; set; }

    public string InstallName { get; set; }

    public string Version { get; set; }

    public string UninstallString { get; set; }

    public string UninstallName { get; set; }

    public IList<ISetup> Setups { get; private set; }

    public IList<ISetup> UnSetups { get; private set; }
    public string InstallExeName { get; internal set; }

    public string HelpLink => "https://github.com/HaiyuGame/Haiyu/issues";

    public string UnInstallArgs { get; internal set; }

    public SetupProperty(IList<ISetup> setups, IList<ISetup> unSetups)
    {
        Setups = setups;
        UnSetups = unSetups;
    }

    internal string GetStartMenuFolder()
    {
        string startupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        return $"{startupFolderPath}\\Programs\\{InstallName}";
    }

    internal string GetDesktopMenuFolder()
    {
        string startupFolderPath = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory
        );
        return $"{startupFolderPath}";
    }

    internal string GetDesktopMenuLink() =>
        Path.Combine(GetDesktopMenuFolder(), $"{InstallName}.lnk");

    internal string GetUninstallLink() =>
        Path.Combine(GetStartMenuFolder(), $"Uninstall {InstallName}.lnk");

    internal string GetStartMenuLink() => Path.Combine(GetStartMenuFolder(), $"{InstallName}.lnk");

    internal string GetUninstallPath() => Path.Combine(InstallPath, UninstallName);
}

public static class SetupPropertyFactory
{
    public static SetupProperty CreateInstall() =>
        new SetupProperty([new DecompressionSetup(), new OutputLocalExeSetup()], [])
        {
            InstallName = Resource1.InstallName,
            InstallExeName = Resource1.ProgramExe,
            Version = Resource1.Version,
            UninstallName = "uninstall.exe",
            UnInstallArgs = "uninstall"
        };
}
