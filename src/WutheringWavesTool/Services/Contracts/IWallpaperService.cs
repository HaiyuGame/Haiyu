using Haiyu.Helpers;
using Waves.Core.Models.Enums;

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
    void SetMediaForUrl(WallpaperShowType type, string backgroundFile);
}
