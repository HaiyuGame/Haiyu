using Haiyu.Plugin.Contracts;
using MemoryPack;
using Waves.Api.Helper;
using Waves.Api.Models.Record;

namespace Haiyu.Plugin.Services;

public sealed class WavesPlayerCardCacheServices : IWavesPlayerCardCacheServices
{
    private readonly string _baseFolder;

    private static readonly MemoryPackSerializerOptions SerializerOptions = new()
    {
        StringEncoding = StringEncoding.Utf8,
    };

    public WavesPlayerCardCacheServices(string baseFolder)
    {
        _baseFolder = baseFolder;
        if (!Directory.Exists(_baseFolder))
        {
            Directory.CreateDirectory(_baseFolder);
        }
    }

    public async Task<Tuple<WavesAnalysisPlayerCard, string>> SaveAsync(
        WavesAnalysisPlayerCard card
    )
    {
        var oldFile = await FindFileBySessionIdAsync(card.SessionId);
        var newPath = Path.Combine(_baseFolder, $"{Guid.NewGuid():N}.record");
        if (oldFile != null)
        {
            await using var fs = new FileStream(
                oldFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true
            );
            var existing = await MemoryPackSerializer.DeserializeAsync<WavesAnalysisPlayerCard>(
                fs,
                SerializerOptions
            );
            if (existing != null)
            {
                var mergedItems = new List<WavesAnalysisPlayerCardItem>();
                foreach (var newItem in card.Items)
                {
                    var existItem = existing.Items.FirstOrDefault(x =>
                        x.PoolType == newItem.PoolType
                    );
                    mergedItems.Add(newItem.Merge(existItem));
                }
                foreach (var existItem in existing.Items)
                {
                    if (!mergedItems.Any(x => x.PoolType == existItem.PoolType))
                    {
                        mergedItems.Add(existItem);
                    }
                }
                card.Items = mergedItems;
            }
        }
        card.LastUpdater = DateTime.Now;
        var bytes = MemoryPackSerializer.Serialize(card, SerializerOptions);
        await File.WriteAllBytesAsync(newPath, bytes);
        if (oldFile != null && oldFile != newPath)
        {
            File.Delete(oldFile);
        }
        return Tuple.Create(card, newPath);
    }

    public async Task<WavesAnalysisPlayerCard?> LoadAsync(string sessionId)
    {
        var file = await FindFileBySessionIdAsync(sessionId);
        if (file == null)
            return null;

        await using var fs = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            true
        );
        return await MemoryPackSerializer.DeserializeAsync<WavesAnalysisPlayerCard>(
            fs,
            SerializerOptions
        );
    }

    public async Task<List<WavesAnalysisPlayerCard>> LoadAllAsync()
    {
        var results = new List<WavesAnalysisPlayerCard>();
        foreach (var file in Directory.EnumerateFiles(_baseFolder, "*.record"))
        {
            try
            {
                await using var fs = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    true
                );
                var card = await MemoryPackSerializer.DeserializeAsync<WavesAnalysisPlayerCard>(
                    fs,
                    SerializerOptions
                );
                if (card != null)
                {
                    results.Add(card);
                }
            }
            catch
            {
                continue;
            }
        }
        return results;
    }

    public async Task DeleteAsync(string sessionId)
    {
        var file = await FindFileBySessionIdAsync(sessionId);
        if (file != null)
        {
            File.Delete(file);
        }
    }

    public async Task<bool> ExistsAsync(string sessionId)
    {
        return await FindFileBySessionIdAsync(sessionId) != null;
    }

    private async Task<string?> FindFileBySessionIdAsync(string sessionId)
    {
        foreach (var file in Directory.EnumerateFiles(_baseFolder, "*.record"))
        {
            try
            {
                await using var fs = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    true
                );
                var card = await MemoryPackSerializer.DeserializeAsync<WavesAnalysisPlayerCard>(
                    fs,
                    SerializerOptions
                );
                if (card?.SessionId == sessionId)
                {
                    return file;
                }
            }
            catch
            {
                continue;
            }
        }
        return null;
    }
}
