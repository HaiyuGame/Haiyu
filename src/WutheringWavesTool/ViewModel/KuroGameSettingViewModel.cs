using Haiyu.Common.Contracts;
using Waves.Core.Models.Enums;
using Waves.Core.Models.Options;

namespace Haiyu.ViewModel;

public sealed partial class KuroGameSettingViewModel : DialogViewModelBase
{
    public KuroGameSettingViewModel(DialogSession dialogSession)
        : base(dialogSession) { }

    public IGameContextV2 GameContext { get; private set; }

    public GameType GameType { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<string> LuancheExeNames { get; set; }

    [ObservableProperty]
    public partial string LauncheExeName { get; set; }

    [ObservableProperty]
    public partial bool FastVerify { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultLaunchEnabled))]
    public partial bool UseCustomLauncher { get; set; }

    public bool DefaultLaunchEnabled => !UseCustomLauncher;

    [ObservableProperty]
    public partial string CustomLauncherPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CustomLauncherArguments { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CustomLaunchError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCustomLaunchError { get; set; }

    private CustomLauncherOptions GetCustomLauncher() => new()
    {
        Enabled = UseCustomLauncher,
        TargetPath = CustomLauncherPath.Trim(),
        Arguments = CustomLauncherArguments,
    };

    [RelayCommand]
    async Task BrowseCustomLauncher()
    {
        try
        {
            var file = await AppContext.WindowManager.Shell.PickersService.GetFileOpenPicker([".exe"]);
            if (file is null) return;
            CustomLauncherPath = file.Path;
            UseCustomLauncher = true;
            ValidateCustomLauncher();
        }
        catch (Exception ex)
        {
            CustomLaunchError = ex.Message;
            HasCustomLaunchError = true;
        }
    }

    [RelayCommand]
    void ValidateCustomLauncher()
    {
        var error = GetCustomLauncher().Validate();
        CustomLaunchError = error is null ? string.Empty : LanguageService.GetStringByText(error);
        HasCustomLaunchError = CustomLaunchError.Length > 0;
    }

    [RelayCommand]
    async Task SaveSettings()
    {
        ValidateCustomLauncher();
        if (HasCustomLaunchError) return;
        await CloseAsync();
    }

    [RelayCommand]
    async Task Loaded()
    {
        var custom = await CustomLauncherOptions.LoadAsync(GameContext.GameLocalConfig);
        CustomLauncherPath = custom.TargetPath;
        CustomLauncherArguments = custom.Arguments;
        UseCustomLauncher = custom.Enabled;
        var speed = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LimitSpeed
        );
        var fastVerify = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.FastVerify
        );
        if (long.TryParse(speed, out var speedValue))
        {
            this.DownloadSpeedLimit = speedValue;
        }
        if (bool.TryParse(fastVerify, out var fastVerifyFlage))
        {
            this.FastVerify = fastVerifyFlage;
        }
        Arguments =
            await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.StartGameArguments
            ) ?? string.Empty;
        LauncheExeName =
            await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.StartGameExeName
            )
            ?? LuancheExeNames?.FirstOrDefault()
            ?? string.Empty;

        if (this.GameType == GameType.Waves)
        {
            var disableDlss = await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.DisableDlss
            );
            if (bool.TryParse(disableDlss, out var disableDlssValue))
            {
                DlssEnable = disableDlssValue;
            }

            var dx11 = await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.IsDx11
            );
            if (bool.TryParse(dx11, out var dx11Value))
            {
                Directx11Enable = dx11Value;
            }

            var cleanLogs = await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.CleanGameLogsOnStart
            );
            if (bool.TryParse(cleanLogs, out var cleanLogsValue))
            {
                CleanLogsOnStart = cleanLogsValue;
            }
        }
    }

    public void SetConfig(GameSettingDialogConfig config)
    {
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(
            config.CoreName
        );
        this.GameType = this.GameContext.GameType;
        if (GameType == GameType.Waves)
        {
            WavesSettingVisibility = Visibility.Visible;
            LuancheExeNames = new(StartGameOption.GetWavesExes);
        }
        else
        {
            LuancheExeNames = new(StartGameOption.GetPunishExes);
        }
    }

    [ObservableProperty]
    public partial double DownloadSpeedLimit { get; set; }

    [ObservableProperty]
    public partial string Arguments { get; set; }

    [ObservableProperty]
    public partial bool DlssEnable { get; set; }

    [ObservableProperty]
    public partial Visibility WavesSettingVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial bool Directx11Enable { get; set; }

    [ObservableProperty]
    public partial bool CleanLogsOnStart { get; set; }

    public override async Task BeforeCloseAsync()
    {
        await this.GameContext.SetDownloadSpeedAsync((long) DownloadSpeedLimit);
        if (this.GameType == GameType.Waves)
        {
            await this.GameContext.GameLocalConfig.SaveConfigsAsync(
                new Dictionary<string, string>()
                {
                    [GameLocalSettingName.IsDx11] = Directx11Enable ? "true" : "false",
                    [GameLocalSettingName.DisableDlss] = DlssEnable ? "true" : "false",
                    [GameLocalSettingName.StartGameArguments] = Arguments ?? string.Empty,
                    [GameLocalSettingName.StartGameExeName] =
                        LauncheExeName ?? LuancheExeNames?.FirstOrDefault() ?? string.Empty,
                    [GameLocalSettingName.FastVerify] = this.FastVerify.ToString(),
                    [GameLocalSettingName.CleanGameLogsOnStart] = CleanLogsOnStart
                        ? "true"
                        : "false"
                }
            );
        }
        else
        {
            await this.GameContext.GameLocalConfig.SaveConfigsAsync(
                new Dictionary<string, string>()
                {
                    [GameLocalSettingName.StartGameArguments] = Arguments ?? string.Empty,
                    [GameLocalSettingName.StartGameExeName] =
                        LauncheExeName ?? LuancheExeNames?.FirstOrDefault() ?? string.Empty,
                    [GameLocalSettingName.FastVerify] = this.FastVerify.ToString()
                }
            );
        }
        await base.BeforeCloseAsync();
        var custom = GetCustomLauncher();
        if (custom.Validate() is null)
            await custom.SaveAsync(GameContext.GameLocalConfig);
    }
}
