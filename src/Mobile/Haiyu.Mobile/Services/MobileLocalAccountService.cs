using System.Buffers;
using System.Text.Json;
using Android.Telephony.Data;
using CommunityToolkit.Mvvm.Messaging;
using Haiyu.KuroClient;
using Haiyu.Mobile.Contracts;
using MemoryPack;
using Waves.Api.Models;
using Waves.Api.Models.Communitys;
using Waves.Api.Models.Messanger;

namespace Haiyu.Mobile.Services;


public sealed class MobileLocalAccountService:IMobileLocalAccountService
{
    internal string LocalUserFolder => Path.Combine(MauiProgram.MobileBaseFolder, "LocalUser");


    public MobileLocalAccountService()
    {
        Directory.CreateDirectory(LocalUserFolder);
    }

    const int BufferSize = 1024 * 1024;

    readonly Dictionary<string, Tuple<string, LocalAccount>> _cache = new();


    public async Task<LocalAccount?> GetUserAsync(string userId)
    {
        await GetUsersAsync();
        if (_cache.Count == 0)
        {
            //LoggerService.WriteError("未找到本地账号");
            return null;
        }
        else
        {
            if (_cache.TryGetValue(userId, out var value))
            {
                return value.Item2;
            }
            //LoggerService.WriteError("未找到本地账号");
            return null;
        }
    }

    public async Task<List<LocalAccount>?> GetUsersAsync()
    {
        List<LocalAccount> values = new();
        var shared = ArrayPool<byte>.Shared;
        _cache.Clear();
        foreach (var item in new DirectoryInfo(LocalUserFolder).GetFiles("*.dat"))
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
                    if (model != null)
                    {
                        values.Add(model);
                        _cache.Add(model.TokenId, Tuple.Create(item.FullName, model));
                    }
                }
            }
            catch (Exception)
            {
                //LoggerService.WriteError($"路径{item.FullName}访问失败");
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
                    Path.Combine(LocalUserFolder, $"{localAccount.TokenId}.dat"),
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
            return false;
        }
    }

}
