using Waves.Core.Models.Options;

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

        public async Task<bool> StartGameAsync()
        {
            var arguments =
                await GameLocalConfig.GetConfigAsync(GameLocalSettingName.StartGameArguments)
                ?? string.Empty;
            var exeName =
                await GameLocalConfig.GetConfigAsync(GameLocalSettingName.StartGameExeName)
                ?? string.Empty;

            if (this.GameType == GameType.Waves)
            {
                var dx11 = await GameLocalConfig.GetConfigAsync(GameLocalSettingName.IsDx11);
                var disableDlss = await GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.DisableDlss
                );
                bool isDx11 = bool.TryParse(dx11, out var dx11Value) && dx11Value;
                bool isDisableDlss =
                    bool.TryParse(disableDlss, out var disableDlssValue) && disableDlssValue;

                return await StartGameAsync(
                    StartGameOption.BuildWavesGameOption(
                        isDx11,
                        isDisableDlss,
                        arguments,
                        exeName
                    )
                );
            }

            return await StartGameAsync(StartGameOption.BuildPunishGameOption(arguments, exeName));
        }

        public virtual async Task<bool> StartGameAsync(StartGameOption option)
        {
            try
            {
                var gameFolder = await GameLocalConfig.GetConfigAsync(
                    GameLocalSettingName.GameLauncherBassFolder
                );
                if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
                {
                    this.GameEventPublisher.Publish(
                        new GameContextOutputArgs()
                        {
                            Type = GameContextActionType.TipMessage,
                            TipMessage = "未找到游戏本体文件",
                        }
                    );
                    return false;
                }

                var executablePath = ResolveStartGameExecutablePath(gameFolder, option);
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    this.GameEventPublisher.Publish(
                        new GameContextOutputArgs()
                        {
                            Type = GameContextActionType.TipMessage,
                            TipMessage = "未找到可用的启动对象，请先在游戏设置中选择正确的启动文件",
                        }
                    );
                    return false;
                }

                Process ps = new();
                string? argument = option.ToString();
                ProcessStartInfo info = new(executablePath)
                {
                    Arguments = argument,
                    WorkingDirectory =  gameFolder,
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
                this.GameEventPublisher.Publish(
                    new GameContextOutputArgs { Type = GameContextActionType.None }
                );
                return true;
            }
            catch (Exception ex)
            {
                this._isStarting = false;
                Logger.WriteError($"游戏启动错误{ex.Message}");
                SystemEventPublisher.Publish(new() { Message = $"游戏启动错误{ex.Message}" });
                this.GameEventPublisher.Publish(
                    new GameContextOutputArgs { Type = GameContextActionType.None }
                );
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
                Logger.WriteError($"检查游戏状态失败 {ex.Message}");
                SystemEventPublisher.Publish(new() { Message = $"检查游戏状态失败 {ex.Message}" });
            }
        }

        private async Task OnGameExited()
        {
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
            Logger.WriteInfo("退出游戏……");
            this._isStarting = false;
            this.GameEventPublisher.Publish(new GameContextOutputArgs { Type = GameContextActionType.GameExit });
        }

        private static string? ResolveStartGameExecutablePath(string gameFolder, StartGameOption option)
        {
            IReadOnlyCollection<string> executableCandidates = option.Type switch
            {
                GameType.Waves => StartGameOption.GetWavesExes,
                GameType.Punish => StartGameOption.GetPunishExes,
                _ => Array.Empty<string>(),
            };

            string? selectedExe = option.Type switch
            {
                GameType.Waves => option.WavesOption?.BaseExe,
                GameType.Punish => option.PunishOption?.BaseExe,
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(selectedExe))
            {
                var selectedPath = Path.Combine(gameFolder, selectedExe);
                if (File.Exists(selectedPath))
                {
                    return selectedPath;
                }
            }

            foreach (var candidate in executableCandidates)
            {
                var candidatePath = Path.Combine(gameFolder, candidate);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return null;
        }
    }
}
