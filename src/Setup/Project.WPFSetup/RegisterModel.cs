using System.IO;
using Microsoft.Win32;
using Project.WPFSetup.Common;
using Project.WPFSetup.Resources;

namespace Project.WPFSetup;

public class RegisterModel
{
    //public string ProductId => "{6B29B687-E1D1-4CDD-8B83-11E251AF8B73}";
    //public string Comments => Resource1.InstallName;
    //public string Contact => "";
    //public string DisplayIcon => System.Reflection.Assembly.GetExecutingAssembly().Location;
    //public string DisplayName => Resource1.InstallName;
    //public string DisplayVersion => Resource1.Version;
    //public int EstimatedSize { get; set; }
    //public string HelpLink => "https://github.com/blametwo/Haiyu.git";
    //public string InstallDate => DateTime.Now.ToString();
    //public string InstallLocation { get; set; }
    //public string InstallSource { get; set; }
    //public string UninstallString { get; set; }
    //public string Publisher => "blame";

    public Dictionary<string, (RegistryValueKind, object)> Keys { get; set; }

    public string ProductId => "{6B29B687-E1D1-4CDD-8B83-11E251AF8B73}";

    public static RegisterModel CreateRegister(SetupProperty property, int length)
    {
        return new RegisterModel()
        {
            Keys = new Dictionary<string, (RegistryValueKind, object)>()
            {
                { "EstimatedSize", (RegistryValueKind.DWord, length) },
                { "DisplayName", (RegistryValueKind.String, property.InstallName) },
                { "Contact", (RegistryValueKind.String, property.InstallName) },
                {
                    "UninstallString",
                    (
                        RegistryValueKind.String,
                        $"\"{property.UninstallString}\" {property.UnInstallArgs}"
                    )
                },
                { "DisplayVersion", (RegistryValueKind.String, property.Version) },
                { "Publisher", (RegistryValueKind.String, "Blame") },
                { "InstallLocation", (RegistryValueKind.String, property.InstallPath) },
                {
                    "DisplayIcon",
                    (
                        RegistryValueKind.String,
                        $@"{property.InstallPath}\{property.InstallExeName},0"
                    )
                },
                { "InstallDate", (RegistryValueKind.String, DateTime.Now.ToString("yyyyMMdd")) },
                {
                    "VersionMajor",
                    (RegistryValueKind.DWord, Version.Parse(property.Version).Major)
                },
                {
                    "VersionMinor",
                    (RegistryValueKind.DWord, Version.Parse(property.Version).Minor)
                },
                { "Language", (RegistryValueKind.DWord, 0x0800) },
                { "SystemComponent", (RegistryValueKind.DWord, 0) },
                { "NoRemove", (RegistryValueKind.DWord, 0) },
                { "NoModify", (RegistryValueKind.DWord, 0) },
                { "NoRepair", (RegistryValueKind.DWord, 0) },
                { "HelpLink", (RegistryValueKind.String, property.HelpLink) },
                { "URLInfoAbout", (RegistryValueKind.String, "") },
            },
        };
    }
}
