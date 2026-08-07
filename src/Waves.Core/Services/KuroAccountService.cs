namespace Waves.Core.Services;

public class KuroAccountService : IKuroAccountService
{
    public KuroAccountService(
        [FromKeyedServices("AppLog")] LoggerService loggerService,
        AppSettings appSettings
    )
    {
        LoggerService = loggerService;
        AppSettings = appSettings;
    }

    const int BufferSize = 1024 * 1024;

    readonly Dictionary<string, Tuple<string, LocalAccount>> _cache = new();

    public LoggerService LoggerService { get; }
    public AppSettings AppSettings { get; }
    public KuroAccount? CurrentAccount { get; private set; }

    public async Task<LocalAccount?> GetUserAsync(string userId)
    {
        await GetUsersAsync();
        if (_cache.Count == 0)
        {
            LoggerService.WriteError("未找到本地账号");
            return null;
        }
        else
        {
            if (_cache.TryGetValue(userId, out var value))
            {
                return value.Item2;
            }
            LoggerService.WriteError("未找到本地账号");
            return null;
        }
    }

    public async Task<List<LocalAccount>?> GetUsersAsync()
    {
        List<LocalAccount> values = new();
        var shared = ArrayPool<byte>.Shared;
        _cache.Clear();
        foreach (var item in new DirectoryInfo(AppSettings.LocalUserFolder).GetFiles("*.dat"))
        {
            var buffer = shared.Rent(BufferSize);
            try
            {
                using (
                    var fs = new FileStream(
                        item.FullName,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        4096,
                        true
                    )
                )
                {
                    var bytes = await fs.ReadAsync(buffer);
                    var model = MemoryPackSerializer.Deserialize<LocalAccount>(
                        buffer.AsSpan(),
                        new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                    );
                    if (model == null)
                    {
                        continue;
                    }
                    if (CurrentAccount != null && model.TokenId == CurrentAccount.UserId)
                    {
                        model.IsSelect = true;
                    }
                    else
                    {
                        model.IsSelect = false;
                    }
                    if (model != null)
                    {
                        values.Add(model);
                        _cache.Add(model.TokenId, Tuple.Create(item.FullName, model));
                    }
                }
            }
            catch (Exception)
            {
                LoggerService.WriteError($"路径{item.FullName}访问失败");
            }
            finally
            {
                shared.Return(buffer);
            }
        }
        return values;
    }

    public async Task<bool> SaveUserAsync(LocalAccount localAccount)
    {
        try
        {
            await GetUsersAsync();
            if (_cache.TryGetValue(localAccount.TokenId, out var tuple))
            {
                File.Delete(tuple.Item1);
            }
            using (
                var fs = new FileStream(
                    Path.Combine(AppSettings.LocalUserFolder, $"{localAccount.TokenId}.dat"),
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    true
                )
            )
            {
                await MemoryPackSerializer.SerializeAsync(
                    fs,
                    localAccount,
                    new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                );
            }
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.WriteError($"保存本地账号失败：{ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        try
        {
            await GetUsersAsync();
            if (_cache.TryGetValue(userId, out var tuple))
            {
                File.Delete(tuple.Item1);
            }
            await GetUsersAsync();
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.WriteError($"删除本地账号失败：{ex.Message}");
            return false;
        }
    }

    public void SetCurrentUser(string userId, bool isWrite = true)
    {
        if (this._cache.TryGetValue(userId, out var value))
        {
            _ = AppSettings.SetLastSelectUserAsync(value.Item2.TokenId).ConfigureAwait(false);
            this.CurrentAccount = value.Item2.ToKuroAccount();

            WeakReferenceMessenger.Default.Send(new SelectUserMessanger(true));
        }
    }

    public void SetCurrentUser(LocalAccount localAccount, bool isWrite = true)
    {
        this.CurrentAccount = localAccount.ToKuroAccount();
        if (isWrite)
            _ = AppSettings.SetLastSelectUserAsync(localAccount.TokenId).ConfigureAwait(false);
    }

    public async Task SetAutoUser()
    {
        await GetUsersAsync();
        var lastSelectUser = await AppSettings.GetLastSelectUserAsync().ConfigureAwait(false);
        if (lastSelectUser == null)
        {
            if (_cache == null)
            {
                return;
            }
            return;
        }
        else
        {
            var result = _cache
                .Where(x => x.Value.Item2.TokenId == lastSelectUser)
                .FirstOrDefault();
            if (result.Key == null || result.Value == null)
            {
                return;
            }
            this.SetCurrentUser(result.Value.Item2);
        }
    }
}
