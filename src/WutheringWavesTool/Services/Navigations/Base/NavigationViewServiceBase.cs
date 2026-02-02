namespace Haiyu.Services.Navigations.Base;

public class NavigationViewServiceBase : INavigationViewService
{
    internal NavigationView _viewBase = null;

    public NavigationViewServiceBase(INavigationService navigationService, IPageService pageService)
    {
        NavigationService = navigationService;
        PageService = pageService;
    }

    public INavigationService NavigationService { get; }
    public IPageService PageService { get; }

    public IList<object> GetFooterMenuItems() => _viewBase.FooterMenuItems;

    public IList<object> GetMenuItems() => _viewBase.MenuItems;

    public virtual NavigationViewItem GetSelectItem(Type pagetype) =>
        GetSelectedItem(GetMenuItems(), pagetype)
        ?? GetSelectedItem(GetFooterMenuItems(), pagetype);

    public void Register(NavigationView view)
    {
        _viewBase = view;
        _viewBase.ItemInvoked += _viewBase_ItemInvoked;
        _viewBase.BackRequested += _viewBase_BackRequested;
    }

    void ItemInvoked(NavigationViewItemInvokedEventArgs eventArgs)
    {
        if (eventArgs.IsSettingsInvoked)
        {
            //
        }
        else
        {
            var selectedItem = eventArgs.InvokedItemContainer as NavigationViewItem;
            var paramter = selectedItem?.GetValue(NavigationItemClickHelper.ParamterProperty);
            if (
                selectedItem?.GetValue(NavigationItemClickHelper.NavigationKeyProperty)
                is string pageKey
            )
            {
                NavigationService.NavigationTo(
                    pageKey,
                    paramter,
                    new DrillInNavigationTransitionInfo()
                );
            }
        }
    }

    private NavigationViewItem GetSelectedItem(IEnumerable<object> menuItems, Type pageType)
    {
        foreach (var item in menuItems.OfType<NavigationViewItem>())
        {
            if (IsMenuItemForPageType(item, pageType))
            {
                return item;
            }

            var selectedChild = GetSelectedItem(item.MenuItems, pageType);
            if (selectedChild != null)
            {
                return selectedChild;
            }
        }

        return null;
    }

    /// <summary>
    /// 是否注册MenuPpage
    /// </summary>
    /// <param name="menuItem"></param>
    /// <param name="sourcePageType"></param>
    /// <returns></returns>
    private bool IsMenuItemForPageType(NavigationViewItem menuItem, Type sourcePageType)
    {
        if (menuItem.GetValue(NavigationItemClickHelper.NavigationKeyProperty) is string pageKey)
        {
            return PageService.GetPage(pageKey) == sourcePageType;
        }

        return false;
    }

    private void _viewBase_BackRequested(
        NavigationView sender,
        NavigationViewBackRequestedEventArgs args
    )
    {
        NavigationService.GoBack();
    }

    private void _viewBase_ItemInvoked(
        NavigationView sender,
        NavigationViewItemInvokedEventArgs args
    )
    {
        ItemInvoked(args);
    }

    public void UnRegister()
    {
        _viewBase.ItemInvoked -= _viewBase_ItemInvoked;
        _viewBase.BackRequested -= _viewBase_BackRequested;

        _viewBase = null;
    }

    public object GetSettingItem() => _viewBase.SettingsItem;
}
