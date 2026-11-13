using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Haiyu.Common.Contracts;
using Waves.Core.Models.Messanger;
using Waves.Core.Models.Options;
using Waves.Core.Models.Enums;
using Waves.Core.Services.GameResourceProvider;

namespace Haiyu.ViewModel.DialogViewModels;

public partial class SelectGameBunleViewModel : DialogViewModelBase
{
    public SelectGameBunleViewModel(DialogSession dialogSession)
        : base(dialogSession)
    {
        RegisterMessager();
    }

    private readonly Dictionary<string, GamePackageType> _packageTypes = new();

    [ObservableProperty]
    public partial Visibility PackageVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial int SelectedPackageIndex { get; set; }

    partial void OnSelectedPackageIndexChanged(int value)
    {
        foreach (var item in Bunles)
            item.PackageType = value == 1 ? GamePackageType.Minimal : GamePackageType.Full;
    }

    private void OnBundlePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(BunleResourceSize.IsSelect)
            && sender is BunleResourceSize { IsSelect: true } item)
            SelectedPackageIndex = _packageTypes.GetValueOrDefault(item.BunleName) == GamePackageType.Minimal ? 1 : 0;
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
    public partial ObservableCollection<BunleResourceSize> Bunles { get; set; } = new();

    [RelayCommand]
    async Task Loaded()
    {
        if (this.GameContext == null)
        {
            NullEnable = true;
            return;
        }
        NullEnable = false;
        InvokeEnable = false;
        try
        {
            if (GameContext.IsBunle && GameContext.GameResourceProvider is BundleGameResourceProvider provider)
            {
                var bundles = await provider.GetBunlesAsync();
                await provider.LoadPackageSizesAsync(bundles, CTS.Token);
                _packageTypes.Clear();
                foreach (var item in bundles)
                    _packageTypes[item.BunleName] = await provider.ReadPackageTypeAsync(item.BunleName, CTS.Token);
                foreach (var item in Bunles)
                    item.PropertyChanged -= OnBundlePropertyChanged;
                Bunles = bundles;
                PackageVisibility = provider.SupportsPackageType ? Visibility.Visible : Visibility.Collapsed;
                foreach (var item in Bunles)
                    item.PropertyChanged += OnBundlePropertyChanged;
            }
            var selected = await GameContext.GameLocalConfig.GetConfigAsync(GameLocalSettingName.BunleName);
            foreach (var item in Bunles)
            {
                item.IsSelect = selected == item.BunleName;
                item.Select = item.IsSelect;
            }
        }
        finally
        {
            InvokeEnable = true;
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
        var packageType = SelectedPackageIndex == 1 ? GamePackageType.Minimal : GamePackageType.Full;
        if (string.Equals(currentBundle, select.BunleName, StringComparison.Ordinal)
            && (PackageVisibility != Visibility.Visible
                || packageType == _packageTypes.GetValueOrDefault(select.BunleName)))
        {
            await this.CloseAsync(null);
            return;
        }
        if (GameContext.GameResourceProvider is BundleGameResourceProvider provider)
            await provider.SavePackageTypeAsync(select.BunleName, packageType, CTS.Token);
        var filePaths = await AppSettings.GetskipVerifyFilesAsync();
        var skipDelete = await AppSettings.GetverifySkilDeleteAsync();
        var parameter = new GameResourceParameter
        {
            BundleName = select.BunleName,
            PackageType = PackageVisibility == Visibility.Visible ? packageType : null
        };
        _ = Task.Run(async () =>
        {
            await GameContext.RepairGameAsync(
                isDelete: !skipDelete,
                skipFilePath: filePaths,
                parameter: parameter);
        });
        await CloseAsync(select);
    }
}
