

namespace Waves.Core.Contracts.CloudGame;

/// <summary>
/// 云库洛游戏上下文接口
/// </summary>
public interface IKuroCloudGameContext
{
    IGameEventPublisher<CloudMessageArgs> CloudGameEventPublisher { get; }
    CloudGameProcessTracker CloudGameProcessTracker { get; }
    GameLocalConfig GameLocalConfig { get; }
    
    Task InitAsync();

    Task StartGameAsync(
        CloudGameLoginSession session,
        IEnumerable<CloudGameNode> nodes,
        CloudGameNode node,
        StreamQualityOptions options,
        uint payType
    );

    Task StopQueueAsync();

    Task<KuroCLoudGameCoreState> GetCloudStateAsync();

    public Task<StreamQualityOptions?> GetOptionsAsync(int dpi, int width, int height);

    /// <summary>
    /// 取消当前活动排队
    /// </summary>
    /// <returns></returns>
    Task ClearActiveAsync();

    void ClearWindow();
    void SetGameingWindow(nint handle, string titleKey);
}
