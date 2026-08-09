namespace Haiyu.KuroClient;

public sealed class KuroHttpHandler : HttpClientHandler
{
    public KuroHttpHandler()
    {
        AutomaticDecompression = DecompressionMethods.All;
        ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
    }
}
