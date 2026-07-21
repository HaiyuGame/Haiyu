using System.Diagnostics;
using Waves.Core.Models.Enums;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Haiyu.Controls;

[TemplateVisualState(GroupName = "CommonStates", Name = "ShowMedia")]
[TemplateVisualState(GroupName = "CommonStates", Name = "ShowImage")]
[TemplateVisualState(GroupName = "CommonStates", Name = "MediaLoading")]
[TemplateVisualState(GroupName = "CommonStates", Name = "ImageLoading")]
[TemplatePart(Name = "MediaControl", Type = typeof(MediaPlayerPresenter))]
[TemplatePart(Name = "ImageControl", Type = typeof(ImageEx))]
public partial class ApplicationBackgroundControl : Control
{
    private MediaPlayer? mediaPlayer;
    private MediaSource? activeMediaSource;
    private bool isLoaded;
    private bool isPaused;

    public ApplicationBackgroundControl()
    {
        DefaultStyleKey = typeof(ApplicationBackgroundControl);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnApplyTemplate()
    {
        if (MediaControl is not null)
        {
            MediaControl.MediaPlayer = null;
        }

        base.OnApplyTemplate();
        ImageControl = GetTemplateChild("ImageControl") as ImageEx;
        MediaControl = GetTemplateChild("MediaControl") as MediaPlayerPresenter;

        if (MediaControl is not null && mediaPlayer is not null)
        {
            MediaControl.MediaPlayer = mediaPlayer;
        }

        if (isLoaded)
        {
            UpdateMedia();
        }
    }

    public string? MediaSource
    {
        get => (string?)GetValue(MediaSourceProperty);
        set => SetValue(MediaSourceProperty, value);
    }

    public static readonly DependencyProperty MediaSourceProperty = DependencyProperty.Register(
        nameof(MediaSource),
        typeof(string),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(null, OnSourceChanged)
    );

    public string? ImageSource
    {
        get => (string?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource),
        typeof(string),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(null, OnSourceChanged)
    );

    public WallpaperShowType ShowType
    {
        get => (WallpaperShowType)GetValue(ShowTypeProperty);
        set => SetValue(ShowTypeProperty, value);
    }

    public static readonly DependencyProperty ShowTypeProperty = DependencyProperty.Register(
        nameof(ShowType),
        typeof(WallpaperShowType),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(WallpaperShowType.Image)
    );

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(ApplicationBackgroundControl),
        new PropertyMetadata(Stretch.Uniform)
    );

    public ImageEx? ImageControl { get; private set; }
    public MediaPlayerPresenter? MediaControl { get; private set; }
    public string? MediaBackground { get; private set; }
    public string? ImageBackground { get; private set; }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ApplicationBackgroundControl control && control.isLoaded)
        {
            control.UpdateMedia();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        isLoaded = true;
        UpdateMedia();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        isLoaded = false;
        ReleaseMediaPlayer();
    }

    public void UpdateMedia()
    {
        if (!isLoaded || MediaControl is null || ImageControl is null)
        {
            return;
        }

        try
        {
            if (ShowType == WallpaperShowType.Image)
            {
                ReleaseMediaPlayer();
                if (!string.IsNullOrWhiteSpace(ImageSource))
                {
                    ImageControl.Source = new BitmapImage(new Uri(ImageSource));
                }

                VisualStateManager.GoToState(this, "ShowImage", false);
                return;
            }

            if (string.IsNullOrWhiteSpace(MediaSource))
            {
                ReleaseMediaPlayer();
                return;
            }

            EnsureMediaPlayer();
            ReplaceMediaSource(MediaSource);
            VisualStateManager.GoToState(this, "MediaLoading", false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unable to update application background: {ex}");
            ReleaseMediaPlayer();
        }
    }

    private void EnsureMediaPlayer()
    {
        if (mediaPlayer is not null)
        {
            return;
        }

        mediaPlayer = new MediaPlayer
        {
            IsLoopingEnabled = true,
            AutoPlay = true,
        };
        mediaPlayer.CommandManager.IsEnabled = false;
        mediaPlayer.MediaOpened += Player_MediaOpened;
        mediaPlayer.MediaFailed += Player_MediaFailed;
        MediaControl!.MediaPlayer = mediaPlayer;
    }

    private void ReplaceMediaSource(string sourcePath)
    {
        if (activeMediaSource is not null && MediaBackground == sourcePath)
        {
            if (!isPaused && mediaPlayer!.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
            {
                mediaPlayer.Play();
            }
            return;
        }

        var newSource = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(sourcePath));
        var previousSource = activeMediaSource;
        activeMediaSource = newSource;
        MediaBackground = sourcePath;
        mediaPlayer!.Source = newSource;
        previousSource?.Dispose();

        if (isPaused)
        {
            mediaPlayer.Pause();
        }
    }

    private void Player_MediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!isLoaded || sender != mediaPlayer)
            {
                return;
            }

            VisualStateManager.GoToState(this, "ShowMedia", false);
            if (isPaused)
            {
                sender.Pause();
            }
        });
    }

    private void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"Application background playback failed: {args.Error} - {args.ErrorMessage}");
    }

    private void ReleaseMediaPlayer()
    {
        var player = mediaPlayer;
        mediaPlayer = null;

        if (MediaControl is not null)
        {
            MediaControl.MediaPlayer = null;
        }

        if (player is not null)
        {
            player.MediaOpened -= Player_MediaOpened;
            player.MediaFailed -= Player_MediaFailed;
            player.Pause();
            player.Source = null;
            player.Dispose();
        }

        activeMediaSource?.Dispose();
        activeMediaSource = null;
        MediaBackground = null;
    }

    public void Pause()
    {
        isPaused = true;
        // A paused MediaPlayer keeps its decoder and frame surfaces alive. Background
        // playback can remain paused for the entire lifetime of a game, so release it.
        ReleaseMediaPlayer();
    }

    public void Play()
    {
        isPaused = false;
        if (!isLoaded || ShowType != WallpaperShowType.Video)
        {
            return;
        }

        if (mediaPlayer is null || activeMediaSource is null)
        {
            UpdateMedia();
        }
        else
        {
            mediaPlayer.Play();
        }
    }

    public void SetMediaSource(string backgroundFile)
    {
        if (MediaSource == backgroundFile)
        {
            UpdateMedia();
            return;
        }

        MediaBackground = null;
        MediaSource = backgroundFile;
    }

    public void SetImageSource(string backgroundFile)
    {
        if (ImageSource == backgroundFile)
        {
            UpdateMedia();
            return;
        }

        ImageBackground = backgroundFile;
        ImageSource = backgroundFile;
    }
}
