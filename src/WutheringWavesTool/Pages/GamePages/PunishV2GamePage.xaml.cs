using CommunityToolkit.WinUI.Animations;
using Haiyu.Common.Contracts;
using Haiyu.ViewModel.GameViewModels.GameContexts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Waves.Core.GameContext.ContextsV2;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Haiyu.Pages.GamePages;

public sealed partial class PunishV2GamePage : Page,IPage
{
    public Type PageType => typeof(PunishV2GamePage);

    public PunishV2GamePage()
    {
        InitializeComponent();
        ViewModel = Instance.GetService<PunishV2GameContextViewModel>()!;
    }


    public PunishV2GameContextViewModel? ViewModel { get; set; }

    public System.Collections.ObjectModel.ObservableCollection<PunishServerPreview> ServerPreviews { get; } = new();
    private readonly CancellationTokenSource _serverPreviewLifetime = new();

    private bool _serverSelectorOpen;
    private Storyboard? _serverSelectorAnimation;

    private void AnimateServerSelector(bool open)
    {
        var from = ServerSelectorMenu.ActualHeight;
        var scrimOpacity = ServerSelectorScrim.Opacity;
        _serverSelectorAnimation?.Stop();
        ServerSelectorMenu.Height = from;
        _serverSelectorOpen = open;
        ServerSelectorMenu.IsHitTestVisible = open;
        var animation = new DoubleAnimation
        {
            From = from,
            To = open ? 140 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, ServerSelectorMenu);
        Storyboard.SetTargetProperty(animation, "Height");
        _serverSelectorAnimation = new Storyboard();
        _serverSelectorAnimation.Children.Add(animation);
        var dimAnimation = new DoubleAnimation
        {
            From = scrimOpacity,
            To = open ? 1 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(220))
        };
        Storyboard.SetTarget(dimAnimation, ServerSelectorScrim);
        Storyboard.SetTargetProperty(dimAnimation, "Opacity");
        _serverSelectorAnimation.Children.Add(dimAnimation);
        ServerSelectorScrim.IsHitTestVisible = open;
        _serverSelectorAnimation.Begin();
        ServerSelectorChevron.Glyph = open ? "\uE70E" : "\uE70D";
        CollapsedServerContent.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        CollapseServerLabel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ServerSelectorMenuSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ServerCards.Width = Math.Max(0, Math.Min(1040, e.NewSize.Width - 32));
        ((Microsoft.UI.Xaml.Media.RectangleGeometry)ServerSelectorMenu.Clip).Rect =
            new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
    }

    private void ServerCardPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        => AnimateServerCardBackground(sender, 1.5);

    private void ServerCardPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        => AnimateServerCardBackground(sender, 1);

    private static void AnimateServerCardBackground(object sender, double scale)
    {
        if (sender is not Grid card || card.Children.OfType<Image>().FirstOrDefault() is not Image image)
            return;

        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(image);
        visual.CenterPoint = new System.Numerics.Vector3(
            (float)image.ActualWidth / 2, (float)image.ActualHeight / 2, 0);
        // Omit From so rapid enter/exit continues from the current scale without jumping.
        AnimationBuilder.Create()
            .Scale(to: scale, duration: TimeSpan.FromMilliseconds(300),
                easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
            .Start(image);
    }

    private void ToggleServerSelector(object sender, RoutedEventArgs e)
    {
        if (_serverSelectorOpen)
        {
            CloseServerSelector();
            return;
        }
        if (ServerPreviews.Count == 0 && ViewModel is not null)
        {
            foreach (var server in ViewModel.Servers)
                ServerPreviews.Add(new PunishServerPreview(server));
            _ = LoadServerPreviewsAsync(_serverPreviewLifetime.Token);
        }
        foreach (var preview in ServerPreviews)
            preview.IsCurrent = preview.Server == ViewModel?.SelectServer;
        AnimateServerSelector(true);
    }

    private void CloseServerSelector()
    {
        AnimateServerSelector(false);
        ServerSelectorButton.Focus(FocusState.Programmatic);
    }


    private void DismissServerSelector(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        => CloseServerSelector();

    private void ServerSelectorKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CloseServerSelector();
            e.Handled = true;
        }
    }

    private void SelectServerCard(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PunishServerPreview preview && ViewModel is not null)
            ViewModel.SelectServer = preview.Server;
        CloseServerSelector();
    }

    private void SelectServerTab(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ServerDisplay server } && ViewModel is not null)
        {
            ViewModel.SelectServer = server;
            CloseServerSelector();
        }
    }

    private async Task LoadServerPreviewsAsync(CancellationToken token)
    {
        await Task.WhenAll(ServerPreviews.Select(async preview =>
        {
            try
            {
                var context = Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(preview.Server.Key);
                var index = await context.GetDefaultLauncherValue(token);
                if (index is null) { preview.Status = "暂无背景预览"; return; }
                var background = await context.GetLauncherBackgroundDataAsync(index.FunctionCode.Background, token);
                token.ThrowIfCancellationRequested();
                // Decode a thumbnail, never decode the full launcher wallpaper for a card.
                var imageUrl = background?.FirstFrameImage;
                if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                {
                    preview.Status = "暂无背景预览";
                    return;
                }
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage
                {
                    DecodePixelWidth = 680,
                    DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Physical
                };
                bitmap.ImageFailed += (_, _) => preview.Status = "背景加载失败";
                bitmap.UriSource = uri;
                preview.Background = bitmap;
                preview.Status = "";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) preview.Status = "背景加载失败";
                System.Diagnostics.Debug.WriteLine($"Server preview {preview.Server.Key}: {ex.Message}");
            }
        }));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        try
        {
            _serverSelectorAnimation?.Stop();
            _serverPreviewLifetime.Cancel();
            this.Bindings.StopTracking();
            this.ViewModel?.Dispose();
        }
        finally
        {
            this.ViewModel = null;
            base.OnNavigatedFrom(e);
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        switcher.Switch();
        if(switcher.CurrentIndex == 1)
        {
            filpViewAutoPlay.IsPlay = true;
        }
        else
        {
            filpViewAutoPlay.IsPlay = false;
        }
    }
}


public partial class PunishServerPreview : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public PunishServerPreview(ServerDisplay server) => Server = server;
    public ServerDisplay Server { get; }
    public string Display => Server.Display;
    public string Region => Server.Tag switch
    {
        "Main" => LanguageService.GetString("Game_OfficialSubtitle"),
        "BiliBili" => LanguageService.GetString("Game_BiliBiliSubtitle"),
        "Global" => LanguageService.GetString("Game_GlobalSubtitle"),
        "Tw" => LanguageService.GetString("Game_TwOfficialSubtitle"),
        _ => Server.Display
    };
    [ObservableProperty]
    public partial Microsoft.UI.Xaml.Media.ImageSource? Background { get; set; }
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
    [ObservableProperty]
    public partial string Status { get; set; } = "正在加载背景…";
}
