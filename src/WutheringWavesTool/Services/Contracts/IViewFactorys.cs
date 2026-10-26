using Haiyu.Models.Enums;
using Waves.Api.Models.CloudGame;
using Waves.Core.Models.CloudGame;

namespace Haiyu.Services.Contracts;

public interface IViewFactorys
{
    public IAppContext<App> AppContext { get; }
    public GetGeetWindow CreateGeetWindow(nint value,GeetType type);

    public void ShowSignWindow(GameRoilDataItem role);




    public void ShowAdminDevice();

    public void ShowAnalysisRecordV2(CloudGameLoginSession selectLogin);

    #region Tool

    public void ShowAutoKruoTokenWindow();

    public void ShowMoniterSettingWindow();
    public void ShowMonitorToolWindow(PostionType postion = PostionType.TopCenter);

    public void ShowWavesRecordManagerWindow();
    #endregion


}
