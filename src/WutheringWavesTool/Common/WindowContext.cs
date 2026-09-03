using CommunityToolkit.WinUI;
using Haiyu.Models.Options;
using Microsoft.UI.Dispatching;
using Waves.Core.Services;

namespace Haiyu.Common;

/// <summary>
/// WindowContext
/// </summary>
public class WindowContext
{
    /// <summary>
    /// Key
    /// </summary>
    public string Key { get; }

    private Window _window;

    /// <summary>
    /// 消息弹出框
    /// </summary>
    public ITipShow TipShow { get; set; }

    /// <summary>
    /// 对话框管理器
    /// </summary>
    public IDialogManager DialogManager { get; set; }

    /// <summary>
    /// 系统事件
    /// </summary>
    public SystemEventPublisher SystemPublisher { get; }

    /// <summary>
    /// 作用域Service
    /// </summary>
    public IServiceScope Service { get; }

    /// <summary>
    /// 当前窗口上下文选项
    /// </summary>
    public WindowManagerOption Option { get; set; }

    public async Task TryInvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(this._window);
        await SafeInvokeAsync(
                this._window.DispatcherQueue,
                action,
                priority: Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal
            )
            .ConfigureAwait(false);
    }

    async Task SafeInvokeAsync(
        DispatcherQueue dispatcher,
        Func<Task> action,
        DispatcherQueuePriority priority = DispatcherQueuePriority.Normal
    )
    {
        try
        {
            if (dispatcher.HasThreadAccess)
            {
                await action().ConfigureAwait(false);
            }
            else
            {
                await dispatcher.EnqueueAsync(action, priority).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UI操作失败: {ex.Message}");
        }
    }
}
