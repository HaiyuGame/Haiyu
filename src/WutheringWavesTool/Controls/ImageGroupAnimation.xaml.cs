using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace Haiyu.Controls;

public sealed partial class ImageGroupAnimation : UserControl
{
    private List<string> imageBitmaps = new();
    private DispatcherTimer timer;
    private int currentIndex;

    public CanvasBitmap Lastbitmap { get; private set; }

    public bool IsDraw
    {
        get { return (bool)GetValue(IsDrawProperty); }
        set { SetValue(IsDrawProperty, value); }
    }

    public string GroupPath { get; private set; }
    public string LocalImage { get; private set; }

    public static readonly DependencyProperty IsDrawProperty = DependencyProperty.Register(
        "IsDraw",
        typeof(bool),
        typeof(ImageGroupAnimation),
        new Microsoft.UI.Xaml.PropertyMetadata(null)
    );

    public ImageGroupAnimation()
    {
        this.InitializeComponent();
        this.Loaded += ImageGroupAnimation_Loaded;
        this.Unloaded += ImageGroupAnimation_Unloaded;
    }

    private void ImageGroupAnimation_Loaded(object sender, RoutedEventArgs e) { }

    private void ImageGroupAnimation_Unloaded(object sender, RoutedEventArgs e)
    {
        StopTimer();
    }

    private void StartTimer()
    {
        StopTimer();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += Timer_Tick;
        timer.Start();
    }

    private void StopTimer()
    {
        if (timer != null)
        {
            timer.Stop();
            timer.Tick -= Timer_Tick;
            timer = null;
        }
    }

    private void Timer_Tick(object sender, object e)
    {
        if (imageBitmaps.Count == 0)
            return;
        currentIndex = (currentIndex + 1) % imageBitmaps.Count;
        if (!IsDraw)
            return;
        var fileName = imageBitmaps[currentIndex];
        if (Lastbitmap != null)
        {
            Lastbitmap.Dispose();
        }
        Lastbitmap = CanvasBitmap.LoadAsync(canvasControl, fileName).GetAwaiter().GetResult();
        canvasControl.Invalidate();
    }

    private void canvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        try
        {
            args.DrawingSession.DrawImage(
                Lastbitmap,
                new Rect(0, 0, sender.ActualWidth, sender.ActualHeight)
            );
        }
        catch (Exception) { }
        finally
        {
            GC.Collect();
        }
    }

    private void LoadGroupImages()
    {
        var folder = new DirectoryInfo(GroupPath);
        var files = folder.GetFiles("home_*.jpg");
        var sortedFiles = files
            .Select(file =>
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                var numberPart = fileNameWithoutExtension.Split('_')[1];
                if (int.TryParse(numberPart, out int number))
                {
                    return new { File = file, Number = number };
                }
                return null;
            })
            .Where(x => x != null)
            .OrderBy(x => x.Number)
            .Select(x => x.File)
            .ToList();
        foreach (var file in sortedFiles)
        {
            imageBitmaps.Add(file.FullName);
        }
        StartTimer();
    }

    public void SetImageGroupsPath(string path)
    {
        this.GroupPath = path;
    }

    public void SetWallpaperImage(string url)
    {
        //this.ImageUrl = url;
    }

    public void SetLocalWallpaperImage(string url)
    {
        this.LocalImage = url;
    }

    public void SetDisplayMode(WallpaperDisplayEnum type)
    {
        switch (type)
        {
            case WallpaperDisplayEnum.Animation:
                LoadGroupImages();
                VisualStateManager.GoToState(this, "Animation", true);
                break;
            case WallpaperDisplayEnum.Local:
                StopTimer();
                SetLocal();
                this.IsDraw = false;
                VisualStateManager.GoToState(this, "Image", true);
                break;
            case WallpaperDisplayEnum.Url:
                StopTimer();
                SetUrl();
                this.IsDraw = false;
                VisualStateManager.GoToState(this, "Image", true);
                break;
        }
    }

    private void SetLocal()
    {
        this.image.Source = new BitmapImage(new Uri(this.LocalImage));
    }

    private void SetUrl()
    {
        //this.image.Source = new BitmapImage(new Uri(this.ImageUrl));
    }
}

public enum WallpaperDisplayEnum
{
    Animation,
    Local,
    Url,
}
