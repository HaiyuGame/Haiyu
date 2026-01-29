using Waves.Core.Models.Enums;
using Windows.Media;
using Windows.Media.Playback;

namespace Haiyu.Controls;

[TemplateVisualState(GroupName = "CommonStates", Name = "ShowMedia")]
[TemplateVisualState(GroupName = "CommonStates", Name = "ShowImage")]
[TemplateVisualState(GroupName = "CommonStates", Name = "MediaLoading")]
[TemplateVisualState(GroupName = "CommonStates", Name = "ImageLoading")]
[TemplatePart(Name = "MediaControl", Type = typeof(MediaPlayerPresenter))]
[TemplatePart(Name = "MediaBorder", Type = typeof(Border))]
[TemplatePart(Name = "ImageControl", Type = typeof(ImageEx))]
[TemplatePart(Name = "LoadingControl", Type = typeof(ProgressBar))]
public partial class ApplicationBackgroundControl : Control
{
    protected override void OnApplyTemplate()
    {
        this.ImageControl = (ImageEx)GetTemplateChild("ImageControl");
        this.MediaControl = (MediaPlayerPresenter)GetTemplateChild("MediaControl");
    }

    public ApplicationBackgroundControl()
    {
        this.DefaultStyleKey = typeof(ApplicationBackgroundControl);
    }

    public string MediaSource
    {
        get { return (string)GetValue(MediaSourceProperty); }
        set { SetValue(MediaSourceProperty, value); }
    }

    public static readonly DependencyProperty MediaSourceProperty = DependencyProperty.Register(
        "MediaSource",
        typeof(string),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(null, OnMediaPlayerChanged)
    );

    private static void OnMediaPlayerChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        (d as ApplicationBackgroundControl).UpdateMedia();
    }

    public void UpdateMedia()
    {
        if (this.ShowType == WallpaperShowType.Video && string.IsNullOrWhiteSpace(this.MediaSource))
            return;
        if (this.ShowType == WallpaperShowType.Image && string.IsNullOrWhiteSpace(this.ImageSource))
            return;
        try
        {
            
            if (this.ShowType == WallpaperShowType.Image)
            {
                if (MediaControl.MediaPlayer != null)
                {
                    MediaControl.MediaPlayer.MediaOpened -= Player_MediaOpened;
                    MediaControl.MediaPlayer.Dispose();
                    MediaControl.MediaPlayer = null;
                }
                this.ImageControl.Source = new BitmapImage(new(this.ImageSource));
                MediaControl.MediaPlayer = null;
                VisualStateManager.GoToState(this, "ShowImage", false);
            }
            else
            {
                if(this.MediaControl.MediaPlayer == null)
                {
                    var MediaPlayer = new MediaPlayer() { IsLoopingEnabled = true, AutoPlay = true };
                    MediaPlayer.CommandManager.IsEnabled = false;
                    MediaPlayer.MediaOpened += Player_MediaOpened;
                    this.MediaControl.MediaPlayer = MediaPlayer;
                }
                var source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(MediaSource));
                this.MediaControl.MediaPlayer?.Source = source;
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    VisualStateManager.GoToState(this, "MediaLoading", false);
                });
            }
        }
        catch (Exception) 
        {

        }
        finally
        {
            GC.Collect();
        }
    }

    private void Player_MediaOpened(MediaPlayer sender, object args)
    {
        this.DispatcherQueue.TryEnqueue(async () =>
        {
            VisualStateManager.GoToState(this, "ShowMedia", false);
            await Task.Delay(500);
        });
    }

    public string ImageSource
    {
        get { return (string)GetValue(ImageSourceProperty); }
        set { SetValue(ImageSourceProperty, value); }
    }

    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        "ImageSource",
        typeof(string),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(null, OnImageSourceChanged)
    );

    private static void OnImageSourceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        //(d as ApplicationBackgroundControl).UpdateMedia();
    }

    public WallpaperShowType ShowType
    {
        get { return (WallpaperShowType)GetValue(ShowTypeProperty); }
        set { SetValue(ShowTypeProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ShowType.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ShowTypeProperty = DependencyProperty.Register(
        "ShowType",
        typeof(WallpaperShowType),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(WallpaperShowType.Image, OnWallpaperTypeChanged)
    );

    private static void OnWallpaperTypeChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        //(d as ApplicationBackgroundControl).UpdateMedia();
    }

    public Stretch Stretch
    {
        get { return (Stretch)GetValue(StretchProperty); }
        set { SetValue(StretchProperty, value); }
    }

    public ImageEx ImageControl { get; private set; }
    public MediaPlayerPresenter MediaControl { get; private set; }
    public string MediaBackground { get; private set; }
    public string ImageBackground { get; private set; }

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        "Stretch",
        typeof(Stretch),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(Stretch.Uniform)
    );

    public void Pause()
    {
        if (MediaControl.MediaPlayer == null)
            return;
        MediaControl.MediaPlayer.Pause();
    }

    public void Play()
    {
        if (MediaControl.MediaPlayer == null)
            return;
        MediaControl.MediaPlayer.Play();
    }

    public void SetMediaSource(string backgroundFile)
    {
        if (this.MediaBackground != backgroundFile)
            this.MediaSource = backgroundFile;
    }

    internal void SetImageSource(string backgroundFile)
    {
        if (this.ImageBackground != backgroundFile)
            this.ImageSource = backgroundFile;
    }
}
