using Haiyu.Plugin.Extensions;

namespace Haiyu.ViewModel;

partial class SettingViewModel
{
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ModifierKey> CaptureModifierKeys { get; set; } =
        ModifierKey.GetDefault().ToObservableCollection();

    [ObservableProperty]
    public partial ModifierKey CaptureModifierKey { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<Keys> CaptureKeys { get; set; } =
        Plugin.Extensions.Keys.GetDefault().ToObservableCollection();


    [ObservableProperty]
    public partial Keys CaptureKey { get; set; }


    partial void OnCaptureModifierKeyChanged(ModifierKey value)
    {
        AppSettings.SetCaptureModifierKeyAsync(value.Name).GetAwaiter().GetResult();
        var result = ScreenCaptureService.Register();
        this.TipShow.ShowMessage(result.Item2, Symbol.Read);
    }

    partial void OnIsOnChanged(bool value)
    {
        AppSettings.SetIsCaptureAsync(value.ToString()).GetAwaiter().GetResult();

    }

    partial void OnCaptureKeyChanged(Keys value)
    {
        AppSettings.SetCaptureKeyAsync(value.Name).GetAwaiter().GetResult();
        var result = ScreenCaptureService.Register();
        this.TipShow.ShowMessage(result.Item2, Symbol.Read);
    }

    public async Task InitCapture()
    {
        try
        {
            var isCapture = await AppSettings.GetIsCaptureAsync();
            var captureModifierKey = await AppSettings.GetCaptureModifierKeyAsync();
            var captureKey = await AppSettings.GetCaptureKeyAsync();
            this.IsOn = isCapture == null ? true : Boolean.Parse(isCapture);
            if (string.IsNullOrWhiteSpace(captureModifierKey) || string.IsNullOrWhiteSpace(captureKey))
            {
                this.CaptureModifierKey = this.CaptureModifierKeys.Where(x => x.Name == "Win").First();
                this.CaptureKey = this.CaptureKeys.Where(x => x.Name == "F12").First();
            }
            else
            {
                this.CaptureModifierKey = this.CaptureModifierKeys.Where(x => x.Name == captureModifierKey).First();
                this.CaptureKey = this.CaptureKeys.Where(x => x.Name == captureKey).First();
            }
        }
        catch (Exception ex)
        {
            TipShow.ShowMessage(LanguageService.FormatByText(LanguageService.GetStringByText("注册失败{0}"), ex.Message), Symbol.Clear);
        }
    }
}
