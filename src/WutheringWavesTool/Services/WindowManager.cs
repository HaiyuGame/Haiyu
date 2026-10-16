using Haiyu.Common.Contracts;
using Haiyu.Common.WindowContext;
using Haiyu.Models.Options;

namespace Haiyu.Services;

public sealed class WindowManager : IWindowManager
{
    public WindowManager(AppSettings appSettings)
    {
        AppSettings = appSettings;
    }

    private readonly Dictionary<string, WindowContext> _windowContext = new();

    public ShellWindowContext Shell
    {
        get
        {
            var context = _windowContext.GetValueOrDefault("Shell");
            if (context is ShellWindowContext shellC && shellC.Key == IWindowManager.ShellKey)
            {
                return shellC;
            }
            return default;
        }
    }

    public AppSettings AppSettings { get; }

    public async Task CreateShellWindowAsync()
    {
        if (this.Shell != null)
            return;
        WindowEx winEx = new WindowEx();
        winEx.Title = "Haiyu";
        winEx.AppWindow.SetIcon(AppDomain.CurrentDomain.BaseDirectory + "Assets/appLogo.ico");
        NativeWindowHelper.ForceDisableMaximize(winEx, targetDipWidth: 1150, targetDipHeight: 650);
        winEx.SystemBackdrop = new MicaBackdrop();
        (winEx.AppWindow.Presenter as OverlappedPresenter)!.SetBorderAndTitleBar(true, false);
        var shell = new ShellWindowContext(
            Instance.Host.Services.CreateAsyncScope(),
            IWindowManager.ShellKey
        );
        shell.SetWindow(winEx);
        this._windowContext.Add(shell.Key, shell);
        #region Config
        var mainSizeConfig = await this.AppSettings.GetMainWindowSettingsAsync();
        if (mainSizeConfig == null)
        {
            mainSizeConfig = MainWindowSetting.Default;
        }
        #endregion
        #region Page
        if (await AppSettings.GetAutoOOBEAsync() == true)
        {
            var page = shell.Service.ServiceProvider.GetRequiredService<OOBEPage>();
            page.titlebar.Window = this.Shell.GetWindow();
            this.Shell.GetWindow().Content = page;
            this.Shell.GetWindow().ApplyWindowsOption(WindowsOption.OOBEWindowOption);
        }
        else
        {
            var page = shell.Service.ServiceProvider.GetRequiredService<ShellPage>();
            page.titlebar.Window = this.Shell.GetWindow();
            this.Shell.GetWindow().Content = page;
            var defaultOption = WindowsOption.DefaultWindowsOption;
            var widthRate =
                double.IsFinite(mainSizeConfig.WidthRate) && mainSizeConfig.WidthRate > 0
                    ? mainSizeConfig.WidthRate
                    : MainWindowSetting.Default.WidthRate;
            var heightRate =
                double.IsFinite(mainSizeConfig.HeightRate) && mainSizeConfig.HeightRate > 0
                    ? mainSizeConfig.HeightRate
                    : MainWindowSetting.Default.HeightRate;

            this.Shell.GetWindow()
                .ApplyWindowsOption(
                    defaultOption with
                    {
                        Width = defaultOption.Width * widthRate,
                        Height = defaultOption.Height * heightRate,
                        IsResizable = mainSizeConfig.IsResize,
                    }
                );
        }
        #endregion
        winEx.Activate();
    }



    public Task RemoveShellWindowAsync()
    {
        if (!_windowContext.TryGetValue(IWindowManager.ShellKey, out var context))
            return Task.CompletedTask;

        var window = context.GetWindow();

        try
        {
            // Tear down the managed/XAML graph while the Window and its dispatcher
            // are still valid. Disposing the scope from Closed runs too late for
            // controls that detach XAML events or compiled bindings in Dispose().
            if (window.Content is IDisposable disposableContent)
            {
                disposableContent.Dispose();
            }

            window.Content = null;
            context.Dispose();
            _windowContext.Remove(IWindowManager.ShellKey);
            window.Close();
        }
        catch
        {
            _windowContext.Remove(IWindowManager.ShellKey);
            context.Dispose();
            throw;
        }

        return Task.CompletedTask;
    }

    public void CreateWindow<T>(WindowManagerOption managerOption)
        where T : UIElement
    {
        CreateWindowCore<T>(managerOption);
    }

    public async Task<TResult?> CreateWindowAsync<T, TResult>(
        WindowManagerOption managerOption)
        where T : UIElement
    {
        var session = CreateWindowCore<T>(managerOption);
        return await session.GetResultAsync<TResult>();
    }

