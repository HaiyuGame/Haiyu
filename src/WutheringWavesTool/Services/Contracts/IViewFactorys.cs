using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Haiyu.Services.Contracts;

public interface IViewFactorys
{
    public IAppContext<App> AppContext { get; }
    public GetGeetWindow CreateGeetWindow(GeetType type);

    public WindowModelBase ShowSignWindow(GameRoilDataItem role);



    public TransparentWindow CreateTransperentWindow();

    public WindowModelBase ShowAdminDevice();

    public WindowModelBase ShowAnalysisRecordV2(CloudGameLoginSession selectLogin);

    public WindowModelBase ShowAutoKruoTokenWindow();

}
