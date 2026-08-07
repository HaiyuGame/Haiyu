namespace Waves.Core.Models;

public static class KuroAccountExtensions
{
    public static KuroAccount ToKuroAccount(this LocalAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return KuroAccount.Create(account.TokenId, account.Token, account.TokenDid);
    }
}
