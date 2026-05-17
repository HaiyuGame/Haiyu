using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Services.CloudGameServices;

namespace Waves.Core.Contracts.CloudGame;

/// <summary>
/// 云库洛游戏上下文接口
/// </summary>
public interface IKuroCloudGameContext
{

    WavesCloudSurvivalService WavesCloudSurivivalService { get; }

    public Task InitAsync();
}
