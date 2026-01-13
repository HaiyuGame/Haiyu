using Haiyu.Models.ColorGames;
using Waves.Core.Settings;

namespace Haiyu.Services;

public class ColorGameManager : IColorGameManager
{
    public async Task<List<ColorInfo>> GetGamesAsync(CancellationToken token = default)
    {
        var items = new List<ColorInfo>();
        foreach (var item in Directory.GetFiles("*.json"))
        {
            try
            {
                var model = JsonSerializer.Deserialize(await File.ReadAllTextAsync(item), GameContext.Default.ColorInfo);
                items.Add(model);
            }
            catch (Exception)
            {
                continue;
            }
        }
        return items;
    }

    public async Task<(bool, string)> SaveGameAsync(ColorInfo info, string currentFile, CancellationToken token = default)
    {
        if (File.Exists(currentFile))
        {
            using (var fs = File.CreateText(currentFile))
            {
                await fs.WriteAsync(JsonSerializer.Serialize(info, GameContext.Default.ColorInfo));
            }
            return (true, currentFile);
        }
        else
        {
            var file = AppSettings.ColorGameFolder + $"\\{info.GameFile}.json";
            if (File.Exists(file))
            {
                return (false, "游戏文件已经存在，请重新命名！");
            }
        }
        return (false, "未知错误");
    }
}
