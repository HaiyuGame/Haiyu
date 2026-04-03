using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.Xaml.Interactivity;

namespace Haiyu.Behaviors;

public sealed class HoverOpenCloseFlyoutBehavior : Behavior<FrameworkElement>
{
    public FlyoutBase Flyout
    {
        get => (FlyoutBase)GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.Register(
            nameof(Flyout),
            typeof(FlyoutBase),
            typeof(HoverOpenCloseFlyoutBehavior),
            new PropertyMetadata(null));

    public FlyoutPlacementMode Placement
    {
        get => (FlyoutPlacementMode)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.Register(
            nameof(Placement),
            typeof(FlyoutPlacementMode),
            typeof(HoverOpenCloseFlyoutBehavior),
            new PropertyMetadata(FlyoutPlacementMode.Bottom));

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PointerEntered += OnPointerEntered;
        AssociatedObject.PointerExited += OnPointerExited;
        AssociatedObject.Unloaded += OnUnloaded;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PointerEntered -= OnPointerEntered;
        AssociatedObject.PointerExited -= OnPointerExited;
        AssociatedObject.Unloaded -= OnUnloaded;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (Flyout is null) return;

        Flyout.ShowAt(AssociatedObject, new FlyoutShowOptions
        {
            Placement = Placement
        });
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        Flyout?.Hide();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Flyout?.Hide();
    }
}