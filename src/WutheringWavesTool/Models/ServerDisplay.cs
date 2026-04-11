using Waves.Core.GameContext.Contexts.PRG;
using Waves.Core.GameContext.ContextsV2;
using Waves.Core.GameContext.ContextsV2.Waves;

namespace Haiyu.Models;

public class ServerDisplay
{
    public string Display { get; set; }

    public string Tag { get; set; }

    public string Key { get; set; }

    /// <summary>
    /// 是否显示公告面板
    /// </summary>
    public bool ShowCard { get; set; }

    public static ObservableCollection<ServerDisplay> GetWavesGames =>
        [
            new ServerDisplay()
            {
                Display = "官服",
                Key = $"{nameof(WavesMainGameContext)}",
                Tag = "Main",
                ShowCard = true,
            },
            new ServerDisplay()
            {
                Display = "Bilibili",
                Key = $"{nameof(WavesBiliBiliGameContext)}",
                Tag = "Main",
                ShowCard = false,
            },
            new ServerDisplay()
            {
                Display = "国际服",
                Key = $"{nameof(WavesGlobalGameContext)}",
                Tag = "Main",
                ShowCard = true,
            },
        ];

    public static ObservableCollection<ServerDisplay> GetPunishGames =>
        [
            new ServerDisplay()
            {
                Display = "官服",
                Key = $"{nameof(PunishMainGameContext)}",
                Tag = "Main",
                ShowCard = true,
            },
            new ServerDisplay()
            {
                Display = "Bilibili",
                Key = $"{nameof(PunishBiliBiliGameContext)}",
                Tag = "Main",
                ShowCard = false,
            },
            new ServerDisplay()
            {
                Display = "国际服",
                Key = $"{nameof(PunishGlobalGameContext)}",
                Tag = "Main",
                ShowCard = true,
            },
            new ServerDisplay()
            {
                Display = "台服",
                Key = $"{nameof(PunishTwGameContext)}",
                Tag = "Main",
                ShowCard = true,
            },
        ];

    public static ObservableCollection<ServerDisplay> GetPunishV2Games =>
        [
            new ServerDisplay()
            {
                Display = "官服",
                Key = $"{nameof(PunishMainGameContextV2)}",
                Tag = "Main",
                ShowCard = true,
            },
        ];

    public static ObservableCollection<ServerDisplay> GetWavesV2Games =>
        [
            new ServerDisplay()
            {
                Display = "官服",
                Key = $"{nameof(WavesMainGameContextV2)}",
                Tag = "Main",
                ShowCard = true,
            },
            new ServerDisplay()
            {
                Display = "B服",
                Key = $"{nameof(WavesBiliBiliGameContextV2)}",
                Tag = "BiliBili",
                ShowCard = false,
            },
            new ServerDisplay()
            {
                Display = "国际服",
                Key = $"{nameof(WavesGlobalGameContextV2)}",
                Tag = "Global",
                ShowCard = true,
            },
        ];
}
