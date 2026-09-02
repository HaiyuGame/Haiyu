using Haiyu.Models.Enums;

namespace Haiyu.Models.Options;

/// <summary>
/// 窗口导航配置或自身初始化配置
/// </summary>
public class WindowManagerOption
{
    public string Key { get; }

    public WindowRole Role { get; }


    public object Paramter { get; set; }

    public WindowsOption WindowConfig { get; set; }

    
}
