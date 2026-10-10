using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Haiyu.Common.Contracts;
using Waves.Core.Models.Messanger;
using Waves.Core.Models.Options;
using Waves.Core.Services.GameResourceProvider;

namespace Haiyu.ViewModel.DialogViewModels;

public partial class SelectGameBunleViewModel : DialogViewModelBase
{
    public SelectGameBunleViewModel(DialogSession dialogSession)
        : base(dialogSession)
    {
        RegisterMessager();
    }

    [ObservableProperty]
    public partial bool InvokeEnable { get; set; } = true;

    [ObservableProperty]
    public partial double Progress { get; set; }

    private void RegisterMessager()
    {
        this.Messenger.Register<DeleteBunleGameResourceMessager>(this, DeleteBunleGameResource);
    }

    private async void DeleteBunleGameResource(
        object recipient,
        DeleteBunleGameResourceMessager message
    )
    {
        IProgress<double> progress = new Progress<double>(
            (s) =>
            {
                this.InvokeEnable = false;
                this.Progress = s;
            }
        );
        if (
            this.GameContext.IsBunle
            && this.GameContext.GameResourceProvider is BundleGameResourceProvider p
        )
        {
            await p.DeleteResourcePackFilesAsync(message.BunleName, progress, this.CTS.Token);
            this.InvokeEnable = true;
        }
    }

    /// <summary>
    /// 中断用户操作
    /// </summary>
    [ObservableProperty]
    public partial bool NullEnable { get; set; }

    public IGameContextV2 GameContext { get; private set; }

    internal void SwitchCore(string contextName)
    {
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(
            contextName
        );
    }

    [ObservableProperty]
    public partial ObservableCollection<BunleResourceSize> Bunles { get; set; }

    [RelayCommand]
    async Task Loaded()
    {
        if (this.GameContext == null)
        {
            NullEnable = true;
            return;
        }
        NullEnable = false;
        if (GameContext.IsBunle && GameContext.GameResourceProvider is BundleGameResourceProvider p)
        {
            this.Bunles = await p.GetBunlesAsync();
        }
        var selectQuality = await this.GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.BunleName
        );
        foreach (var item in Bunles)
        {
            if (selectQuality == item.BunleName)
            {
                item.IsSelect = true;
                item.Select = true;
            }
        }
    }

    [RelayCommand]
    async Task Invoke()
    {
        var select = this.Bunles.FirstOrDefault(x => x.IsSelect);
        if (select == null)
            return;
        var currentBundle = await GameContext.GameLocalConfig.GetConfigAsync(
            GameLocalSettingName.BunleName
        );
        if (string.Equals(currentBundle, select.BunleName, StringComparison.Ordinal))
        {
            await this.CloseAsync(null);
            return;
        }
        var arguments =
            await GameContext.GameLocalConfig.GetConfigAsync(
                GameLocalSettingName.StartGameArguments
            ) ?? "";
        arguments = System
            .Text.RegularExpressions.Regex.Replace(
                arguments,
                @"(?<!\S)-krqlv=(?:""[^""]*""|\S+)",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            )
            .Trim();
        var quality = string.IsNullOrWhiteSpace(select.ResourceCommand)
            ? select.BunleName.ToLowerInvariant()
            : select.ResourceCommand.ToLowerInvariant();
        arguments = string.IsNullOrWhiteSpace(arguments)
            ? $"-krqlv={quality}"
            : $"{arguments} -krqlv={quality}";
        if (
            !await GameContext.GameLocalConfig.SaveConfigsAsync(
                new Dictionary<string, string>
                {
                    [GameLocalSettingName.BunleName] = select.BunleName,
                    [GameLocalSettingName.StartGameArguments] = arguments,
                }
            )
        )
            return;
        var filePaths = await AppSettings.GetskipVerifyFilesAsync();
        var skipDelete = await AppSettings.GetverifySkilDeleteAsync();
        if (
            !await GameContext.GameLocalConfig.SaveConfigAsync(
                GameLocalSettingName.GetBunlePackVersionKey(select.BunleName),
                select.Version
            )
        )
            return;

        _ = Task.Run(async () =>
            await GameContext.RepairGameAsync(
                isDelete: !skipDelete,
                skipFilePath: filePaths,
                parameter: new GameResourceParameter { BundleName = select.BunleName }
            )
        );
        await CloseAsync(select);
    }
}
