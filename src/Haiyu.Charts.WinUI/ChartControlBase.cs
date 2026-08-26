using System.Collections.Specialized;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI;

namespace Haiyu.Charts.WinUI;

public abstract partial class ChartControlBase : Grid, IDisposable
{
    protected CanvasControl? Canvas { get; private set; }
    private readonly TextBlock _tooltipText = new() { TextWrapping = TextWrapping.NoWrap };
    private readonly Border _tooltipPresenter;
    protected Point PointerPosition;
    protected bool IsDragging;
    protected Point DragStart;
    protected double ZoomX = 1, ZoomY = 1, PanX, PanY;
    private bool _active;
    private bool _disposed;
    private readonly DispatcherTimer _animationTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private DateTime _animationStarted;
    protected double AnimationProgress { get; private set; } = 1;

    protected ChartControlBase()
    {
        _tooltipPresenter = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6, 10, 6),
            Child = _tooltipText,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        UpdateTooltipTheme();
        Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(_tooltipPresenter, 100);
        Children.Add(_tooltipPresenter);
        IsTabStop = true;
        ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY | ManipulationModes.Scale;
        AutomationProperties.SetName(this, "Interactive chart");
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnKeyDown;
        ManipulationDelta += OnManipulationDelta;
        ActualThemeChanged += OnActualThemeChanged;
        _animationTimer.Tick += OnAnimationTick;
    }

    public static readonly DependencyProperty ZoomModeProperty = DependencyProperty.Register(nameof(ZoomMode), typeof(ChartZoomMode), typeof(ChartControlBase), new PropertyMetadata(ChartZoomMode.None));
    public static readonly DependencyProperty PanModeProperty = DependencyProperty.Register(nameof(PanMode), typeof(ChartPanMode), typeof(ChartControlBase), new PropertyMetadata(ChartPanMode.None));
    public static readonly DependencyProperty TooltipModeProperty = DependencyProperty.Register(nameof(TooltipMode), typeof(ChartTooltipMode), typeof(ChartControlBase), new PropertyMetadata(ChartTooltipMode.NearestPoint));
    public static readonly DependencyProperty LegendPositionProperty = DependencyProperty.Register(nameof(LegendPosition), typeof(ChartLegendPosition), typeof(ChartControlBase), new PropertyMetadata(ChartLegendPosition.Bottom, Changed));
    public static readonly DependencyProperty AnimationsEnabledProperty = DependencyProperty.Register(nameof(AnimationsEnabled), typeof(bool), typeof(ChartControlBase), new PropertyMetadata(true));

    public ChartZoomMode ZoomMode { get => (ChartZoomMode)GetValue(ZoomModeProperty); set => SetValue(ZoomModeProperty, value); }
    public ChartPanMode PanMode { get => (ChartPanMode)GetValue(PanModeProperty); set => SetValue(PanModeProperty, value); }
    public ChartTooltipMode TooltipMode { get => (ChartTooltipMode)GetValue(TooltipModeProperty); set => SetValue(TooltipModeProperty, value); }
    public ChartLegendPosition LegendPosition { get => (ChartLegendPosition)GetValue(LegendPositionProperty); set => SetValue(LegendPositionProperty, value); }
    public bool AnimationsEnabled { get => (bool)GetValue(AnimationsEnabledProperty); set => SetValue(AnimationsEnabledProperty, value); }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed || _active) return;
        UpdateTooltipTheme();
        var canvas = new CanvasControl { ClearColor = Colors.Transparent };
        Canvas = canvas;
        Children.Insert(0, canvas);
        canvas.Draw += DrawCanvas;
        canvas.PointerMoved += OnPointerMoved;
        canvas.PointerPressed += OnPointerPressed;
        canvas.PointerReleased += OnPointerReleased;
        canvas.PointerExited += OnPointerExited;
        canvas.PointerWheelChanged += OnPointerWheelChanged;
        canvas.DoubleTapped += OnDoubleTapped;
        SetDataSubscriptions(true);
        _active = true;
        BeginAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => ReleaseRenderingResources();

    private void ReleaseRenderingResources()
    {
        if (!_active && Canvas is null) return;
        _active = false;
        _animationTimer.Stop();
        AnimationProgress = 1;
        HideTooltip();
        SetDataSubscriptions(false);
        ClearRenderState();
        var canvas = Canvas;
        Canvas = null;
        if (canvas is null) return;
        canvas.Draw -= DrawCanvas;
        canvas.PointerMoved -= OnPointerMoved;
        canvas.PointerPressed -= OnPointerPressed;
        canvas.PointerReleased -= OnPointerReleased;
        canvas.PointerExited -= OnPointerExited;
        canvas.PointerWheelChanged -= OnPointerWheelChanged;
        canvas.DoubleTapped -= OnDoubleTapped;
        Children.Remove(canvas);
        canvas.RemoveFromVisualTree();
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ChartControlBase)d).Invalidate();
    protected void Invalidate()
    {
        if (!_active || Canvas is null) return;
        if (DispatcherQueue.HasThreadAccess) Canvas.Invalidate();
        else DispatcherQueue.TryEnqueue(() => Canvas?.Invalidate());
    }
    protected void ShowTooltip(string text, Point anchor)
    {
        _tooltipText.Text = text;
        _tooltipPresenter.Visibility = Visibility.Visible;
        _tooltipPresenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _tooltipPresenter.DesiredSize;
        var x = Math.Clamp(anchor.X - size.Width / 2, 4, Math.Max(4, ActualWidth - size.Width - 4));
        var y = anchor.Y - size.Height - 10;
        if (y < 4) y = Math.Min(ActualHeight - size.Height - 4, anchor.Y + 10);
        
        _tooltipPresenter.Margin = new Thickness(x, Math.Max(4, y), 0, 0);
    }
    protected void HideTooltip() => _tooltipPresenter.Visibility = Visibility.Collapsed;
    protected virtual void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => BeginAnimation();
    protected void BeginAnimation()
    {
        if (!_active) return;
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(BeginAnimation);
            return;
        }
        if (!AnimationsEnabled || !new UISettings().AnimationsEnabled)
        {
            AnimationProgress = 1;
            Invalidate();
            return;
        }
        _animationStarted = DateTime.UtcNow;
        AnimationProgress = 0;
        if (!_animationTimer.IsEnabled) _animationTimer.Start();
        Invalidate();
    }
    private void OnAnimationTick(object? sender, object e)
    {
        var linear = Math.Clamp((DateTime.UtcNow - _animationStarted).TotalMilliseconds / 320d, 0, 1);
        AnimationProgress = linear;
        Invalidate();
        if (linear >= 1) _animationTimer.Stop();
    }
    protected virtual void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not CanvasControl canvas) return;
        PointerPosition = e.GetCurrentPoint(canvas).Position;
        if (IsDragging)
        {
            var delta = new Point(PointerPosition.X - DragStart.X, PointerPosition.Y - DragStart.Y);
            if (PanMode is ChartPanMode.X or ChartPanMode.Both) PanX += delta.X;
            if (PanMode is ChartPanMode.Y or ChartPanMode.Both) PanY += delta.Y;
            DragStart = PointerPosition;
        }
        Invalidate();
    }
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not CanvasControl canvas) return;
        Focus(FocusState.Pointer); IsDragging = true; DragStart = e.GetCurrentPoint(canvas).Position; canvas.CapturePointer(e.Pointer); Invalidate();
    }
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not CanvasControl canvas) return;
        IsDragging = false; canvas.ReleasePointerCapture(e.Pointer); SelectAt(e.GetCurrentPoint(canvas).Position);
    }
    private void OnPointerExited(object sender, PointerRoutedEventArgs e) { IsDragging = false; HideTooltip(); }
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not CanvasControl canvas) return;
        ApplyKeyboardZoom(e.GetCurrentPoint(canvas).Properties.MouseWheelDelta > 0 ? 1.15 : 1 / 1.15); e.Handled = true;
    }
    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => ResetView();
    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateTooltipTheme();
        Invalidate();
    }

    private void UpdateTooltipTheme()
    {
        if (Parent is FrameworkElement ui)
        {
            _tooltipPresenter.RequestedTheme = ui.ActualTheme;
        }
    }
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape) { ResetView(); e.Handled = true; }
        else if (e.Key is Windows.System.VirtualKey.Add or Windows.System.VirtualKey.GamepadRightShoulder) { ApplyKeyboardZoom(1.15); e.Handled = true; }
        else if (e.Key is Windows.System.VirtualKey.Subtract or Windows.System.VirtualKey.GamepadLeftShoulder) { ApplyKeyboardZoom(1 / 1.15); e.Handled = true; }
        else if (e.Key == Windows.System.VirtualKey.Left) { PanX -= 12; Invalidate(); e.Handled = true; }
        else if (e.Key == Windows.System.VirtualKey.Right) { PanX += 12; Invalidate(); e.Handled = true; }
        else if (e.Key == Windows.System.VirtualKey.Up) { PanY -= 12; Invalidate(); e.Handled = true; }
        else if (e.Key == Windows.System.VirtualKey.Down) { PanY += 12; Invalidate(); e.Handled = true; }
    }
    private void ApplyKeyboardZoom(double factor)
    {
        if (ZoomMode is ChartZoomMode.X or ChartZoomMode.Both) ZoomX = Math.Clamp(ZoomX * factor, 1, 100);
        if (ZoomMode is ChartZoomMode.Y or ChartZoomMode.Both) ZoomY = Math.Clamp(ZoomY * factor, 1, 100);
        Invalidate();
    }
    private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (PanMode is ChartPanMode.X or ChartPanMode.Both) PanX += e.Delta.Translation.X;
        if (PanMode is ChartPanMode.Y or ChartPanMode.Both) PanY += e.Delta.Translation.Y;
        if (ZoomMode is ChartZoomMode.X or ChartZoomMode.Both) ZoomX = Math.Clamp(ZoomX * e.Delta.Scale, 1, 100);
        if (ZoomMode is ChartZoomMode.Y or ChartZoomMode.Both) ZoomY = Math.Clamp(ZoomY * e.Delta.Scale, 1, 100);
        Invalidate();
    }
    public void ResetView() { ZoomX = ZoomY = 1; PanX = PanY = 0; Invalidate(); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseRenderingResources();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        KeyDown -= OnKeyDown;
        ManipulationDelta -= OnManipulationDelta;
        ActualThemeChanged -= OnActualThemeChanged;
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer.Stop();
        GC.SuppressFinalize(this);
    }
    protected abstract void DrawCanvas(CanvasControl sender, CanvasDrawEventArgs args);
    protected abstract void SetDataSubscriptions(bool subscribe);
    protected abstract void ClearRenderState();
    protected abstract void SelectAt(Point point);
}
