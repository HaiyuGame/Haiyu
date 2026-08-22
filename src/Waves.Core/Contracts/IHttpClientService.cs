using Waves.Core.Models.Options;

namespace Waves.Core.Contracts;

public interface IHttpClientService
{
    public HttpClient HttpClient { get; }

    public HttpClient GameDownloadClient { get; }

    public void BuildClient(DownloadClientOption? option = null);
}
