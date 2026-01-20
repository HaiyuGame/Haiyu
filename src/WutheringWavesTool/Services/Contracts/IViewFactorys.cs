using Waves.Api.Models.CloudGame;

namespace Haiyu.Services.Contracts;

public interface IViewFactorys
{
    public IAppContext<App> AppContext { get; }
    public GetGeetWindow CreateGeetWindow(GeetType type);

    public WindowModelBase ShowSignWindow(GameRoilDataItem role);

    public WindowModelBase ShowRolesDataWindow(ShowRoleData detily);

    public WindowModelBase ShowPlayerRecordWindow();

    public WindowModelBase ShowWavesDataCenter(GameRoilDataItem item);
    public Window ShowAnalysisRecord(LoginData data);
    public TransparentWindow CreateTransperentWindow();
    public WindowModelBase ShowAdminDevice();

    public WindowModelBase ShowStartColorGame();
    public WindowModelBase ShowColorGame();
}
