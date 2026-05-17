using System.Collections.ObjectModel;
using System.Text.Json;
using Waves.Api.Models;
using Waves.Api.Models.CloudGame;

namespace Waves.Core.Contracts;

public sealed partial class CloudConfigManager
{
    public string Path { get; }

    public CloudConfigManager(string savePath)
    {
        this.Path = savePath;
    }

    public Dictionary<string, CloudGameLoginData> cacheData;



    public async Task<ObservableCollection<CloudGameLoginData>> GetUsersAsync(CancellationToken token = default)
    {
        ObservableCollection<CloudGameLoginData> logins = new ObservableCollection<CloudGameLoginData>();
        foreach (var item in Directory.GetFiles(this.Path, "*.json"))
        {
            try
            {
                var result = JsonSerializer.Deserialize(
                    await File.ReadAllTextAsync(item, token),
                    CloudGameContext.Default.CloudGameLoginData
                );
                logins.Add(result);
            }
            catch (Exception)
            {
                continue;
            }
        }
        foreach (var item in logins)
        {
            cacheData = logins.ToDictionary(x => x.Username, x => x);
        }
        return logins;
    }

    public async Task<CloudGameLoginData?> GetUserAsync(string userName,CancellationToken token = default)
    {
        List<CloudGameLoginData> items = null;
        if(cacheData == null || cacheData.Count == 0)
            items = (await GetUsersAsync()).ToList();
        else
            items = cacheData.Values.ToList();
        return items.Where(x => x.Username == userName).FirstOrDefault();
    }

    public async Task<bool> SaveUserAsync(CloudGameLoginData loginResult)
    {
        try
        {
            foreach (var item in Directory.GetFiles(this.Path, ".json"))
            {
                var result = JsonSerializer.Deserialize(
                    await File.ReadAllTextAsync(item),
                    CloudGameContext.Default.CloudGameLoginData
                );
                if (result.Username == loginResult.Username)
                {
                    File.Delete(item);
                }
            }
            await File.WriteAllTextAsync(
                Path + $"\\{loginResult.Username}.json",
                JsonSerializer.Serialize(loginResult, CloudGameContext.Default.CloudGameLoginData)
            );
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 删除本地账号
    /// </summary>
    /// <param name="id">账户ID</param>
    /// <returns></returns>
    public async Task DeleteUserAsync(string id)
    {
        await Task.Run(() =>
        {
            var path = System.IO.Path.Combine(this.Path, $"{id}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }).ConfigureAwait(false);
    }
}
