using Waves.Core.Common;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext.Common.FilesAction;

/// <summary>
/// 移动文件， 一次性多文件操作，不能停止
/// </summary>
public class MoveFileResource : IProgressSetup
{
    public string ProgressName { get; set; }
    public Dictionary<string, object> Param { get; private set; }

    public double ProgressValue { get; private set; }

    public bool CanPause => false;

    public bool CanStop => false;

    public Dictionary<string, string> Files { get; private set; }

    private IGameEventPublisher gameEventPublisher;


    private async Task<bool> CheckAsync()
    {
        if (!Param.CheckParam<Dictionary<string,string>>("files",out var files))
        {
            return false;
        }
        this.Files = files!;
        return true;
    }

    public async Task<object?> ExecuteAsync(bool isSync = false)
    {
        if (isSync)
        {
            return await RunAsync();
        }
        else
        {
            Task.Run(async () => await RunAsync());
            return null;
        }
    }

    private async Task<object?> RunAsync()
    {
        if (!(await CheckAsync()))
        {
            return null;
        }
        var keys = Files.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            string value = Files[key];
            if (File.Exists(value))
                File.Delete(value);
            File.Move(key, value);
            this.gameEventPublisher.Publish(new GameContextOutputArgs()
            {
                Type = GameContextActionType.BottomText,
                FileTotal = keys.Count,
                CurrentFile = i,
                DeleteString = $"正在移动校验文件{System.IO.Path.GetFileName(value)}",
            });
            await Task.Delay(100);
        }
        return true;
    }


    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
        this.Param = param;
        this.gameEventPublisher = gameEventPublisher;
    }
}
