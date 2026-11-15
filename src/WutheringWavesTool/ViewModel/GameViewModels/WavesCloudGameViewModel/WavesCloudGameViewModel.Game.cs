using Haiyu.Helpers;
using Waves.Api.Models.CloudGame;

namespace Haiyu.ViewModel.GameViewModels;

partial class WavesCloudGameViewModel
{
    [ObservableProperty]
    public partial string BottomText { get; set; } = LanguageService.GetString("ViewModel_Ready")!;

    [ObservableProperty]
    public partial string StartGameText { get; set; } = LanguageService.GetString("Display_MasukKeGame")!;

    private async Task StartGameAsync(CancellationToken token)
    {
        var session = await WavesCloudGameService.GetCurrentUserSession();
        token.ThrowIfCancellationRequested();
        if (session is null)
        {
            await ShowStartErrorAsync(LanguageService.GetStringByText("请在左侧卡片登录一个账号"));
            return;
        }

        var wallet = await WavesCloudGameService.GetWalletDataAsync(session, token);
        if (wallet?.Data is not { } balance)
        {
            await ShowStartErrorAsync(wallet?.Msg ?? LanguageService.GetStringByText("获取余额失败！"));
            return;
        }

        var autoSelect = KuroCloudGameContext.AutoSelectLowestLatencyNode;
        var selection = autoSelect
            ? await SelectFastestNodeAsync(session, token)
            : await WindowManager.Shell.DialogManager.ShowSelectGameNodeAsync(session.GetId());
        token.ThrowIfCancellationRequested();
        if (selection?.SelectNode is not { } node)
        {
            if (autoSelect)
                BottomText = LanguageService.GetString("CloudGame_NoMeasuredNode");
            else
                await ShowStartErrorAsync(LanguageService.GetStringByText("请选择节点或节点失效"));

            return;
        }

        var options = await GetOptionsAsync();
        if (options is null)
        {
            await ShowStartErrorAsync(LanguageService.GetStringByText("构建清晰度失败，日志已记录"));
            return;
        }

        token.ThrowIfCancellationRequested();
        await KuroCloudGameContext.StartGameAsync(
            session,
            selection.Nodes,
            node,
            options,
            GetDefaultPayType(balance)
        );
    }

    private async Task<LauncheNodeConfig?> SelectFastestNodeAsync(
        CloudGameLoginSession session,
        CancellationToken token
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));

        var previousText = BottomText;
        var testingText = LanguageService.GetString("CloudGame_TestingNodes");
        BottomText = testingText;
        try
        {
            return await WavesCloudGameService.GetFastestGameNodeAsync(session, timeout.Token);
        }
        finally
        {
            if (IsAlive && BottomText == testingText)
                BottomText = previousText;
        }
    }

    private Task ShowStartErrorAsync(string message) =>
        WindowManager.Shell.TipShow.ShowMessageAsync(message, Symbol.Clear);
}
