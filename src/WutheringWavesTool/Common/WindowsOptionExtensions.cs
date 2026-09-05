namespace Haiyu.Common;

public static class WindowsOptionExtensions
{
    public static void ApplyWindowsOption(this Window window, WindowsOption? option)
    {
        if (option is null)
        {
            return;
        }

        var manager = WinUIEx.WindowManager.Get(window);

        if (option.Width.HasValue)
        {
            manager.Width = option.Width.Value;
        }

        if (option.Height.HasValue)
        {
            manager.Height = option.Height.Value;
        }

        if (option.MinWidth.HasValue)
        {
            manager.MinWidth = option.MinWidth.Value;
        }

        if (option.MinHeight.HasValue)
        {
            manager.MinHeight = option.MinHeight.Value;
        }

        if (option.MaxWidth.HasValue)
        {
            manager.MaxWidth = option.MaxWidth.Value;
        }

        if (option.MaxHeight.HasValue)
        {
            manager.MaxHeight = option.MaxHeight.Value;
        }

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            if (option.IsResizable.HasValue)
            {
                presenter.IsResizable = option.IsResizable.Value;
            }

            if (option.IsMaximizable.HasValue)
            {
                presenter.IsMaximizable = option.IsMaximizable.Value;
            }

            if (option.IsMinimizable.HasValue)
            {
                presenter.IsMinimizable = option.IsMinimizable.Value;
            }
        }
    }
}