    private WindowSession CreateWindowCore<T>(WindowManagerOption managerOption)
        where T : UIElement
    {
        ArgumentNullException.ThrowIfNull(managerOption);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerOption.Key);

        if (_windowContext.TryGetValue(managerOption.Key, out var existingContext))
        {
            existingContext.Show();
            existingContext.GetWindow().Activate();
            return existingContext.Service.ServiceProvider.GetRequiredService<WindowSession>();
        }

        var scope = Instance.Host.Services.CreateAsyncScope();
        WindowContext? context = null;
        WindowEx? window = null;
        WindowSession? session = null;

        try
        {
            window = new WindowEx()
            {
                SystemBackdrop = new MicaBackdrop(),
            };
            window.ApplyWindowsOption(managerOption.WindowConfig);
            context = new WindowContext(scope, managerOption.Key)
            {
                Option = managerOption,
            };

            context.SetWindow(window);

            if (!_windowContext.TryAdd(context.Key, context))
            {
                throw new InvalidOperationException($"窗口 Key 已存在：{context.Key}");
            }

            session = scope.ServiceProvider.GetRequiredService<WindowSession>();

            session.Attach(window,context);

            var page = scope.ServiceProvider.GetRequiredService<T>();
            window.Content = page;

            window.Closed += Window_Closed;
            window.Activate();

            return session;

            void Window_Closed(object sender, WindowEventArgs args)
            {
                try
                {
                    if (window is IWindowInitializable initializable)
                    {
                        initializable.Dispose();
                    }
                }
                finally
                {
                    window.Closed -= Window_Closed;
                    session.Detach();
                    _windowContext.Remove(context.Key);
                    context.Dispose();
                }
            }
        }
        catch
        {
            if (context is not null)
            {
                _windowContext.Remove(context.Key);
            }

            try
            {
                session?.Detach();
                window?.Close();
            }
            finally
            {
                scope.Dispose();
            }
            throw;
        }
    }

    public WindowSession CreateTransparentWindow<T>(PostionTransparentWindowOption managerOption)
        where T:UIElement
    {
        ArgumentNullException.ThrowIfNull(managerOption);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerOption.Key);

        if (_windowContext.TryGetValue(managerOption.Key, out var existingContext))
        {
            existingContext.Show();
            existingContext.GetWindow().Activate();
            return existingContext.Service.ServiceProvider.GetRequiredService<WindowSession>();
        }

        var scope = Instance.Host.Services.CreateAsyncScope();
        PostionTransparentWindowContext? context = null;
        TransparentWindowBase? window = null;
        WindowSession? session = null;
        T? page = null;

        try
        {
            window = new TransparentWindowBase();
            context = new PostionTransparentWindowContext(scope, managerOption.Key)
            {
                Option = managerOption,
            };

            context.SetWindow(window);

            if (!_windowContext.TryAdd(context.Key, context))
            {
                throw new InvalidOperationException($"窗口 Key 已存在：{context.Key}");
            }

            session = scope.ServiceProvider.GetRequiredService<WindowSession>();

            session.Attach(window, context);

            page = scope.ServiceProvider.GetRequiredService<T>();
            window.Content = page;

            context.ApplyOption();

            if (page is IWindowInitializable initializable)
            {
                initializable.Initialize();
            }

            window.Closed += TransparentWindow_Closed;
            window.Activate();

            return session;

            void TransparentWindow_Closed(object sender, WindowEventArgs args)
            {
                try
                {
                    if (page is IWindowInitializable initializable)
                    {
                        initializable.Dispose();
                    }

                    window.Content = null;
                }
                finally
                {
                    window.Closed -= TransparentWindow_Closed;
                    session.Detach();
                    _windowContext.Remove(context.Key);
                    context.Dispose();
                }
            }
        }
        catch
        {
            if (context is not null)
            {
                _windowContext.Remove(context.Key);
            }

            try
            {
                if (page is IWindowInitializable initializable)
                {
                    initializable.Dispose();
                }

                if (window is not null)
                {
                    window.Content = null;
                }

                session?.Detach();
                window?.Close();
            }
            finally
            {
                scope.Dispose();
            }
            throw;
        }
    }

    public void CreateWindowBase<T>(WindowManagerOption managerOption, nint ownerId)
        where T : UIElement
    {
        CreateWindowBaseCore<T>(managerOption, ownerId);
    }

    public async Task<TResult?> CreateWindowBaseAsync<T, TResult>(
        WindowManagerOption managerOption,
        nint ownerId)
        where T : UIElement
    {
        var session = CreateWindowBaseCore<T>(managerOption, ownerId);
        return await session.GetResultAsync<TResult>();
    }

    private WindowSession CreateWindowBaseCore<T>(
        WindowManagerOption managerOption,
        nint ownerId)
        where T : UIElement
    {
        ArgumentNullException.ThrowIfNull(managerOption);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerOption.Key);

        if (_windowContext.TryGetValue(managerOption.Key, out var existingContext))
        {
            existingContext.Show();
            existingContext.GetWindow().Activate();
            return existingContext.Service.ServiceProvider.GetRequiredService<WindowSession>();
        }

        var scope = Instance.Host.Services.CreateAsyncScope();
        WindowModelContext? context = null;
        WindowModelBase? window = null;
        WindowSession? session = null;

        try
        {
            window = new WindowModelBase(ownerId, managerOption.WindowConfig)
            {
                SystemBackdrop = new MicaBackdrop(),
            };

            context = new WindowModelContext(scope, managerOption.Key)
            {
                OwnerId = ownerId,
                Option = managerOption,
            };
            context.SetWindow(window);

            if (!_windowContext.TryAdd(context.Key, context))
            {
                throw new InvalidOperationException($"窗口 Key 已存在：{context.Key}");
            }

            session = scope.ServiceProvider.GetRequiredService<WindowSession>();
            session.Attach(window,context);

            var page = scope.ServiceProvider.GetRequiredService<T>();
            window.Content = page;

            window.Closed += Window_Closed;
            window.AppWindow.Show();

            return session;

            void Window_Closed(object sender, WindowEventArgs args)
            {
                try
                {
                    window.Closed -= Window_Closed;
                    session.Detach();
                }
                finally
                {
                    _windowContext.Remove(context.Key);
                    context.Dispose();
                }
            }
        }
        catch
        {
            if (context is not null)
            {
                _windowContext.Remove(context.Key);
            }

            try
            {
                session?.Detach();
                window?.Close();
            }
            finally
            {
                scope.Dispose();
            }
            throw;
        }
    }

    public void CreateOriginWindow<T>(WindowManagerOption managerOption)
        where T :Window
    {
        CreateOriginWindowCore<T>(managerOption);
    }

    public async Task<TResult?> CreateOriginWindowAsync<T, TResult>(
        WindowManagerOption managerOption)
        where T : Window
    {
        var session = CreateOriginWindowCore<T>(managerOption);
        return await session.GetResultAsync<TResult>();
    }

    private WindowSession CreateOriginWindowCore<T>(WindowManagerOption managerOption)
        where T : Window
    {
        ArgumentNullException.ThrowIfNull(managerOption);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerOption.Key);

        if (_windowContext.TryGetValue(managerOption.Key, out var existingContext))
        {
            existingContext.Show();
            existingContext.GetWindow().Activate();
            return existingContext.Service.ServiceProvider.GetRequiredService<WindowSession>();
        }

        var scope = Instance.Host.Services.CreateAsyncScope();
        WindowContext? context = null;
        WindowSession? session = null;
        Window? window = null;
        try
        {

            window = scope.ServiceProvider.GetRequiredService<T>();
            window.ApplyWindowsOption(managerOption.WindowConfig);

            context = new WindowContext(scope, managerOption.Key)
            {
                Option = managerOption,
            };
            context.SetWindow(window);

            session = scope.ServiceProvider.GetRequiredService<WindowSession>();
            session.Attach(window, context);

            if (!_windowContext.TryAdd(context.Key, context))
            {
                throw new InvalidOperationException($"窗口 Key 已存在：{context.Key}");
            }

            if (window is IWindowInitializable initializable)
            {
                initializable.Initialize();
            }

            window.Closed += Window_Closed;
            window.Activate();

            return session;

            void Window_Closed(object sender, WindowEventArgs args)
            {
                try
                {
                    if (window is IWindowInitializable initializable)
                    {
                        initializable.Dispose();
                    }
                }
                finally
                {
                    window.Closed -= Window_Closed;
                    session.Detach();
                    _windowContext.Remove(context.Key);
                    context.Dispose();
                }
            }
        }
        catch
        {
            if (context is not null)
            {
                _windowContext.Remove(context.Key);
            }

            try
            {
                session?.Detach();
                window?.Close();
            }
            finally
            {
                scope.Dispose();
            }
            throw;
        }
    }


    public Task<IEnumerable<WindowContext>> GetWindowContextsAsync() =>
        Task.FromResult(_windowContext.Values.AsEnumerable());

    public bool IsWindowShow(string key)
    {
        var context = _windowContext.GetValueOrDefault(key);
        if (context is null)
            return false;
        var window = context.GetWindow();
        if (window is null)
            return false;
        return window.AppWindow.IsVisible;
    }

    public WindowContext? GetWindowContext(string key)
    {
        return _windowContext.GetValueOrDefault(key);
    }
}
