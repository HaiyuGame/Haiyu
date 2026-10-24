using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Haiyu.Charts.WinUI;

public sealed partial class ChartSurface : UserControl
{
    public ChartSurface() => InitializeComponent();

    internal CanvasControl CreateRenderingCanvas()
    {
        var canvas = (CanvasControl)((DataTemplate)Resources["RenderingCanvasTemplate"]).LoadContent();
        RenderingHost.Children.Add(canvas);
        return canvas;
    }

    internal void RemoveRenderingCanvas(CanvasControl canvas) => RenderingHost.Children.Remove(canvas);

    internal void ShowTooltip(string text, Point pointer)
    {
        const double edge = 4;
        const double gap = 12;
        TooltipText.Text = text;
        TooltipPresenter.MaxWidth = Math.Max(1, Math.Min(400, ActualWidth - edge * 2));
        TooltipPresenter.Visibility = Visibility.Visible;
        TooltipPresenter.Measure(new Size(TooltipPresenter.MaxWidth, double.PositiveInfinity));
        var size = TooltipPresenter.DesiredSize;

        // Prefer below/right of the pointer; flip at the edges without covering the cursor.
        var x = pointer.X + gap;
        var y = pointer.Y + gap;
        if (x + size.Width + edge > ActualWidth) x = pointer.X - size.Width - gap;
        if (y + size.Height + edge > ActualHeight) y = pointer.Y - size.Height - gap;
        Canvas.SetLeft(TooltipPresenter, Math.Clamp(x, edge, Math.Max(edge, ActualWidth - size.Width - edge)));
        Canvas.SetTop(TooltipPresenter, Math.Clamp(y, edge, Math.Max(edge, ActualHeight - size.Height - edge)));
    }

    internal void HideTooltip() => TooltipPresenter.Visibility = Visibility.Collapsed;
}
