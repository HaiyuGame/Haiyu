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
        await AppContext.UpdateAppAsync(true);
    }

}
