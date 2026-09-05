namespace Haiyu.Services.Contracts;

public interface IAppContext<T>
    where T : Application
{
    /// <summary>
    /// App对象
    /// </summary>
    public T App { get; }

    public IWallpaperService WallpaperService { get; }

    public IWindowManager WindowManager { get; }

    public ABIRuntimeService ABIRuntimeService { get; }

    /// <summary>
    /// 启动程序
    /// </summary>
    /// <param name="app">App对象</param>
    /// <returns></returns>
    public Task LauncherAsync(T app);


    Task UpdateAppAsync(bool isApply = false, CancellationToken token = default);


}
