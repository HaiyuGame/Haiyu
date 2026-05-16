using System.Runtime.CompilerServices;
using Waves.Api.Models.CloudGame;
using Waves.Core.Helpers;

namespace Waves.Core.Models.CloudGame;

/// <summary>
/// 云游戏登录快照
/// </summary>
public partial class CloudGameLoginSession
{
    public LoginData OrginData { get; set; }

    
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
}