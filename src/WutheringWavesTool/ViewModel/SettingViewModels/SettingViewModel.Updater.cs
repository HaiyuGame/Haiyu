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
    }


    [RelayCommand]
    async Task UpdateVersion()
    {
        if (DesktopBridge.IsRunningAsMsix())
        {
            return;
        }
        IUpdateService? service = null;
        if(AppSettings.UpdateType == "Github")
        {
            service = Instance.Host.Services.GetKeyedService<Haiyu.Plugin.Contracts.IUpdateService>("GitHub");
        }
        else
        {
            throw new Exception("未实现Mirror更新源");
        }
        if (service == null)
            return;
        if (await service.CheckProgramUpdateAsync(App.AppVersion))
        {
            var info = await service.GetLasterProgramInfoAsync();
            if (info != null)
            {

            }
            else
            {
                await TipShow.ShowMessageAsync("检查更新失败，请稍后再试", Symbol.Clear);
            }
        }
    }

}
