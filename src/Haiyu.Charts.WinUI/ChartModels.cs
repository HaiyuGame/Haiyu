using System.Collections;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace Haiyu.Charts.WinUI;

public enum ChartZoomMode { None, X, Y, Both }
public enum ChartPanMode { None, X, Y, Both }
public enum ChartLegendPosition { Hidden, Top, Right, Bottom, Left }
public enum ChartTooltipMode { None, NearestPoint }

public readonly record struct ChartPoint(double X, double Y);
public readonly record struct DateTimeChartPoint(DateTime DateTime, double Value)
{
    public double X => DateTime.Ticks;
    public double Y => Value;
}

public abstract class ChartPaint;
public sealed class SolidChartPaint(Color color, float strokeWidth = 2) : ChartPaint
{
    public Color Color { get; set; } = color;
    public float StrokeWidth { get; set; } = strokeWidth;
}
public sealed class LinearGradientChartPaint(Color start, Color end) : ChartPaint
{
    public Color StartColor { get; set; } = start;
    public Color EndColor { get; set; } = end;
}

public abstract class ChartSeries
{
    public string Name { get; set; } = string.Empty;
    public int YAxisIndex { get; set; }
    public SolidChartPaint Stroke { get; set; } = new(Colors.DodgerBlue);
    public ChartPaint? Fill { get; set; }
    public bool ShowGeometry { get; set; }
    public Func<double, string>? TooltipFormatter { get; set; }
    internal abstract IEnumerable<ChartPoint> GetPoints();
    internal abstract INotifyCollectionChanged? ObservableValues { get; }
}

public sealed class LineSeries : ChartSeries
{
    public IReadOnlyList<DateTimeChartPoint> Values { get; set; } = Array.Empty<DateTimeChartPoint>();
    internal override IEnumerable<ChartPoint> GetPoints() => Values.Select(p => new ChartPoint(p.X, p.Y));
    internal override INotifyCollectionChanged? ObservableValues => Values as INotifyCollectionChanged;
}

public sealed class ColumnSeries : ChartSeries
{
    public IReadOnlyList<DateTimeChartPoint> Values { get; set; } = Array.Empty<DateTimeChartPoint>();
    internal override IEnumerable<ChartPoint> GetPoints() => Values.Select(p => new ChartPoint(p.X, p.Y));
    internal override INotifyCollectionChanged? ObservableValues => Values as INotifyCollectionChanged;
}

public class PieSeries : System.ComponentModel.INotifyPropertyChanged
{
    private double _value;
    public string Name { get; set; } = string.Empty;
    public double Value { get => _value; set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
    public double OuterRadiusOffset { get; set; }
    public double MaxRadialWidth { get; set; } = double.PositiveInfinity;
    public Color? Color { get; set; }
    public bool ShowDataLabel { get; set; }
    public Func<double, double, string>? LabelFormatter { get; set; }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public abstract class ChartAxis
{
    public bool ShowLabels { get; set; } = true;
    public bool ShowSeparatorLines { get; set; } = true;
    public float TextSize { get; set; } = 10;
    public IReadOnlyList<double>? CustomSeparators { get; set; }
    public Func<double, string>? LabelFormatter { get; set; }
}
public sealed class NumericAxis : ChartAxis;
public sealed class DateTimeAxis : ChartAxis
{
    public TimeSpan Interval { get; set; } = TimeSpan.Zero;
    public Func<DateTime, string>? DateFormatter { get; set; }
}

public sealed class ChartSelectionChangedEventArgs(string seriesName, int pointIndex, double value) : EventArgs
{
    public string SeriesName { get; } = seriesName;
    public int PointIndex { get; } = pointIndex;
    public double Value { get; } = value;
}

internal static class CollectionSubscriptions
{
    public static void Subscribe(IEnumerable? source, NotifyCollectionChangedEventHandler handler, bool subscribe)
    {
        if (source is INotifyCollectionChanged changed)
        {
            if (subscribe) changed.CollectionChanged += handler; else changed.CollectionChanged -= handler;
        }
    }
    public static void Subscribe(INotifyCollectionChanged? source, NotifyCollectionChangedEventHandler handler, bool subscribe)
    {
        if (source is null) return;
        if (subscribe) source.CollectionChanged += handler; else source.CollectionChanged -= handler;
    }
}
