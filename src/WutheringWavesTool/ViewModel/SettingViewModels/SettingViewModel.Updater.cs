using Haiyu.Plugin.Contracts;

namespace Haiyu.ViewModel;

partial class SettingViewModel
{
    [ObservableProperty]
    public partial List<string> UpdateAppType { get; set; } = ["Github", "Mirror"];

    [ObservableProperty]
    public partial string SelectUpdateAppType { get; set; }

    partial void OnSelectUpdateAppTypeChanged(string value)
    {
        if (AppSettings.UpdateType != value)
            AppSettings.UpdateType = value;
    }

    [ObservableProperty]
    public partial string MirrorKey { get; set; }

    public void LoadUpdateAppType()
    {
        if (DesktopBridge.IsRunningAsMsix())
        {
            CheckUpdateVisibility = false;
            return;
        }
        CheckUpdateVisibility = true;
        foreach (var item in UpdateAppType.Index())
        {
            if(AppSettings.UpdateType == item.Item)
            {
                this.SelectUpdateAppType = item.Item;
            }
        }
        if(SelectUpdateAppType == null)
        {
            this.SelectUpdateAppType = UpdateAppType[0];
        }
        this.MirrorKey = AppSettings.MirrorKey;
    }


    [RelayCommand]
    async Task UpdateVersion()
    {
        await AppContext.UpdateAppAsync(true);
    }

    [RelayCommand]
    void SetMirrorKey()
    {
        AppSettings.MirrorKey = MirrorKey;
        if(Instance.Host.Services.GetRequiredKeyedService<IUpdateService>("Mirror") is IMirrorUpdateService mirror)
        {
            mirror.SetMirrorKey(MirrorKey);
        };
        TipShow.ShowMessage("设置成功！", Symbol.Accept);
    }

}
