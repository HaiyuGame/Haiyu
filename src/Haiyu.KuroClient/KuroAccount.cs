namespace Haiyu.KuroClient;

public sealed class KuroAccount
{
    public required string UserId { get; init; }

    public required string Token { get; init; }

    public required string DeviceId { get; init; }

    public static KuroAccount Create(string userId, string token, string deviceId)
    {
        return new KuroAccount
        {
            UserId = userId,
            Token = token,
            DeviceId = deviceId,
        };
    }

    public static KuroAccount Create(LocalAccount account)
    {
        return new KuroAccount
        {
            UserId = account.TokenId,
            Token = account.Token,
            DeviceId = account.TokenDid,
        };
    }
}
