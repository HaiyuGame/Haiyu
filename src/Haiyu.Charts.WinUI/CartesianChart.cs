using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.UI;

namespace Haiyu.Charts.WinUI;

public sealed partial class CartesianChart : ChartControlBase
{
    private readonly List<HitPoint> _hits = [];
    private readonly Dictionary<ChartSeries, Dictionary<double, double>> _pointSnapshots = [];
    private readonly Dictionary<(ChartSeries Series, double X), PointTransition> _pointTransitions = [];
    private Rect _plot;
    private bool _hasRenderedData;
    private bool _pendingIncrementalDiff;
    public CartesianChart() { }

    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(IEnumerable), typeof(CartesianChart), new PropertyMetadata(null, SeriesChanged));
    public static readonly DependencyProperty XAxesProperty = DependencyProperty.Register(nameof(XAxes), typeof(IEnumerable), typeof(CartesianChart), new PropertyMetadata(null, AxisChanged));
    public static readonly DependencyProperty YAxesProperty = DependencyProperty.Register(nameof(YAxes), typeof(IEnumerable), typeof(CartesianChart), new PropertyMetadata(null, AxisChanged));
    public IEnumerable? Series { get => (IEnumerable?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public IEnumerable? XAxes { get => (IEnumerable?)GetValue(XAxesProperty); set => SetValue(XAxesProperty, value); }
    public IEnumerable? YAxes { get => (IEnumerable?)GetValue(YAxesProperty); set => SetValue(YAxesProperty, value); }
    public event EventHandler<ChartSelectionChangedEventArgs>? SelectionChanged;

    private static void SeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (CartesianChart)d;
        if (c.IsLoaded) { c.Watch(e.OldValue as IEnumerable, false); c.Watch(e.NewValue as IEnumerable, true); }
        c.ResetAnimationState(); c.BeginAnimation();
    }
    private static void AxisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CartesianChart)d).Invalidate();
    private void Watch(IEnumerable? source, bool add)
    {
        CollectionSubscriptions.Subscribe(source, CollectionChanged, add);
        if (source is null) return;
        foreach (var s in source.OfType<ChartSeries>()) CollectionSubscriptions.Subscribe(s.ObservableValues, CollectionChanged, add);
    }

    protected override void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_hasRenderedData)
        {
            base.CollectionChanged(sender, e);
            return;
        }
        _pendingIncrementalDiff = true;
        BeginAnimation();
    }

    protected override void DrawCanvas(CanvasControl sender, CanvasDrawEventArgs e)
    {
        var ds = e.DrawingSession; _hits.Clear();
        var width = sender.ActualWidth; var height = sender.ActualHeight;
        if (width < 40 || height < 30) return;
        _plot = new Rect(42, 8, Math.Max(1, width - 54), Math.Max(1, height - 34));
        var series = Series?.OfType<ChartSeries>().ToArray() ?? [];
        var all = series.SelectMany(s => s.GetPoints()).ToArray();
        if (all.Length == 0) { DrawEmpty(ds); return; }
        PreparePointTransitions(series);
        var isInitialAnimation = !_hasRenderedData;
        var orderedX = all.Select(p => p.X).Distinct().Order().ToArray();
        var spacing = GetDomainSpacing(orderedX);
        var domainMinX = orderedX[0] - spacing / 2; var domainMaxX = orderedX[^1] + spacing / 2;
        if (domainMinX == domainMaxX) domainMaxX = domainMinX + 1;
        var (minX, maxX) = GetVisibleRange(domainMinX, domainMaxX, ZoomX, PanX, _plot.Width);
        var yAxes = YAxes?.OfType<ChartAxis>().ToArray() ?? [new NumericAxis()];
        var ranges = new (double min, double max)[Math.Max(1, yAxes.Length)];
        for (var i = 0; i < ranges.Length; i++)
        {
            var ys = series.Where(s => Math.Clamp(s.YAxisIndex, 0, ranges.Length - 1) == i).SelectMany(s => s.GetPoints()).Where(p => p.X >= minX && p.X <= maxX).Select(p => p.Y).ToArray();
            if (ys.Length == 0) ys = series.Where(s => Math.Clamp(s.YAxisIndex, 0, ranges.Length - 1) == i).SelectMany(s => s.GetPoints()).Select(p => p.Y).ToArray();
            var lo = ys.Length == 0 ? 0 : Math.Min(0, ys.Min()); var hi = ys.Length == 0 ? 1 : ys.Max();
            ranges[i] = AddYAxisPadding(lo, hi);
            ranges[i] = GetVisibleRange(ranges[i].min, ranges[i].max, ZoomY, PanY, _plot.Height, invertPan: true);
        }
        DrawAxes(ds, minX, maxX, ranges[0].min, ranges[0].max, yAxes.FirstOrDefault(), XAxes?.OfType<ChartAxis>().FirstOrDefault());
        using var layer = ds.CreateLayer(1, _plot);
        foreach (var item in series)
        {
            var animatedPoints = GetAnimatedPoints(item).OrderBy(p => p.X).ToArray();
            var visiblePoints = animatedPoints.Where(p => p.X >= minX && p.X <= maxX).ToArray();
            if (visiblePoints.Length == 0) continue;
            var range = ranges[Math.Clamp(item.YAxisIndex, 0, ranges.Length - 1)];
            ChartPoint[] renderPoints;
            if (item is ColumnSeries)
            {
                renderPoints = visiblePoints;
            }
            else
            {
                // Keep at most one point on either side to make a continuous clipped line.
                // Those points are render-only and must never participate in hit testing.
                var connected = new List<ChartPoint>(visiblePoints.Length + 2);
                var before = animatedPoints.Where(p => p.X < minX).ToArray();
                if (before.Length > 0) connected.Add(before[^1]);
                connected.AddRange(visiblePoints);
                var after = animatedPoints.FirstOrDefault(p => p.X > maxX);
                if (animatedPoints.Any(p => p.X > maxX)) connected.Add(after);
                renderPoints = connected.DistinctBy(p => p.X).OrderBy(p => p.X).ToArray();
            }
            var screen = Downsample(renderPoints.Select(p => Map(p, minX, maxX, range.min, range.max)).ToArray(), (int)_plot.Width * 2);
            if (item is ColumnSeries) DrawColumns(ds, item, screen, isInitialAnimation);
            else DrawLine(ds, item, screen, isInitialAnimation);

            var hitPoints = visiblePoints;
            if (isInitialAnimation)
            {
                var visibleCount = Math.Clamp((int)Math.Ceiling(hitPoints.Length * AnimationProgress), 1, hitPoints.Length);
                hitPoints = hitPoints[..visibleCount];
            }
            for (var i = 0; i < hitPoints.Length; i++)
            {
                var position = Map(hitPoints[i], minX, maxX, range.min, range.max);
                if (!_plot.Contains(new Point(position.X, position.Y))) continue;
                Rect? bounds = null;
                if (item is ColumnSeries)
                {
                    var barWidth = GetColumnWidth(screen);
                    var top = isInitialAnimation ? _plot.Bottom - (_plot.Bottom - position.Y) * AnimationProgress : position.Y;
                    bounds = new Rect(position.X - Math.Max(2, barWidth / 2), top, Math.Max(4, barWidth), Math.Max(1, _plot.Bottom - top));
                }
                _hits.Add(new(position, item, i, hitPoints[i].Y, bounds));
            }
        }
        _hasRenderedData = true;
        DrawHover(ds);
    }
    private Vector2 Map(ChartPoint p, double minX, double maxX, double minY, double maxY)
    {
        var nx = (p.X - minX) / (maxX - minX); var ny = (p.Y - minY) / (maxY - minY);
        return new((float)(_plot.X + nx * _plot.Width), (float)(_plot.Bottom - ny * _plot.Height));
    }
    private void DrawLine(CanvasDrawingSession ds, ChartSeries series, Vector2[] pts, bool isInitialAnimation)
    {
        if (pts.Length == 1) { ds.FillCircle(pts[0], 3, series.Stroke.Color); return; }
        if (isInitialAnimation)
        {
            var visibleCount = Math.Clamp((int)Math.Ceiling(pts.Length * AnimationProgress), 1, pts.Length);
            pts = pts[..visibleCount];
        }
        if (pts.Length == 1) { ds.FillCircle(pts[0], 3, series.Stroke.Color); return; }
        using var path = new CanvasPathBuilder(ds); path.BeginFigure(pts[0]); for (var i = 1; i < pts.Length; i++) path.AddLine(pts[i]); path.EndFigure(CanvasFigureLoop.Open);
        using var geometry = CanvasGeometry.CreatePath(path);
        if (series.Fill is LinearGradientChartPaint gradient)
        {
            var fillPts = pts.Concat([new Vector2(pts[^1].X, (float)_plot.Bottom), new Vector2(pts[0].X, (float)_plot.Bottom)]).ToArray();
            using var fillPath = new CanvasPathBuilder(ds); fillPath.BeginFigure(fillPts[0]); for (var i = 1; i < fillPts.Length; i++) fillPath.AddLine(fillPts[i]); fillPath.EndFigure(CanvasFigureLoop.Closed);
            using var fillGeometry = CanvasGeometry.CreatePath(fillPath); using var brush = new CanvasLinearGradientBrush(ds, gradient.StartColor, gradient.EndColor) { StartPoint = new(0, (float)_plot.Top), EndPoint = new(0, (float)_plot.Bottom) }; ds.FillGeometry(fillGeometry, brush);
        }
        ds.DrawGeometry(geometry, series.Stroke.Color, series.Stroke.StrokeWidth);
        if (series.ShowGeometry) foreach (var p in pts) ds.FillCircle(p, 3, series.Stroke.Color);
    }
    private void DrawColumns(CanvasDrawingSession ds, ChartSeries series, Vector2[] pts, bool isInitialAnimation)
    {
        var bar = GetColumnWidth(pts);
        foreach (var p in pts)
        {
            var animatedTop = isInitialAnimation ? _plot.Bottom - (_plot.Bottom - p.Y) * AnimationProgress : p.Y;
            ds.FillRectangle((float)(p.X - bar / 2), (float)animatedTop, (float)bar, (float)(_plot.Bottom - animatedTop), series.Stroke.Color);
        }
    }
    private double GetColumnWidth(Vector2[] pts)
    {
        var distances = pts.Zip(pts.Skip(1), (a, b) => Math.Abs(b.X - a.X)).Where(v => v > .5).Order().ToArray();
        var spacing = distances.Length == 0 ? _plot.Width * .5 : distances[distances.Length / 2];
        // Win2D accepts sub-pixel widths. Do not impose a multi-pixel minimum here:
        // at dense zoom levels it would make neighbouring columns touch or overlap.
        return Math.Min(96, Math.Max(.25, spacing * .64));
    }
    private void DrawAxes(CanvasDrawingSession ds, double minX, double maxX, double minY, double maxY, ChartAxis? y, ChartAxis? x)
    {
        var line = ActualTheme == ElementTheme.Dark ? Color.FromArgb(80, 255, 255, 255) : Color.FromArgb(70, 0, 0, 0);
        var text = ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black;
        using var format = new CanvasTextFormat { FontSize = 10 };
        for (var i = 0; i <= 4; i++) { var yy = _plot.Top + _plot.Height * i / 4; if (y?.ShowSeparatorLines != false) ds.DrawLine((float)_plot.Left, (float)yy, (float)_plot.Right, (float)yy, line); if (y?.ShowLabels != false) ds.DrawText((minY + (4 - i) / 4d * (maxY - minY)).ToString("N0"), 0, (float)yy - 7, text, format); }
        for (var i = 0; i <= 4; i++) { var value = minX + (maxX - minX) * i / 4; var xx = _plot.Left + _plot.Width * i / 4; var label = x is DateTimeAxis dt ? (dt.DateFormatter?.Invoke(new DateTime((long)value)) ?? new DateTime((long)value).ToString("MM/dd")) : (x?.LabelFormatter?.Invoke(value) ?? value.ToString("N0")); if (x?.ShowLabels != false) ds.DrawText(label, (float)xx - 20, (float)_plot.Bottom + 5, text, format); }
    }
    private void DrawEmpty(CanvasDrawingSession ds) { ds.DrawText("No data", new Vector2((float)Math.Max(4, _plot.X), (float)Math.Max(4, _plot.Y)), ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black); }
    private void DrawHover(CanvasDrawingSession ds)
    {
        if (TooltipMode == ChartTooltipMode.None || _hits.Count == 0) { HideTooltip(); return; }
        var candidate = FindHit(PointerPosition);
        if (candidate is null) { HideTooltip(); return; }
        var hit = candidate.Value;
        if (hit.Bounds is null) ds.FillCircle(hit.Position, 5, hit.Series.Stroke.Color);
        ShowTooltip($"{hit.Series.Name}: {hit.Series.TooltipFormatter?.Invoke(hit.Value) ?? hit.Value.ToString("N2")}", PointerPosition);
    }
    protected override void SelectAt(Point point)
    {
        var hit = FindHit(point);
        if (hit is not null) SelectionChanged?.Invoke(this, new(hit.Value.Series.Name, hit.Value.Index, hit.Value.Value));
    }
    private HitPoint? FindHit(Point point)
    {
        foreach (var hit in _hits)
        {
            if (hit.Bounds is Rect bounds && bounds.Contains(point)) return hit;
        }

        HitPoint? nearest = null;
        var nearestDistance = 18d;
        foreach (var hit in _hits)
        {
            if (hit.Bounds is not null) continue;
            var distance = Distance(hit.Position, point);
            if (distance > nearestDistance) continue;
            nearestDistance = distance;
            nearest = hit;
        }
        return nearest;
    }
    private static double Distance(Vector2 p, Point q) => Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Y - q.Y, 2));
    private static Vector2[] Downsample(Vector2[] points, int max) { if (points.Length <= max || max < 2) return points; var result = new Vector2[max]; for (var i = 0; i < max; i++) result[i] = points[(int)((long)i * (points.Length - 1) / (max - 1))]; return result; }
    private static double GetDomainSpacing(double[] values)
    {
        if (values.Length < 2) return 1;
        var gaps = values.Zip(values.Skip(1), (a, b) => b - a).Where(v => v > 0).Order().ToArray();
        return gaps.Length == 0 ? 1 : gaps[gaps.Length / 2];
    }
    private static (double min, double max) AddYAxisPadding(double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max)) return (0, 1);
        if (min == max)
        {
            if (min == 0) return (0, 1);
            var fallback = Math.Max(1, Math.Abs(min) * .1);
            return min > 0 ? (0, min + fallback) : (min - fallback, 0);
        }

        var span = max - min;
        var padding = Math.Max(span * .08, double.Epsilon);
        // Positive-only charts keep their meaningful zero baseline while gaining
        // enough room for the stroke, geometry and hover marker at the top.
        return min == 0 && max > 0 ? (0, max + padding) : (min - padding, max + padding);
    }
    private static (double min, double max) GetVisibleRange(double fullMin, double fullMax, double zoom, double panPixels, double pixelLength, bool invertPan = false)
    {
        var fullSpan = Math.Max(double.Epsilon, fullMax - fullMin);
        var span = fullSpan / Math.Max(1, zoom);
        var direction = invertPan ? 1 : -1;
        var center = (fullMin + fullMax) / 2 + direction * panPixels / Math.Max(1, pixelLength) * span;
        var half = span / 2;
        center = Math.Clamp(center, fullMin + half, fullMax - half);
        return (center - half, center + half);
    }
    private void PreparePointTransitions(ChartSeries[] series)
    {
        if (!_pendingIncrementalDiff && _pointSnapshots.Count > 0) return;
        var now = DateTime.UtcNow;
        foreach (var item in series)
        {
            var current = item.GetPoints().ToArray();
            _pointSnapshots.TryGetValue(item, out var previous);
            previous ??= [];
            foreach (var point in current)
            {
                if (previous.TryGetValue(point.X, out var oldY))
                {
                    if (!oldY.Equals(point.Y)) _pointTransitions[(item, point.X)] = new(oldY, point.Y, now);
                }
                else if (_hasRenderedData)
                {
                    var from = current.Where(p => p.X < point.X && previous.ContainsKey(p.X)).Select(p => previous[p.X]).LastOrDefault();
                    _pointTransitions[(item, point.X)] = new(from, point.Y, now);
                }
            }
            _pointSnapshots[item] = current.ToDictionary(p => p.X, p => p.Y);
        }
        var active = series.ToHashSet();
        foreach (var stale in _pointSnapshots.Keys.Where(k => !active.Contains(k)).ToArray()) _pointSnapshots.Remove(stale);
        foreach (var stale in _pointTransitions.Keys.Where(k => !active.Contains(k.Series) || !_pointSnapshots[k.Series].ContainsKey(k.X)).ToArray()) _pointTransitions.Remove(stale);
        _pendingIncrementalDiff = false;
    }
    private IEnumerable<ChartPoint> GetAnimatedPoints(ChartSeries series)
    {
        foreach (var point in series.GetPoints())
        {
            if (!_pointTransitions.TryGetValue((series, point.X), out var transition)) { yield return point; continue; }
            var linear = Math.Clamp((DateTime.UtcNow - transition.Started).TotalMilliseconds / 320d, 0, 1);
            yield return new ChartPoint(point.X, transition.FromY + (transition.ToY - transition.FromY) * linear);
            if (linear >= 1) _pointTransitions.Remove((series, point.X));
        }
    }
    private void ResetAnimationState()
    {
        _pointSnapshots.Clear(); _pointTransitions.Clear(); _hasRenderedData = false; _pendingIncrementalDiff = false;
    }
    private readonly record struct HitPoint(Vector2 Position, ChartSeries Series, int Index, double Value, Rect? Bounds);
    protected override void SetDataSubscriptions(bool subscribe) => Watch(Series, subscribe);
    protected override void ClearRenderState() { _hits.Clear(); _plot = Rect.Empty; ResetAnimationState(); }
    private readonly record struct PointTransition(double FromY, double ToY, DateTime Started);
}
