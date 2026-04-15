
using Waves.Core.GameContext.ContextsV2;
using Waves.Core.GameContext.ContextsV2.Punish;
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

    public static ObservableCollection<ServerDisplay> GetPunishV2Games =>
        [
            new ServerDisplay()
            {
                Display = "官服",
                Key = $"{nameof(PunishMainGameContextV2)}",
                Tag = "Main",
                ShowCard = true,
            },
            new ServerDisplay()
            {
                Display = "BiliBili",
                Key = $"{nameof(PunishBiliBiliGameContextV2)}",
                Tag = "BiliBili",
                ShowCard = false,
            },
            new ServerDisplay()
            {
                Display = "国际服",
                Key = $"{nameof(PunishGlobalGameContextV2)}",
                Tag = "Global",
                ShowCard = true,
            },new ServerDisplay()
            {
                Display = "台服",
                Key = $"{nameof(PunishTwGameContextV2)}",
                Tag = "Tw",
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
