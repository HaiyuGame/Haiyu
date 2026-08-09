namespace Haiyu.Mobile.Common;

public partial class ItemsViewModelBase<Args>:ViewModelBase,IItemData<Args>
{
    public ItemsViewModelBase(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    public Args BaseData { get; set; }
}
