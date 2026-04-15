using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Timers;
using Waves.Core.Common;
using Waves.Core.Models;
using Waves.Core.Models.Enums;

namespace Waves.Core.GameContext
{
    partial class KuroGameContextBaseV2
    {
        Process? _gameProcess = null;
        private bool _isStarting;
        private int gameId;
        private string gameFile;
        private DateTime _playGameTime = DateTime.MinValue;
        private System.Timers.Timer? gameRunTimer;
        private uint ppid;

        public virtual async Task<bool> StartGameAsync()
        {
            try
            {
                string gameFolder = await GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.GameLauncherBassFolder
                );
                Process ps = new();
                string argument = "";
                if (this.GameType == GameType.Waves)
                {
                    var ixDx11 = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.IsDx11);
                    if (bool.TryParse(ixDx11, out var flag) && flag)
                    {
                        argument = " -dx11";
                    }
                    else
                    {
                        argument = " -dx12";
                    }
                }
                ProcessStartInfo info = new(gameFolder + "\\" + this.Config.GameExeName)
                {
                    Arguments = argument,
                    WorkingDirectory = gameFolder,
                    Verb = "runas",
                    UseShellExecute = true,
                };
                this._gameProcess = ps;
                _gameProcess.StartInfo = info;
                _gameProcess.Start();
                this._isStarting = true;
                this.gameId = _gameProcess.Id;
                this.gameFile = info.FileName;
                this._playGameTime = DateTime.Now;
                gameRunTimer = new System.Timers.Timer();
                gameRunTimer.Elapsed += GameRunTimer_Elapsed;
                gameRunTimer.Interval = 3000;
                gameRunTimer.Start();
                Logger.WriteInfo("正在启动游戏……");
                this.GameEventPublisher.Publish(new GameContextOutputArgs { Type = GameContextActionType.None });
                return true;
            }
            catch (Exception ex)
            {
                this._isStarting = false;
                Logger.WriteError($"游戏启动错误{ex.Message}");

                this.GameEventPublisher.Publish(new GameContextOutputArgs { Type = GameContextActionType.None });
                    return false;
                return false;
            }
        }

        private async void GameRunTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    ProcessScan.CheckGameAliveWithWin32(
                        Path.GetFileName(gameFile),
                        (uint)this.gameId,
                        out bool contained,
                        out uint ppid,
                        out var filepath
                    );
                    return (contained, ppid);
                });

                if (!result.contained)
                {
                    gameRunTimer?.Elapsed -= GameRunTimer_Elapsed;
                    gameRunTimer?.Dispose();
                    gameRunTimer = null;
                    Logger.WriteInfo("游戏退出");
                    await OnGameExited();
                }
                else
                {
                    this.ppid = result.ppid;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError($"检查游戏状态失败: {ex.Message}");
            }
        }

        private async Task OnGameExited()
        {
            // 清理资源
            gameRunTimer?.Dispose();
            _gameProcess?.Dispose();
            _gameProcess = null;
            _isStarting = false;
            var realRunTime = GetGameTime();
            Logger.WriteInfo($"游戏已退出，游戏运行时长:{realRunTime:G}");
            var runGameTime = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.GameRunTotalTime);
            double runTime = 0.0;
            if (runGameTime != null)
                runTime = int.Parse(runGameTime);
            await this.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.GameRunTotalTime,
                (runTime += Convert.ToInt32(realRunTime.TotalSeconds)).ToString()
            );
            this.GameEventPublisher.Publish(new GameContextOutputArgs { Type = GameContextActionType.GameExit });
        }

        public TimeSpan GetGameTime()
        {
            if (_playGameTime == DateTime.MinValue)
            {
                return TimeSpan.Zero;
            }
            return DateTime.Now - _playGameTime;
        }

        [Obsolete("该方法已过时，由于非管理员权限设置，无法进行关闭进程")]
        public async Task StopGameAsync()
        {
            if (!this._isStarting && _gameProcess == null)
            {
                return;
            }
            Process.GetProcessById((int)ppid).Kill();
            _gameProcess?.Kill(true);
            Logger.WriteInfo("退出游戏………………");
            this._isStarting = false;
            this.GameEventPublisher.Publish(new GameContextOutputArgs { Type = GameContextActionType.GameExit });
        }
    }
}
