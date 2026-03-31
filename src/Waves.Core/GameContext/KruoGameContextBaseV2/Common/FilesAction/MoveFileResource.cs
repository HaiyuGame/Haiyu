using Waves.Core.Common;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext.Common.FilesAction;

/// <summary>
/// 移动文件， 一次性多文件操作，不能停止
/// </summary>
public class MoveFileResource : IProgressSetup, IAsyncDisposable
{
    public string ProgressName { get; set; }
    public Dictionary<string, object> Param { get; private set; }

    public double ProgressValue { get; private set; }

    public bool CanPause => false;

    public bool CanStop => false;

    public Dictionary<string, string> Files { get; private set; }

    private IGameEventPublisher gameEventPublisher;
    private LoggerService logger;

    public MoveFileResource(LoggerService logger)
    {
        this.logger = logger;
    }

    private async Task<bool> CheckAsync()
    {
        if (!Param.CheckParam<Dictionary<string, string>>("files", out var files))
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
        await Task.Run(() =>
        {
            var keys = Files.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                string value = Files[key].Replace("/", "\\");
                var dirName = System.IO.Path.GetDirectoryName(value)!;
                Directory.CreateDirectory(dirName);
                try
                {
                    if (File.Exists(value))
                        File.Delete(value);
                    File.Move(key.Replace("/","\\"), value, true);
                    this.gameEventPublisher.Publish(
                        new GameContextOutputArgs()
                        {
                            Type = GameContextActionType.BottomText,
                            FileTotal = keys.Count,
                            CurrentFile = i + 1,
                            DeleteString = $"正在移动校验文件{System.IO.Path.GetFileName(value)}",
                        }
                    );
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        });
        return true;
    }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
        this.Param = param;
        this.gameEventPublisher = gameEventPublisher;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }
}
