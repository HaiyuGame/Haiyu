using Haiyu.Common.Contracts;
using Haiyu.Model;
using Haiyu.Models.Options;
using Haiyu.Pages.SortingView;
using ScoringService.Models;

namespace Haiyu.Contracts.Services;

/// <summary>评分专用窗口与对话框入口；管理与编辑窗口均为非模态。</summary>
public sealed class SortingViewFactory(IWindowManager windows)
{
    public event EventHandler? TemplatesChanged;
    public void NotifyTemplatesChanged() => TemplatesChanged?.Invoke(this, EventArgs.Empty);
    public void ShowManager() => windows.CreateWindow<SortingManagerPage>(new WindowManagerOption
    {
        Key = "SortingManager", WindowConfig = Options("声骸评分", 1180, 780)
    });
    public void ShowEditor(SortingEditorRequest request) => windows.CreateWindow<SortingTemplatePage>(new WindowManagerOption
    {
        Key = "SortingEditor:" + request.Template.Id, Parameter = request,
        WindowConfig = Options("编辑评分模板", 880, 800)
    });
    public Task<EntryItem?> ShowEntryAsync(WindowSession session, SortingRuleRequest? request = null)
    {
        var root = (session.Context.GetWindow().Content as FrameworkElement)?.XamlRoot
            ?? throw new InvalidOperationException("编辑窗口尚未就绪，请稍后重试。");
        session.Context.DialogManager.RegisterRoot(root);
        return session.Context.DialogManager.GetDialogResultAsync<SortingEntryDialog, EntryItem?>(request);
    }
    public async Task<bool> ConfirmDeleteAsync(WindowSession session, string name)
    {
        var root = (session.Context.GetWindow().Content as FrameworkElement)?.XamlRoot
            ?? throw new InvalidOperationException("窗口尚未就绪");
        var dialog = new ContentDialog
        {
            XamlRoot = root, Title = "删除模板？", Content = name,
            PrimaryButtonText = "删除", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close,
            RequestedTheme = root.Content is FrameworkElement element ? element.ActualTheme : ElementTheme.Default
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private static WindowsOption Options(string title, double width, double height) => new()
    {
        Title = title, Width = width, Height = height, MinWidth = width > 1000 ? 980 : 760, MinHeight = 640,
        IsResizable = true, IsMaximizable = true, CenterOnScreen = true, IsExtendWindowTitle = true
    };
}
