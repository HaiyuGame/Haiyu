using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Contracts.CloudGame;
using Waves.Core.Services;

namespace Haiyu.ViewModel.DialogViewModels;

public sealed partial class WavesCloudUserViewModel : DialogViewModelBase
{
    public WavesCloudUserViewModel(
        DialogSession dialogSession,
        IWavesCloudGameService wavesCloudGameService,
        [FromKeyedServices(nameof(KuroCloudGameContext))] IKuroCloudGameContext cloudGameContext
    )
        : base(dialogSession)
    {
        WavesCloudGameService = wavesCloudGameService;
        CloudGameContext = cloudGameContext;
        RegisterMessager();
    }

    private void RegisterMessager()
    {
        this.Messenger.Register<DeleteCloudUserMessager>(this, DeleteCloudUserMethod);
    }

    private async void DeleteCloudUserMethod(object recipient, DeleteCloudUserMessager message)
    {
        await this.WavesCloudGameService.DeleteUserAsync(message.id);
        await Loaded();
    }

    public IWavesCloudGameService WavesCloudGameService { get; }
    public IKuroCloudGameContext CloudGameContext { get; }

    [ObservableProperty]
    public partial ObservableCollection<CloudGameLoginDataWrapper> CloudUsers { get; set; }

    [RelayCommand]
    async Task Loaded()
    {
        var users = await this.WavesCloudGameService.ConfigManager.GetUsersAsync();
        var temp = users.Select(x => new CloudGameLoginDataWrapper(x)).ToObservableCollection();
        var currentLogin = await this.WavesCloudGameService.GetCurrentUserSession();
        if (currentLogin != null)
        {
            foreach (var item in temp)
            {
                if (currentLogin.GetId() == item.Id)
                {
                    item.IsSelect = true;
                }
            }
        }
        this.CloudUsers = temp;
    }

    [RelayCommand]
    public async Task ApplyCloudUser()
    {
        var selectUser = this.CloudUsers.FirstOrDefault(x => x.IsSelect);
        if (selectUser == null)
            return;
        var result = await this.WavesCloudGameService.SetCurrentUserSession(selectUser.Id);
        if (result)
        {
            await AppSettings.SetSelectCloudUserIDAsync(selectUser.Id);
        }
        await this.CloseAsync();
    }
}
