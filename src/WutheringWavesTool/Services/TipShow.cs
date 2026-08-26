using CommunityToolkit.WinUI;

namespace Haiyu.Services;

public class TipShow : ITipShow
{
    private Panel _owner;

    public Panel Owner
    {
        get { return _owner; }
        set { _owner = value; }
    }

    public void ShowMessage(string message, Symbol icon)
    {
        if (this.Owner == null)
            return; 

        var theme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        this.Owner.DispatcherQueue.TryEnqueue(() =>
        {
            PopupMessage popup = new(message, Owner, icon,theme);
            popup.ShowPopup();
        });
    }

    public async Task ShowMessageAsync(string message,Symbol icon)
    {
        if (this.Owner == null)
            return;

        var theme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        await this.Owner.DispatcherQueue.EnqueueAsync(() =>
        {
            PopupMessage popup = new(message, Owner, icon, theme);
            popup.ShowPopup();
        });
    }
}
