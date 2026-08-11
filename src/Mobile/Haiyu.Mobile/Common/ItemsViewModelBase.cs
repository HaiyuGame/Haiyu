namespace Haiyu.Mobile.Common;

public partial class ItemsViewModelBase<Args>:ViewModelBase,IItemData<Args>
{
    public ItemsViewModelBase(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }



    public virtual void SetData(Args args)
    {

    }
}
