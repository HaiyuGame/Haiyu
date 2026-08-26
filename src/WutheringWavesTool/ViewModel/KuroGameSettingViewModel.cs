using Waves.Core.Models.Enums;
using Waves.Core.Models.Options;

namespace Haiyu.ViewModel;

public sealed partial class KuroGameSettingViewModel : DialogViewModelBase
{
    public IGameContextV2 GameContext { get; private set; }

    public GameType GameType { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<string> LuancheExeNames { get; set; }

    [ObservableProperty]
    public partial string LauncheExeName { get; set; }

    [RelayCommand]
    async Task Loaded()
    {
        var speed = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.LimitSpeed
        );
        if (long.TryParse(speed, out var speedValue))
        {
            this.DownloadSpeedLimit = speedValue;
        }

        Arguments =
            await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.StartGameArguments
            ) ?? string.Empty;
        LauncheExeName =
            await this.GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.StartGameExeName
            ) ?? LuancheExeNames?.FirstOrDefault() ?? string.Empty;

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

    public async override Task BeforeCloseAsync()
    {
        await this.GameContext.SetDownloadSpeedAsync((long)DownloadSpeedLimit);
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
                }
            );
        }
        await base.BeforeCloseAsync();
    }
}
