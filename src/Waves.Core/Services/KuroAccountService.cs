using CommunityToolkit.Mvvm.Messaging;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Buffers;
using Waves.Api.Models.Messanger;
using Waves.Core.Contracts;
using Waves.Core.Models;
using Waves.Core.Settings;

namespace Waves.Core.Services;

public class KuroAccountService : IKuroAccountService
{
    public KuroAccountService([FromKeyedServices("AppLog")] LoggerService loggerService,AppSettings appSettings)
    {
        LoggerService = loggerService;
        AppSettings = appSettings;
    }

    const int BufferSize = 1024 * 1024;

    readonly Dictionary<string, Tuple<string, LocalAccount>> _cache = new();

    public LoggerService LoggerService { get; }
    public AppSettings AppSettings { get; }
    public LocalAccount? Current { get; private set; }

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
                    var bytes = (await fs.ReadAsync(buffer));
                    var model = MemoryPackSerializer.Deserialize<LocalAccount>(
                        buffer.AsSpan(),
                        new MemoryPackSerializerOptions() { StringEncoding = StringEncoding.Utf8 }
                    );
                    if (model == null)
                    {
                        continue;
                    }
                    if (Current != null && model.TokenId == Current.TokenId)
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

    public void SetCurrentUser(string userId)
    {
        if (this._cache.TryGetValue(userId, out var value))
        {
            AppSettings.LastSelectUser = value.Item2.TokenId;
            this.Current = value.Item2;

            WeakReferenceMessenger.Default.Send(
                new SelectUserMessanger(true)
            );
        }
    }

    public void SetCurrentUser(LocalAccount localAccount)
    {
        this.Current = localAccount;
        AppSettings.LastSelectUser = localAccount.TokenId;
    }

    public async Task SetAutoUser()
    {
        await GetUsersAsync();
        if (AppSettings.LastSelectUser == null)
        {
            if (_cache == null)
            {
                // 无用户
                return;
            }
            return;
        }
        else
        {
            this.SetCurrentUser(
               _cache
                   .Where(x => x.Value.Item2.TokenId == AppSettings.LastSelectUser)
                   .FirstOrDefault()
                   .Value.Item2
           );
        }
    }
}
