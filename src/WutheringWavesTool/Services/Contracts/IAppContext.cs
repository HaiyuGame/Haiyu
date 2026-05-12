namespace Haiyu.Services.Contracts;

public interface IAppContext<T>
    where T : ClientApplication
{
    /// <summary>
    /// App对象
    /// </summary>
    public T App { get; }

    /// <summary>
    /// 主窗口标题栏对象
    /// </summary>
    public Controls.TitleBar MainTitle { get; }
    public IWallpaperService WallpaperService { get; }

    /// <summary>
    /// 启动程序
    /// </summary>
    /// <param name="app">App对象</param>
    /// <returns></returns>
    public Task LauncherAsync(T app);

    /// <summary>
    /// 回调Dispatcher
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public Task TryInvokeAsync(Func<Task> action);

    public SolidColorBrush StressColor { get; }
    public Color StressShadowColor { get; }
    public SolidColorBrush StessForground { get; }
    public void TryInvoke(Action action);

    /// <summary>
    /// 初始化标题栏
    /// </summary>
    /// <param name="titleBar"></param>
    void SetTitleControl(Controls.TitleBar titleBar);

    Task UpdateAppAsync(bool isApply = false, CancellationToken token = default);

    /// <summary>
    /// 最小化主窗口
    /// </summary>
    public void Minimise();

    /// <summary>
    /// 关闭窗口
    /// </summary>
    /// <returns></returns>
    public Task CloseAsync();

    /// <summary>
    /// 最小化到任务栏
    /// </summary>
    void MinToTaskbar();
}
