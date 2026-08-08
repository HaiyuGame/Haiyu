using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Haiyu.KuroClient;
using Haiyu.Mobile.Common;
using Haiyu.Mobile.ViewModels.ItemsViewModles;
using Waves.Api.Models.Communitys;

namespace Haiyu.Mobile.Services
{
    public sealed class ItemFactory
    {
        public ItemFactory(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public ObservableCollection<T> CreateItem<T, Args>(List<Args> args)
            where T : IItemData<Args> =>
            new(
                args.Select(x =>
                {
                    var item = ServiceProvider.GetRequiredService<T>();
                    item.BaseData = x;
                    return item;
                })
            );

        public ObservableCollection<LocalUserItemViewModel> CreateLocalUserIetms(
            List<LocalAccount> locals
        ) => CreateItem<LocalUserItemViewModel, LocalAccount>(locals);
    }
}
