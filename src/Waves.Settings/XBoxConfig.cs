namespace Waves.Settings;

[SettingsAttribute<bool>(Name = "IsEnable", Nullable = true)]
[SettingsAttribute<string>(Name = "A", Nullable = true)]
[SettingsAttribute<string>(Name = "B", Nullable = true)]
[SettingsAttribute<string>(Name = "X", Nullable = true)]
[SettingsAttribute<string>(Name = "Y", Nullable = true)]
[SettingsAttribute<string>(Name = "Left", Nullable = true)]
[SettingsAttribute<string>(Name = "Top", Nullable = true)]
[SettingsAttribute<string>(Name = "Right", Nullable = true)]
[SettingsAttribute<string>(Name = "Bottom", Nullable = true)]
[SettingsAttribute<bool>(Name = "FpsEnable", DefaultValue = "False")]
public partial class XBoxConfig : SettingBase
{
    private static readonly string XboxConfigFilePath = Path.Combine(
        AppSettings.BassFolder,
        "xbox.json"
    );

    public XBoxConfig()
        : base(XboxConfigFilePath)
    {
    }
}
