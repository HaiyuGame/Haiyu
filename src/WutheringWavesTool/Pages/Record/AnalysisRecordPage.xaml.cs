using Haiyu.Controls.AnimatedTextBlock.Effects;
using Waves.Api.Models.CloudGame;

namespace Haiyu.Pages.Record;

public sealed partial class AnalysisRecordPage : Page, IWindowPage
{
    public AnalysisRecordPage()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<AnalysisRecordViewModel>();

        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
    }

    public AnalysisRecordViewModel ViewModel { get; }

    public void Dispose()
    {
    }

    public void SetData(object value)
    {
        if (value is CloudGameLoginData data)
        {

            this.ViewModel.LoginData = data;
        }
    }

    public void SetWindow(Window window)
    {
        this.titlebar.Window = window;
    }

    internal void SetData(CloudGameLoginData data)
    {
    }
}
