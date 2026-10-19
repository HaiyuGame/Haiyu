using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Security.Cryptography;
using System.Text;
using ScoringService.Models;

namespace ScoringService;

/// <summary>角色模板与可复用词条的 JSON 存储服务。内部文件名由标识散列生成，更新兼容已有文件名。</summary>
public sealed class ScoringEntryService
{
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>本地模板和词条存储目录。</summary>
    public SortingOption Option { get; }

    /// <summary>成功保存或删除后发出变更通知；订阅方按自身线程模型调度刷新。</summary>
    public event EventHandler? Changed;

    public ScoringEntryService(SortingOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        Option = option;
    }

    public async Task<IEnumerable<EntryGroup>> GetGroupTemplateAsync()
    {
        return await ReadAllAsync(Option.GroupFolder, ScoringJsonContext.Default.EntryGroup);
    }

    /// <summary>读取词条库，缺失目录视为空库。</summary>
    public Task<List<EntryItem>> GetItemsAsync()
        => ReadAllAsync(Option.ItemFolder, ScoringJsonContext.Default.EntryItem);

    /// <summary>保存角色模板。默认拒绝覆盖，编辑时显式传入 overwrite。</summary>
    public async Task SaveGroupAsync(EntryGroup group, bool overwrite = false)
    {
        ValidateGroup(group);
        await SaveAsync(Option.GroupFolder, group, x => x.Id, ScoringJsonContext.Default.EntryGroup, overwrite);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>保存可复用词条。编号、类型、用途共同标识一个词条。</summary>
    public async Task SaveItemAsync(EntryItem item, bool overwrite = false)
    {
        ValidateItem(item);
        await SaveAsync(Option.ItemFolder, item, ItemKey, ScoringJsonContext.Default.EntryItem, overwrite);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按模板编号删除已保存文件，不将编号直接作为路径使用。</summary>
    public async Task DeleteGroupAsync(string id)
    {
        await DeleteAsync(Option.GroupFolder, id, x => x.Id, ScoringJsonContext.Default.EntryGroup);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>删除库词条；已保存模板持有独立副本，不受删除影响。</summary>
    public async Task DeleteItemAsync(EntryItem item)
    {
        await DeleteAsync(Option.ItemFolder, ItemKey(item), ItemKey, ScoringJsonContext.Default.EntryItem);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>校验模板名称、标识及全部计算参数。</summary>
    public static void ValidateGroup(EntryGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(group.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(group.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(group.RoleId);
        ScoringService.ValidateTemplate(group);
        foreach (var item in group.Items)
            ValidateItem(item);
    }

    /// <summary>校验库词条的名称、属性标识以及用途对应的参数。</summary>
    public static void ValidateItem(EntryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.EntryName);
        ScoringService.ValidateTemplate(new()
        {
            ScoringVersion = 1, MainStatMaximumBenefit = 1, SubStatMaximumBenefit = 1, Items = [item]
        });
    }

    private static string ItemKey(EntryItem item) => $"{item.EntryNo}\0{(uint)item.Type}\0{(int)item.Usage}";

    private static async Task<List<T>> ReadAllAsync<T>(string folder, JsonTypeInfo<T> info)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var result = new List<T>();
        if (!Directory.Exists(folder))
            return result;
        foreach (var file in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            await using var stream = File.OpenRead(file);
            var model = await JsonSerializer.DeserializeAsync(stream, info);
            if (model is null)
                throw new JsonException($"JSON 文件内容为空：{Path.GetFileName(file)}");
            result.Add(model);
        }
        return result;
    }

    private static async Task<string?> FindPathAsync<T>(string folder, string key,
        Func<T, string> keySelector, JsonTypeInfo<T> info)
    {
        if (!Directory.Exists(folder))
            return null;
        string? found = null;
        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var item = await JsonSerializer.DeserializeAsync(stream, info);
            if (item is null || keySelector(item) != key)
                continue;
            if (found is not null)
                throw new InvalidOperationException("多个存储文件使用相同标识，请先整理重复文件。");
            found = file;
        }
        return found;
    }

    private async Task SaveAsync<T>(string folder, T value, Func<T, string> keySelector,
        JsonTypeInfo<T> info, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        await gate.WaitAsync();
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(folder);
            var key = keySelector(value);
            var existing = await FindPathAsync(folder, key, keySelector, info);
            if (existing is not null && !overwrite)
                throw new InvalidOperationException("相同标识已存在，请使用编辑功能，或修改导入文件的标识。");
            var path = existing ?? Path.Combine(folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json");
            temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(stream, value, info);
            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            try
            {
                if (temporaryPath is not null && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            finally { gate.Release(); }
        }
    }

    private async Task DeleteAsync<T>(string folder, string key, Func<T, string> keySelector, JsonTypeInfo<T> info)
    {
        await gate.WaitAsync();
        try
        {
            var path = await FindPathAsync(folder, key, keySelector, info);
            if (path is not null)
                File.Delete(path);
        }
        finally { gate.Release(); }
    }
}
