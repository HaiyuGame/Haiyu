namespace Haiyu.Services.Contracts;

public interface INavigationViewService
{
    /// <summary>
    /// 注册导航视图
    /// </summary>
    /// <param name="view"></param>
    /// <param name="navigationType"></param>
    void Register(NavigationView view, INavigationService navigationService);

    /// <summary>
    /// 卸载导航视图
    /// </summary>
    /// <param name="navigationType"></param>
    void UnRegister();

    /// <summary>
    /// 获得导航视图的选中项目
    /// </summary>
    /// <param name="navigationType"></param>
    /// <param name="pagetype"></param>
    /// <returns></returns>
    NavigationViewItem GetSelectItem(Type pagetype);

    /// <summary>
    /// 获得导航项目的全部菜单项目
    /// </summary>
    /// <param name="navigationType"></param>
    /// <returns></returns>
    public IList<object> GetMenuItems();

    /// <summary>
    /// 获得导航项目的脚菜单
    /// </summary>
    /// <param name="navigationType"></param>
    /// <returns></returns>
    public IList<object> GetFooterMenuItems();

    /// <summary>
    /// 仅限有设置页面的进行操作，不然返回null
    /// </summary>
    /// <returns></returns>
    public object GetSettingItem();
}
