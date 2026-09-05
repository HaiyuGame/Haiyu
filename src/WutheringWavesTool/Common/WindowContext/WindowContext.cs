using CommunityToolkit.WinUI;
using Haiyu.Models.Options;
using Microsoft.UI.Dispatching;
using Waves.Core.Services;

namespace Haiyu.Common.WindowContext;

/// <summary>
/// WindowContext
/// </summary>
public class WindowContext : IDisposable
{
    /// <summary>
    /// Key
    /// </summary>
    public string Key { get; }

    private Window _window;

    public Window GetWindow() => _window;

    public void SetWindow(Window window)
    {
        this._window = window;
    }

    public WindowContext(IServiceScope service, string key)
    {
        Service = service;
        this.TipShow = service.ServiceProvider.GetRequiredService<ITipShow>();
        this.DialogManager = service.ServiceProvider.GetRequiredService<IDialogManager>();
        this.SystemPublisher = service.ServiceProvider.GetRequiredService<SystemEventPublisher>();
        this.PickersService = service.ServiceProvider.GetRequiredService<IPickersService>();
        Key = key;
    }

    /// <summary>
    /// 消息弹出框
    /// </summary>
    public ITipShow TipShow { get; }

    /// <summary>
    /// 对话框管理器
    /// </summary>
    public IDialogManager DialogManager { get; }

    public IPickersService PickersService { get; }

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
    public WindowManagerOption Option { get; }

    /// <summary>
    /// 最小化
    /// </summary>
    public void Minimize()
    {
        ArgumentNullException.ThrowIfNull(this._window);
        this._window.Minimize();
    }

    /// <summary>
    /// 关闭
    /// </summary>
    public void Close()
    {
        ArgumentNullException.ThrowIfNull(this._window);
        this._window.Close();
    }

    public void Show()
    {
        ArgumentNullException.ThrowIfNull(this._window);
        _window.Show();
    }

    /// <summary>
    /// 关闭
    /// </summary>
    public void Hide()
    {
        ArgumentNullException.ThrowIfNull(this._window);
        this._window.Hide();
    }

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

    public void TryInvoke(Action action)
    {
        this._window.DispatcherQueue.TryEnqueue(() => action.Invoke());
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

    public void Dispose()
    {
        this.Service.Dispose();
    }
}
