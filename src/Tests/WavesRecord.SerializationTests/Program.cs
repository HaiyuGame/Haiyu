using MemoryPack;
using Waves.Api.Models.Record;
using Waves.Api.Models.Wrappers;

var options = new MemoryPackSerializerOptions { StringEncoding = StringEncoding.Utf8 };
var time = new DateTime(2026, 10, 27, 12, 0, 0);
IList<WavesAnalysisPlayerCardItem> items = [new()
{
    PoolType = 1,
    Resource = [new RecordCardItemWrapper
    {
        ResourceId = 1503, QualityLevel = 5, Name = "测试角色", Count = 1,
        CardPoolType = "1", ResourceType = "角色", Time = "2026-10-27 12:00:00",
        RecordTime = time, Day = 27, Month = 10, Year = 2026,
    }],
}];

// Reproduce the exact field layouts from 917c7b38 and 97199ec6.
var v1 = MemoryPackSerializer.Serialize(new OriginalCard
{
    LastUpdater = time, SessionId = "old-account", Items = items,
}, options);
var v2 = MemoryPackSerializer.Serialize(new SourceCard
{
    LastUpdater = time, SessionId = "old-account", Items = items,
    UpdateSource = "source", UpdateType = "CloudGame",
}, options);
Verify(v1, 0, null, null);
Verify(v2, 0, null, "CloudGame");
var current = MemoryPackSerializer.Serialize(new WavesAnalysisPlayerCard
{
    LastUpdater = time, SessionId = "old-account", Items = items,
    UpdateSource = "source", UpdateType = "GameContext", Version = 2, RecordName = "游戏日志档案",
}, options);
Verify(current, 2, "游戏日志档案", "GameContext");
Console.WriteLine("PASS: original 3-member layout, 5-member layout, current round-trip");

void Verify(byte[] bytes, int version, string? name, string? type)
{
    var card = MemoryPackSerializer.Deserialize<WavesAnalysisPlayerCard>(bytes, options)
        ?? throw new Exception("Missing card");
    Check(card.LastUpdater == time && card.SessionId == "old-account", "account metadata");
    Check(card.Version == version && card.RecordName == name && card.UpdateType == type, "new metadata");
    Check(card.Items.Count == 1 && card.Items[0].PoolType == 1, "pool layout");
    var record = card.Items[0].Resource.Single();
    Check(record.ResourceId == 1503 && record.QualityLevel == 5 && record.Name == "测试角色", "pull data");
    Check(record.RecordTime == time, "pull time");
    var rewritten = MemoryPackSerializer.Deserialize<WavesAnalysisPlayerCard>(
        MemoryPackSerializer.Serialize(card, options), options)!;
    Check(rewritten.Items[0].Resource.Single().ResourceId == 1503, "rewrite preserves history");
}

static void Check(bool result, string field)
{
    if (!result) throw new Exception($"Compatibility failure: {field}");
}

[MemoryPackable]
public partial class OriginalCard
{
    public DateTime LastUpdater { get; set; }
    public string SessionId { get; set; } = "";
    public IList<WavesAnalysisPlayerCardItem> Items { get; set; } = [];
}

[MemoryPackable]
public partial class SourceCard
{
    public DateTime LastUpdater { get; set; }
    public string SessionId { get; set; } = "";
    public IList<WavesAnalysisPlayerCardItem> Items { get; set; } = [];
    public string UpdateSource { get; set; } = "";
    public string UpdateType { get; set; } = "";
}
