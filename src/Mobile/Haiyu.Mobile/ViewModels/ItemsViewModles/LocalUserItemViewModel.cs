using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Haiyu.KuroClient;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.Models.Messanger;
using Haiyu.Mobile.Views;

namespace Haiyu.Mobile.ViewModels.ItemsViewModles
{
    public partial class LocalUserItemViewModel : ItemsViewModelBase<LocalAccount>
    {
        public LocalUserItemViewModel(IServiceProvider serviceProvider, IKuroClient kuroClient)
            : base(serviceProvider)
        {
            KuroClient = kuroClient;
        }

        public IKuroClient KuroClient { get; }

        [ObservableProperty]
        public partial string UserName { get;  set; }


        [ObservableProperty]
        public partial int KuroCoin { get; set; }

        [ObservableProperty]
        public partial string ImageCover { get;  set; }

        [ObservableProperty]
        public partial string LastLoginTime { get;  set; }

        [ObservableProperty]
        public partial string Register { get;  set; }

        [ObservableProperty]
        public partial string Phone { get; set; }

        [ObservableProperty]
        public partial string Sign { get; set; }
        public LocalAccount BaseData { get; private set; }

        [RelayCommand]
        public void SendShowSession()
        {
            WeakReferenceMessenger.Default.Send<LocalUserSessionShowMessanger>(new(this));
        }

        [RelayCommand]
        async Task GotoScanAsync()
        {

            await Shell.Current.GoToAsync(nameof(ScanGameQrPage), true,new Dictionary<string, object>()
            {
                { "playerId",this.BaseData.TokenId}
            });
        }

        public override void SetData(LocalAccount args)
        {
            this.BaseData = args;
        }


        [RelayCommand]
        async Task Loaded()
        {
            var mine = await KuroClient.GetWavesMineAsync(
                KuroAccount.Create(this.BaseData),
                long.Parse(this.BaseData.TokenId),
                this.CTS.Token
            );
            if(mine ==null || !mine.Success)
            {
                //账号失效
                return;
            }
            this.UserName = mine.Data.Mine.UserName;
            this.KuroCoin = mine.Data.Mine.GoldNum;
            this.ImageCover = mine.Data.Mine.HeadUrl;
            this.LastLoginTime = mine.Data.Mine.LastLoginTime;
            this.Register = mine.Data.Mine.RegisterTime;
            this.Phone = mine.Data.Mine.Mobile;
            this.Sign = mine.Data.Mine.Signature;
        }
    }
}
