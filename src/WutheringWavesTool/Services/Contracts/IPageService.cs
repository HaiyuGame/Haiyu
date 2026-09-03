using Haiyu.Common.Contracts;

namespace Haiyu.Services.Contracts;

public interface IPageService
{
    public Type GetPage(string key);

    public void RegisterView<View, ViewModel>()
        where View : Page, IPage
        where ViewModel : ObservableObject;
}
