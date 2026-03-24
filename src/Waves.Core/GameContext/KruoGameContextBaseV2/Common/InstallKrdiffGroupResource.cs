using System;
using System.Collections.Generic;
using System.Text;
using Waves.Core.Common;
using Waves.Core.Common.Downloads;
using Waves.Core.Contracts.Events;
using Waves.Core.Helpers;
using Waves.Core.Models;
using Waves.Core.Models.Downloader;
using Waves.Core.Models.Enums;
using Waves.Core.Services;

namespace Waves.Core.GameContext.KruoGameContextBaseV2.Common
{
    /// <summary>
    /// 安装库洛补丁包组资源类
    /// </summary>
    public sealed partial class InstallKrdiffGroupResource : IProgressSetup, IAsyncDisposable
    {
        private List<IndexResource> krdiffs;
        private string diffFolderPath;
        private List<GroupFileInfo> groupFileInfos;
        private string baseFolderPath;

        public InstallKrdiffGroupResource(LoggerService loggerService)
        {
            this.Logger = loggerService;
        }

        public Dictionary<string, object> Param { get; private set; }
        public LoggerService Logger { get; }
        public IGameEventPublisher GameEventPublisher { get; private set; }
        public string ProgressName { get; set; }
        public double ProgressValue { get; set; }

        public void SetParam(Dictionary<string, object> param,GameEventPublisher gameEventPublisher)
        {
            this.Param = param;
            this.GameEventPublisher = GameEventPublisher;
        }

        public async Task<bool> CheckAsync()
        {
            //补丁列表
            if (!Param.CheckParam<List<IndexResource>>("krpdiffs", out var krdiffs))
            {
                return false;
            }
            //补丁路径
            if (!Param.CheckParam<string>("diffFolderPath", out var diffFolderPath))
            {
                return false;
            }
            //游戏本体路径
            if (!Param.CheckParam<string>("baseFolderPath", out var baseFolderPath))
            {
                return false;
            }
            //分组文件信息列表
            if (!Param.CheckParam<List<GroupFileInfo>>("groupFileInfos", out var groupFileInfos))
            {
                return false;
            }
            this.krdiffs = krdiffs!;
            this.diffFolderPath = diffFolderPath!;
            this.groupFileInfos = groupFileInfos!;
            this.baseFolderPath = baseFolderPath!;
            return true;
        }

        public async Task RunAsync()
        {
            if (!await CheckAsync())
            {
                GameEventPublisher.Publish(
                    new GameContextOutputArgs
                    {
                        Type = GameContextActionType.Error,
                        TipMessage = "初始化失败"
                    }
                );
                return;
            }
            var tempFolder = Path.Combine(baseFolderPath, "decompressFolder");
            Directory.CreateDirectory(tempFolder);
            for (int i = 0; i < krdiffs.Count; i++)
            {
                var krdiffPath = BuildFileHelper.BuildFilePath(diffFolderPath, krdiffs[i]);
                IProgress<(GameContextActionType, string, KrDiffDecompressResult)> progress =
                    new Progress<(GameContextActionType, string, KrDiffDecompressResult)>(
                        (s) =>
                        {
                            GameEventPublisher.Publish(
                                new GameContextOutputArgs
                                {
                                    Type = s.Item1,
                                    CurrentSize = (long)s.Item3.PatchedCurrentBytes,
                                    TotalSize = (long)s.Item3.PatchTotalBytes,
                                    DownloadSpeed = 0,
                                    VerifySpeed = 0,
                                    IsAction = true,
                                    IsPause = false,
                                    TipMessage = "正在解压合并资源",
                                    CurrentDecompressCount = i,
                                    MaxDecompressValue = krdiffs.Count,
                                    FilePath = s.Item2,
                                    FileCurrentSize = (long)s.Item3.PatchedCurrentBytes,
                                    FileTotalSize = (long)s.Item3.PatchTotalBytes,
                                    
                                }
                            );
                            ProgressValue = s.Item3.TotalBytesProgress;
                        }
                    );
                await DiffDecompressTask.DecompressKrdiffFile(
                    baseFolderPath,
                    krdiffPath,
                    i,
                    krdiffs.Count,
                    tempFolder,
                    progress: progress
                );
                //删除源文件信息
            }
        }

        public void SetParam(
            Dictionary<string, object> param,
            IGameEventPublisher gameEventPublisher
        )
        {
            this.Param = param;
            this.GameEventPublisher = gameEventPublisher;
        }

        /// <summary>
        /// 当前解压任务不可取消，UI界面需要按钮禁用
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }
}
