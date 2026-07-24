namespace Waves.Core.Models;

public sealed partial class KuroAccount
{
    public required string UserId { get; init; }

    public required string Token { get; init; }

    public required string DeviceId { get; init; }

    public static KuroAccount From(LocalAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new KuroAccount
        {
            UserId = account.TokenId,
            Token = account.Token,
            DeviceId = account.TokenDid,
        };
    }
}
