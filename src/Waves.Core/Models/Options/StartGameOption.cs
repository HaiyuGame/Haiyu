namespace Waves.Core.Models.Options;

public class StartGameOption
{
    /// <summary>
    /// 相对路径
    /// </summary>
    public static IReadOnlyCollection<string> GetWavesExes =
    [
        "Wuthering Waves.exe",
        "Client\\Binaries\\Win64\\Client-Win64-Shipping.exe",
    ];
    public static IReadOnlyCollection<string> GetPunishExes = ["PGR.exe"];

    public required GameType Type { get; init; }

    public WavesLauncheOption? WavesOption { get; internal set; }
    public PunishLauncheOption? PunishOption { get; internal set; }

    public override string ToString()
    {
        if(Type == GameType.Waves)
        {
            return WavesOption?.ToString()!;
        }
        else
        {
            return PunishOption?.ToString()!;
        }
    }

    public static StartGameOption BuildWavesGameOption(
        bool dxEnable,
        bool dlssElable,
        string argument,
        string launcheBaseExe
    )
    {
        return new()
        {
            Type = GameType.Waves,
            WavesOption = new()
            {
                IsDx = dxEnable,
                Arguments = argument,
                DLLSEnable = dlssElable,
                BaseExe = launcheBaseExe,
            },
        };
    }

    public static StartGameOption BuildPunishGameOption(
        string argument,
        string? launcheBaseExe = null
    )
    {
        return new()
        {
            Type = GameType.Punish,
            WavesOption = null,
            PunishOption = new PunishLauncheOption()
            {
                Arguments = argument,
                BaseExe = launcheBaseExe,
            },
        };
    }
}

/// <summary>
/// 鸣潮启动配置
/// </summary>
public class WavesLauncheOption
{
    public bool IsDx { get; internal set; }

    public string Arguments { get; internal set; }

    public bool DLLSEnable { get; internal set; }

    public string BaseExe { get; internal set; }

    public override string ToString() =>
        $"Client "
        + (IsDx == true ? " -dx11" : "")
        + (IsDx == true ? " -slno" : "")
        + $" {Arguments}";
}

/// <summary>
/// 战双启动配置
/// </summary>
public class PunishLauncheOption
{
    public string Arguments { get; internal set; }

    public string? BaseExe { get; internal set; }

    public override string ToString() => Arguments;
}
