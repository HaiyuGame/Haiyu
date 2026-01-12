namespace Haiyu.Controls;

[TemplatePart(Name = nameof(LeftDropColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(IconTitleDropColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(HeaderColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(CenterContentColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(FooterColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(RightDropColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(RightPaddingColumn), Type = typeof(ColumnDefinition))]
[TemplatePart(Name = nameof(LeftPaddingColumn), Type = typeof(ColumnDefinition))]
[TemplateVisualState(GroupName = "DisplayStates", Name = "Tall")]
[TemplateVisualState(GroupName = "DisplayStates", Name = "Standard")]
public partial class TitleBar : ContentControl
{
    public TitleBar()
    {
        this.DefaultStyleKey = typeof(TitleBar);
    }

    private ColumnDefinition LeftDropColumn;
    private ColumnDefinition IconTitleDropColumn;
    private ColumnDefinition HeaderColumn;
    private ColumnDefinition CenterContentColumn;
    private ColumnDefinition FooterColumn;
    private ColumnDefinition RightDropColumn;
    private ColumnDefinition LeftPaddingColumn;
    private ColumnDefinition RightPaddingColumn;

    protected override void OnApplyTemplate()
    {
        this.LeftDropColumn = (ColumnDefinition)this.GetTemplateChild(nameof(LeftDropColumn));
        this.LeftPaddingColumn = (ColumnDefinition)this.GetTemplateChild(nameof(LeftPaddingColumn));
        this.IconTitleDropColumn = (ColumnDefinition)
            this.GetTemplateChild(nameof(IconTitleDropColumn));
        this.HeaderColumn = (ColumnDefinition)this.GetTemplateChild(nameof(HeaderColumn));
        this.CenterContentColumn = (ColumnDefinition)
            this.GetTemplateChild(nameof(CenterContentColumn));
        this.FooterColumn = (ColumnDefinition)this.GetTemplateChild(nameof(FooterColumn));
        this.RightDropColumn = (ColumnDefinition)this.GetTemplateChild(nameof(RightDropColumn));
        this.RightPaddingColumn = (ColumnDefinition)
            this.GetTemplateChild(nameof(RightPaddingColumn));
        this.Loaded += TitleBar_Loaded;
    }

    private void TitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        this.UpDate();
    }

    /// <summary>
    /// 设置标题栏
    /// </summary>
    internal void SetTitleBar()
    {
        //判断是否支持标题栏，或者当前的Window是否为空
        if (!this.IsExtendsContentIntoTitleBar || this.Window == null)
            return;
        //检查是否拓展
        if (!this.Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar)
        {
            this.Window.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        }
        UpDate();
        this.MakeDragLength();
        this.Window.AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
        this.SizeChanged -= TitleBar_SizeChanged;
        this.SizeChanged += TitleBar_SizeChanged;
    }

    private void TitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpDate();
        MakeDragLength();
    }

    public static double GetScaleAdjustment(Window window)
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(window);
        Microsoft.UI.WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
        IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        int result = WindowExtension.GetDpiForMonitor(
            hMonitor,
            WindowExtension.Monitor_DPI_Type.MDT_Default,
            out uint dpiX,
            out uint _
        );
        if (result != 0)
        {
            throw new Exception("Could not get DPI for monitor.");
        }

        uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }

    /// <summary>
    /// 制作拖动范围
    /// </summary>
    private void MakeDragLength()
    {
        var ScaleAdjustment = GetScaleAdjustment(Window);
        List<Windows.Graphics.RectInt32> dragRectsList = new();
        if (RightPaddingColumn == null || LeftPaddingColumn == null)
            return;
        if (Window.AppWindow.TitleBar.RightInset > 0)
        {
            RightPaddingColumn.Width = new GridLength(
                Window.AppWindow.TitleBar.RightInset / ScaleAdjustment
            );
        }
        else
        {
            RightPaddingColumn.Width = new GridLength(0);
        }
        if (Window.AppWindow.TitleBar.LeftInset > 0)
        {
            LeftPaddingColumn.Width = new GridLength(
                Window.AppWindow.TitleBar.RightInset / ScaleAdjustment
            );
        }
        else
        {
            LeftPaddingColumn.Width = new GridLength(0);
        }
        #region Header —— Title
        //左侧拖动
        //Windows.Graphics.RectInt32 dragRectL = new();
        //右侧拖动
        //Windows.Graphics.RectInt32 dragRectR = new();
        //dragRectL.X = (int)(
        //    (LeftPaddingColumn.ActualWidth + HeaderColumn.ActualWidth) * ScaleAdjustment
        //);
        //dragRectL.Y = 0;
        //dragRectL.Height = (int)(this.ActualHeight * ScaleAdjustment);
        //dragRectL.Width = (int)(
        //    (IconTitleDropColumn.ActualWidth + LeftDropColumn.ActualWidth) * ScaleAdjustment
        //);

        //dragRectR.X = (int)(
        //    (
        //        LeftPaddingColumn.ActualWidth
        //        + HeaderColumn.ActualWidth
        //        + IconTitleDropColumn.ActualWidth
        //        + LeftDropColumn.ActualWidth
        //        + CenterContentColumn.ActualWidth
        //    ) * ScaleAdjustment
        //);
        //dragRectR.Y = 0;
        //dragRectR.Height = (int)(this.ActualHeight * ScaleAdjustment);
        //dragRectR.Width = (int)((RightDropColumn.ActualWidth) * ScaleAdjustment);
        #endregion
        #region Title -- Header
        //左侧拖动
        Windows.Graphics.RectInt32 dragRectL = new();
        Windows.Graphics.RectInt32 dragRectL2 = new();
        //右侧拖动
        Windows.Graphics.RectInt32 dragRectR = new();
        dragRectL.X = 0;
        dragRectL.Y = 0;
        dragRectL.Height = (int)(this.ActualHeight * ScaleAdjustment);
        dragRectL.Width = (int)(
            (this.LeftPaddingColumn.ActualWidth + IconTitleDropColumn.ActualWidth) * ScaleAdjustment
        );
        dragRectL2.X = (int)(
            (
                this.LeftPaddingColumn.ActualWidth
                + IconTitleDropColumn.ActualWidth
                + HeaderColumn.ActualWidth
            ) * ScaleAdjustment
        );
        dragRectL2.Height = (int)(this.ActualHeight * ScaleAdjustment);
        dragRectL2.Y = 0;
        dragRectL2.Width = (int)(LeftDropColumn.ActualWidth * ScaleAdjustment);
        dragRectR.X = (int)(
            (
                LeftPaddingColumn.ActualWidth
                + HeaderColumn.ActualWidth
                + IconTitleDropColumn.ActualWidth
                + LeftDropColumn.ActualWidth
                + CenterContentColumn.ActualWidth
            ) * ScaleAdjustment
        );
        dragRectR.Y = 0;
        dragRectR.Height = (int)(this.ActualHeight * ScaleAdjustment);
        dragRectR.Width = (int)((RightDropColumn.ActualWidth) * ScaleAdjustment);
        #endregion
        dragRectsList.Add(dragRectL);
        dragRectsList.Add(dragRectL2);
        dragRectsList.Add(dragRectR);
        if (this.ContentRects != null)
            dragRectsList.AddRange(this.ContentRects);
        Window.AppWindow.TitleBar.SetDragRectangles(dragRectsList.ToArray());
    }
}
