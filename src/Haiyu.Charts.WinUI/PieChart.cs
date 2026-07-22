using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.UI;

namespace Haiyu.Charts.WinUI;

public sealed partial class PieChart : ChartControlBase
{
    private readonly List<Slice> _slices = [];
    private readonly Dictionary<PieSeries, double> _valueSnapshots = [];
    private readonly Dictionary<PieSeries, ValueTransition> _valueTransitions = [];
    private bool _hasRenderedData;
    private bool _pendingIncrementalDiff;
    public PieChart() { }
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series), typeof(IEnumerable), typeof(PieChart), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty InnerRadiusRatioProperty = DependencyProperty.Register(nameof(InnerRadiusRatio), typeof(double), typeof(PieChart), new PropertyMetadata(0d, Changed));
    public IEnumerable? Series { get => (IEnumerable?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public double InnerRadiusRatio { get => (double)GetValue(InnerRadiusRatioProperty); set => SetValue(InnerRadiusRatioProperty, value); }
    public event EventHandler<ChartSelectionChangedEventArgs>? SelectionChanged;
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (PieChart)d; if (c.IsLoaded) { c.Watch(e.OldValue as IEnumerable, false); c.Watch(e.NewValue as IEnumerable, true); } c.ResetAnimationState(); c.BeginAnimation();
    }
    private void Watch(IEnumerable? source, bool add)
    {
        CollectionSubscriptions.Subscribe(source, CollectionChanged, add);
        if (source is null) return;
        foreach (var series in source.OfType<PieSeries>())
        {
            if (add) series.PropertyChanged += SeriesPropertyChanged; else series.PropertyChanged -= SeriesPropertyChanged;
        }
    }
    private void SeriesPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { _pendingIncrementalDiff = true; BeginAnimation(); }
    protected override void CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (!_hasRenderedData) { base.CollectionChanged(sender, e); return; }
        _pendingIncrementalDiff = true; BeginAnimation();
    }
    protected override void DrawCanvas(CanvasControl sender, CanvasDrawEventArgs e)
    {
        _slices.Clear(); var series = Series?.OfType<PieSeries>().ToArray() ?? []; PrepareValueTransitions(series); var items = series.Select(s => (Series: s, Value: GetAnimatedValue(s))).Where(s => s.Value > 0).ToArray(); var total = items.Sum(s => s.Value); if (total <= 0) return;
        var initialAnimation = !_hasRenderedData;
        var center = new Vector2((float)sender.ActualWidth / 2, (float)sender.ActualHeight / 2); var finalRadius = (float)Math.Max(1, Math.Min(sender.ActualWidth, sender.ActualHeight) / 2 - 18); var radius = initialAnimation ? Math.Max(.01f, finalRadius * (float)AnimationProgress) : finalRadius; var angle = -Math.PI / 2;
        for (var i = 0; i < items.Length; i++)
        {
            var entry = items[i]; var item = entry.Series; var sweep = entry.Value / total * Math.PI * 2; var mid = angle + sweep / 2; var offset = new Vector2((float)(Math.Cos(mid) * item.OuterRadiusOffset), (float)(Math.Sin(mid) * item.OuterRadiusOffset)); var c = center + offset; var color = item.Color ?? Palette[i % Palette.Length];
            using var path = new CanvasPathBuilder(e.DrawingSession); path.BeginFigure(c); path.AddLine(c + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius)); path.AddArc(c, radius, radius, (float)angle, (float)sweep); path.EndFigure(CanvasFigureLoop.Closed); using var geometry = CanvasGeometry.CreatePath(path); e.DrawingSession.FillGeometry(geometry, color);
            if (InnerRadiusRatio > 0) e.DrawingSession.FillCircle(c, radius * (float)Math.Clamp(InnerRadiusRatio, 0, .95), ActualTheme == ElementTheme.Dark ? Colors.Black : Colors.White);
            if (item.ShowDataLabel) { var label = item.LabelFormatter?.Invoke(entry.Value, total) ?? $"{entry.Value / total:P1}"; e.DrawingSession.DrawText(label, c + new Vector2((float)Math.Cos(mid) * radius * .62f - 12, (float)Math.Sin(mid) * radius * .62f - 8), Colors.White); }
            if (AnimationProgress >= .999) _slices.Add(new(item, i, angle, angle + sweep, center, finalRadius)); angle += sweep;
        }
        _hasRenderedData = true;
        UpdateHover();
    }
    protected override void OnPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) { base.OnPointerMoved(sender, e); UpdateHover(); }
    private void UpdateHover()
    {
        var slice = Hit(PointerPosition);
        if (slice is null || TooltipMode == ChartTooltipMode.None) { HideTooltip(); return; }
        ShowTooltip($"{slice.Value.Series.Name}: {slice.Value.Series.Value:N2}", PointerPosition);
    }
    protected override void SelectAt(Point point) { var s = Hit(point); if (s is not null) SelectionChanged?.Invoke(this, new(s.Value.Series.Name, s.Value.Index, s.Value.Series.Value)); }
    private Slice? Hit(Point p) { foreach (var s in _slices) { var dx = p.X - s.Center.X; var dy = p.Y - s.Center.Y; var r = Math.Sqrt(dx * dx + dy * dy); if (r > s.Radius || r < s.Radius * InnerRadiusRatio) continue; var a = Math.Atan2(dy, dx); if (a < -Math.PI / 2) a += Math.PI * 2; if (a >= s.Start && a <= s.End) return s; } return null; }
    private static readonly Color[] Palette = [Colors.DodgerBlue, Colors.MediumPurple, Colors.MediumSeaGreen, Colors.Orange, Colors.DeepPink, Colors.Gold];
    private void PrepareValueTransitions(PieSeries[] series)
    {
        if (!_pendingIncrementalDiff && _valueSnapshots.Count > 0) return;
        var now = DateTime.UtcNow;
        foreach (var item in series)
        {
            if (_valueSnapshots.TryGetValue(item, out var previous))
            {
                if (!previous.Equals(item.Value)) _valueTransitions[item] = new(previous, item.Value, now);
            }
            else if (_hasRenderedData) _valueTransitions[item] = new(0, item.Value, now);
            _valueSnapshots[item] = item.Value;
        }
        var active = series.ToHashSet();
        foreach (var stale in _valueSnapshots.Keys.Where(k => !active.Contains(k)).ToArray()) { _valueSnapshots.Remove(stale); _valueTransitions.Remove(stale); }
        _pendingIncrementalDiff = false;
    }
    private double GetAnimatedValue(PieSeries series)
    {
        if (!_valueTransitions.TryGetValue(series, out var transition)) return series.Value;
        var linear = Math.Clamp((DateTime.UtcNow - transition.Started).TotalMilliseconds / 320d, 0, 1);
        if (linear >= 1) { _valueTransitions.Remove(series); return transition.To; }
        return transition.From + (transition.To - transition.From) * linear;
    }
    private void ResetAnimationState() { _valueSnapshots.Clear(); _valueTransitions.Clear(); _hasRenderedData = false; _pendingIncrementalDiff = false; }
    private readonly record struct Slice(PieSeries Series, int Index, double Start, double End, Vector2 Center, float Radius);
    protected override void SetDataSubscriptions(bool subscribe) => Watch(Series, subscribe);
    protected override void ClearRenderState() { _slices.Clear(); ResetAnimationState(); }
    private readonly record struct ValueTransition(double From, double To, DateTime Started);
}
