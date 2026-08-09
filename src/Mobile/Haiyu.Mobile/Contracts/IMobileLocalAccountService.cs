using Haiyu.KuroClient;

namespace Haiyu.Mobile.Contracts;

public interface IMobileLocalAccountService
{
    public Task<LocalAccount?> GetUserAsync(string userId);

    public Task<List<LocalAccount>?> GetUsersAsync();

    public Task<bool> SaveUserAsync(LocalAccount localAccount);

    public Task<bool> DeleteUserAsync(string userId);
} 
