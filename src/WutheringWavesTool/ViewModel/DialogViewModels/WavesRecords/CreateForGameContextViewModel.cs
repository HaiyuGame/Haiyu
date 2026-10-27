using System.Text.RegularExpressions;
using System.Web;
using ABI.System;
using Haiyu.Common.Contracts;

namespace Haiyu.ViewModel.DialogViewModels.WavesRecords;

public partial class CreateForGameContextViewModel : DialogViewModelBase
{
    public CreateForGameContextViewModel(
        DialogSession dialogSession,
        IGameLogService gameLogService,
        IWindowManager windowManager,
        Haiyu.Services.WavesRecordManager wavesRecordManager
    )
        : base(dialogSession)
    {
        DialogSession = dialogSession;
        GameLogService = gameLogService;
        WindowManager = windowManager;
        WavesRecordManager = wavesRecordManager;
    }

    [GeneratedRegex(
        @"https://aki-gm-resources(-oversea)?\.aki-game\.(net|com)/aki/gacha/index\.html#/record[^""\s]*"
    )]
    public static partial Regex ParseRecordText();

    [ObservableProperty]
    public partial string RecordName { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ServerDisplay> Servers { get; set; }

    [ObservableProperty]
    public partial ServerDisplay SelectServer { get; set; }

    public DialogSession DialogSession { get; }
    public IGameLogService GameLogService { get; }
    public IWindowManager WindowManager { get; }
    public Haiyu.Services.WavesRecordManager WavesRecordManager { get; }
    public string ContextName { get; internal set; }
    public IGameContextV2 GameContext { get; private set; }

    [RelayCommand]
    async Task Loaded()
    {
        this.GameContext = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(
            this.ContextName
        );
        Servers = ServerDisplay.GetWavesV2Games;
        if (string.IsNullOrWhiteSpace(ContextName))
        {
            return;
        }
        foreach (var item in Servers)
        {
            if (this.ContextName == item.Key)
            {
                this.SelectServer = item;
            }
        }
    }

    [RelayCommand]
    async Task Invoke()
    {
        var files = await GameLogService.LocateGameLogsAsync(this.GameContext, this.CTS.Token);
        if (files == null)
        {
            await WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("未找到日志文件"),
                Symbol.Message
            );
            return;
        }
        var fileText = await GameLogService.DecryptGameLogsAsync(
            files.LogFiles.First(),
            this.CTS.Token
        );
        var link = ParseRecordText().Matches(fileText);
        if (link.Count == 0)
        {
            await WindowManager.Shell.TipShow.ShowMessageAsync(
                LanguageService.GetStringByText("未找到日志文件"),
                Symbol.Message
            );
            return;
        }
        await this.WavesRecordManager.CreateDefaultRecordAsync(
            new()
            {
                RecordName = this.RecordName,
                RecordLink = link.First().Value,
                GameContextName = this.GameContext.ContextName,
            }
        );
    }
}
