using Haiyu.Models.Dialogs;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System.ComponentModel;
using System.Windows.Input;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Haiyu.ViewModel.GameViewModels;

/// <summary>
/// 通用ViewModel基类
/// </summary>
public interface IKuroGameContextViewModelBase
{
    public GameType GameType { get;}

    #region Properties

    /// <summary>
    /// 默认服务器名称
    /// </summary>
    LoggerService Logger { get; }
    IGameContext GameContext { get; }
    IDialogManager DialogManager { get; }
    IAppContext<App> AppContext { get; }
    ITipShow TipShow { get; }
    IWallpaperService WallpaperService { get; }

    bool IsDx11Launcher { get; set; }
    Visibility GameInstallBthVisibility { get; set; }
    Visibility GameInputFolderBthVisibility { get; set; }
    Visibility GameDownloadingBthVisibility { get; set; }
    Visibility GameLauncherBthVisibility { get; set; }
    string LauncherIcon { get; set; }
    ImageSource VersionLogo { get; set; }
    string LauncheContent { get; set; }
    string DisplayVersion { get; set; }
    string PauseIcon { get; set; }
    bool PauseStartEnable { get; set; }
    string BottomBarContent { get; set; }
    bool EnableStartGameBth { get; set; }
    double MaxProgressValue { get; set; }
    double CurrentProgressValue { get; set; }
    int DownloadSpeedValue { get; set; }
    Color StressShadowColor { get; set; }


    #endregion


    #region Commands
    IAsyncRelayCommand ShowSelectInstallFolderCommand { get; }
    IAsyncRelayCommand ShowSelectGameFolderCommand { get; }
    IAsyncRelayCommand RepirGameCommand { get; }
    IAsyncRelayCommand ShowGameResourceCommand { get; }
    IAsyncRelayCommand DeleteGameResourceCommand { get; }
    IAsyncRelayCommand ShowGameLauncherCacheCommand { get; }
    IAsyncRelayCommand SetDownloadSpeedCommand { get; }
    IAsyncRelayCommand UpdateGameCommand { get; }
    IAsyncRelayCommand PauseDownloadTaskCommand { get;}
    IAsyncRelayCommand CancelDownloadTaskCommand { get; }
    #endregion

    #region Server
    public ObservableCollection<ServerDisplay> Servers { get; set; }

    public ServerDisplay SelectServer { get; set; }

    #endregion

    Task SelectGameContextAsync(string name,bool isCard);
}
