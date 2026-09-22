using Haiyu.Controls.Models;
using Haiyu.Helpers;

namespace Haiyu.Services.Contracts;

public interface IWallpaperService
{
    public string NowHexValue { get; }
    public Task<bool> SetWallpaperAsync(string path);

    public void RegisterImageHost(Controls.ImageEx image);


    public void RegisterHostPath(string folder);

    public bool SetWallpaperForUrl(string uri);
    IAsyncEnumerable<WallpaperModel> GetFilesAsync(CancellationToken token = default);
    void RegisterMediaHost(ApplicationBackgroundControl media);
    void UnregisterMediaHost(ApplicationBackgroundControl media);
    void SetMediaForUrl(WallpaperShowType type, string backgroundFile, string? fallbackImage = null);

    void PauseVideo();

    void RestartVideo();
}
