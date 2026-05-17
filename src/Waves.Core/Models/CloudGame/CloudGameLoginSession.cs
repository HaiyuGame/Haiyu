using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.CompilerServices;
using Waves.Api.Models.CloudGame;
using Waves.Core.Helpers;

namespace Waves.Core.Models.CloudGame;

/// <summary>
/// 云游戏登录快照
/// </summary>
public partial class CloudGameLoginSession:ObservableObject
{
    [ObservableProperty]
    public partial CloudGameLoginData OrginData { get; set; }

    [ObservableProperty]
    public partial PhoneTokenData PhoneToken { get; internal set; }

    [ObservableProperty]
    public partial AccessData AccessData { get; internal set; }

    [ObservableProperty]
    public partial EndLoginReponseData EndLoginData { get; internal set; }
    public string TraceId { get; internal set; }

    public DateTime SaveTime { get; internal set; }
}

/// <summary>
/// 登录窗口快宅
/// </summary>
public partial class CloudGameLoginSnapshot
{
    public string DeviceNum { get; }

    public CloudGameLoginSnapshot(string deviceNum)
    {
        DeviceNum = deviceNum;
    }

    /// <summary>
    /// 创建登录快照
    /// </summary>
    /// <returns></returns>
    public static CloudGameLoginSnapshot Create()
    {
        return new(HardwareIdGenerator.GenerateDeviceId());
    }

    public static CloudGameLoginSnapshot Create(CloudGameLoginData? data)
    {
        return new CloudGameLoginSnapshot(data?.LoginDid ?? HardwareIdGenerator.GenerateDeviceId());
    }
}