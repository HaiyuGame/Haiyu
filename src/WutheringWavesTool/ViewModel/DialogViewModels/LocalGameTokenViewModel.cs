using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.ViewModel.DialogViewModels
{
    public sealed partial class LocalGameTokenViewModel : DialogViewModelBase
    {
        public IGameContextV2 GameContext { get; private set; }

        [ObservableProperty]
        public partial ObservableCollection<KuroGameTokenWrapper> Tokens { get; set; }


        public async Task RefreshContextName(string contextName)
        {
            this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(contextName);
            await RefreshLocalTokenAsync(this.CTS.Token);

        }

        [RelayCommand]
        public async Task SaveAndClose()
        {
            var tokens = this.Tokens.Where(x=>x.IsSelect==true).FirstOrDefault();
            if (tokens == null)
                return;
            await this.GameContext.SetCurrentLoginSdkToken(cache: tokens.Cache, this.CTS.Token);
            await this.Close();
        }

        async Task RefreshLocalTokenAsync(CancellationToken token = default)
        {
            var localCache = await this.GameContext.GetSDKGameTokenAsync(this.CTS.Token);

            if(localCache== null)
            {
                return;
            }

            this.Tokens = localCache.AccountList.Select(x=>new KuroGameTokenWrapper(x)).ToObservableCollection();
            foreach (var item in Tokens)
            {
                if (item.Cache.Cuid == localCache.LastLoginCuid)
                {
                    item.IsSelect = true;
                }
            }
        }
    }
}
