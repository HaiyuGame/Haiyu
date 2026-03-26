using Waves.Core.Common;
using Waves.Core.Contracts.Events;
using Waves.Core.Models;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common;

/// <summary>
/// 安装库洛解压报资源类
/// </summary>
public class InstallKrZipResource : IProgressSetup
{
    public string ProgressName { get; set; }

    public double ProgressValue { get; private set; }

    public bool CanPause => true;

    public bool CanStop => true;

    public IGameEventPublisher GameEventPublisher { get; private set; }
    public Dictionary<string, object> Param { get; private set; }

    public void SetParam(Dictionary<string, object> param, IGameEventPublisher gameEventPublisher)
    {
        this.GameEventPublisher = gameEventPublisher;
        this.Param = param;
    }

    public async Task<bool> CheckAsync()
    {
        if (!Param.CheckParam<string>("zipPath", out var zipPath))
        {
            return false;
        }
        if (!Param.CheckParam<string>("extractPath", out var extractPath))
        {
            return false;
        }
        if (!File.Exists(zipPath!))
        {
            return false;
        }
        return true;
    }

    public async Task<bool> RunAsync()
    {
        return true;
    }

    public Task<object?> ExecuteAsync(bool isSync = false)
    {
        throw new NotImplementedException();
    }
}
