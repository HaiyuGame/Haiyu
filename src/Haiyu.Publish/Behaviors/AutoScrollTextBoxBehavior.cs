using Microsoft.Xaml.Behaviors;
using System.Windows.Controls;

namespace Haiyu.Publish.Behaviors;

public sealed class AutoScrollTextBoxBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.TextChanged += OnTextChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.TextChanged -= OnTextChanged;
        base.OnDetaching();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) => AssociatedObject.ScrollToEnd();
}
