using FluentWPF.Common;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace FluentWPF.Controls;

[TemplatePart(Name = "TitleColumn", Type = typeof(ColumnDefinition))]
[TemplatePart(Name = "CenterColumn", Type = typeof(ColumnDefinition))]
[TemplatePart(Name = "RightColumn", Type = typeof(ColumnDefinition))]
[TemplatePart(Name = "RightPaddingColumn", Type = typeof(ColumnDefinition))]
public partial class FluentWindow : Window
{
    static FluentWindow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentWindow),
            new FrameworkPropertyMetadata(typeof(FluentWindow))
        );
    }

    public FluentWindow()
    {
        this.Loaded += FluentWindow_Initialized;
        this.SizeChanged += FluentWindow_SizeChanged;
        this.Closed += FluentWindow_Closed;
    }

    private void FluentWindow_Closed(object? sender, EventArgs e)
    {
        Instance.RemoveWindow(this.Guid);
    }

    private void FluentWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTitlebar();
    }

    private void FluentWindow_Initialized(object? sender, EventArgs e)
    {
        Instance.AddWindow(this, (guid) =>
        {
            this.Guid = guid;
        });
        Instance.UpdateTheme();
        this.UpdateTitlebar();
    }

    /// <summary>
    /// 更新标题栏
    /// </summary>
    private void UpdateTitlebar()
    {
        IntPtr hMenu = Methods.GetSystemMenu(this.WinHander, false);
        if (hMenu != IntPtr.Zero)
        {
            int menuItemCount = Methods.GetMenuItemCount(hMenu);
            IntPtr hdc = Methods.GetDC(WinHander);
            int dpi = Methods.GetDeviceCaps(hdc, Methods.LOGPIXELSX);
            Methods.ReleaseDC(WinHander, hdc);
            double dpiFactor = dpi / 96.0;
            int buttonWidth = (int)(
                (
                    Methods.GetSystemMetrics(Methods.SM_CXSIZE)
                    - Methods.GetSystemMetrics(Methods.SM_CXSIZEFRAME) * 2
                ) * dpiFactor
            );
            if (_rightpaddingPadding == null)
                return;
            int resval = 0;
            if (dpiFactor == 1)
            {
                resval = 5;
            }
            else if (dpiFactor == 1.25)
            {
                resval = 3;
            }
            var width = buttonWidth * resval;
            this._rightpaddingPadding.Width = new GridLength(width);
        }
    }

    public IntPtr WinHander => new WindowInteropHelper(this).Handle;

    public SystemBackdropBase SystemBackdrop
    {
        get { return (SystemBackdropBase)GetValue(SystemBackdropProperty); }
        set { SetValue(SystemBackdropProperty, value); }
    }
    public static readonly DependencyProperty SystemBackdropProperty = DependencyProperty.Register(
        "SystemBackdrop",
        typeof(SystemBackdropBase),
        typeof(FluentWindow),
        new PropertyMetadata(null, OnBackdropChanged)
    );

    private static void OnBackdropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SystemBackdropBase sys && d is FluentWindow win)
        {
            sys.SetBackdrop(win);
        }
    }

    /// <summary>
    /// 标题栏左侧内容
    /// </summary>
    public object TitleBarLeftContent
    {
        get { return (object)GetValue(TitleBarLeftContentProperty); }
        set { SetValue(TitleBarLeftContentProperty, value); }
    }

    public static readonly DependencyProperty TitleBarLeftContentProperty =
        DependencyProperty.Register(
            "TitleBarLeftContent",
            typeof(object),
            typeof(FluentWindow),
            new PropertyMetadata(null)
        );

    public GridLength TitleBarHeight
    {
        get { return (GridLength)GetValue(TitleBarHeightProperty); }
        set { SetValue(TitleBarHeightProperty, value); }
    }

    // Using a DependencyProperty as the backing store for TitleBarHeight.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TitleBarHeightProperty = DependencyProperty.Register(
        "TitleBarHeight",
        typeof(GridLength),
        typeof(FluentWindow),
        new PropertyMetadata(new GridLength(36))
    );

    /// <summary>
    /// 标题栏居中内容
    /// </summary>
    public object TitlebarContent
    {
        get { return (object)GetValue(TitlebarContentProperty); }
        set { SetValue(TitlebarContentProperty, value); }
    }

    public static readonly DependencyProperty TitlebarContentProperty = DependencyProperty.Register(
        "TitlebarContent",
        typeof(object),
        typeof(FluentWindow),
        new PropertyMetadata(null)
    );

    #region 获得模板控件
    private ColumnDefinition _leftPadding;
    private ColumnDefinition _titlePadding;
    private ColumnDefinition _centerPadding;
    private ColumnDefinition _rightPadding;
    private ColumnDefinition _rightpaddingPadding;

    public Visibility TitlebarVisibility
    {
        get { return (Visibility)GetValue(TitlebarVisibilityProperty); }
        set { SetValue(TitlebarVisibilityProperty, value); }
    }

    public Guid Guid { get; private set; }

    public static readonly DependencyProperty TitlebarVisibilityProperty =
        DependencyProperty.Register(
            "TitlebarVisibility",
            typeof(Visibility),
            typeof(FluentWindow),
            new PropertyMetadata(Visibility.Visible)
        );




    public object AppImageBackground
    {
        get { return (object)GetValue(AppImageBackgroundProperty); }
        set { SetValue(AppImageBackgroundProperty, value); }
    }

    // Using a DependencyProperty as the backing store for AppImageBackground.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty AppImageBackgroundProperty =
        DependencyProperty.Register(nameof(AppImageBackground), typeof(object), typeof(FluentWindow), new PropertyMetadata(null));



    public override void OnApplyTemplate()
    {
        this._titlePadding = (ColumnDefinition)GetTemplateChild("TitleColumn");
        this._centerPadding = (ColumnDefinition)GetTemplateChild("CenterColumn");
        this._rightPadding = (ColumnDefinition)GetTemplateChild("RightColumn");
        this._rightpaddingPadding = (ColumnDefinition)GetTemplateChild("RightPaddingColumn");
        UpdateTitlebar();
        base.OnApplyTemplate();
    }
    #endregion
}
