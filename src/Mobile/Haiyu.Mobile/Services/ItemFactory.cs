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
                    item.SetData(x);
                    return item;
                })
            );

        public ObservableCollection<LocalUserItemViewModel> CreateLocalUserItems(
            List<LocalAccount> locals
        ) => CreateItem<LocalUserItemViewModel, LocalAccount>(locals);

        public ObservableCollection<GameRoilItemViewModel> CreateGameRoilItems(List<GameRoilDataItem> roils)
        => CreateItem<GameRoilItemViewModel, GameRoilDataItem>(roils);
    }
}
