namespace Haiyu.Models.Wrapper;

public sealed partial class WebViewRuntimeWrapper : ObservableObject
{
    public required string DisplayName { get; init; }

    public required string RuntimePath { get; init; }

    /// <summary>
    /// 是否为系统 Evergreen 运行时（不可删除）。
    /// </summary>
    public bool IsEvergreen =>
        RuntimePath.Equals("Evergreen", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    public partial bool IsSelect { get; set; }

    [RelayCommand]
    private void DeleteRuntime()
    {
        if (IsEvergreen)
            return;

        WeakReferenceMessenger.Default.Send(new DeleteWebViewRuntimeMessager(this));
    }
}

/// <summary>
/// 删除本地导入的 WebView 固定运行时。
/// </summary>
public sealed record DeleteWebViewRuntimeMessager(WebViewRuntimeWrapper Runtime);
