using System.Text.Json.Serialization;
using Waves.Core.Contracts;
using Waves.Core.Models.Enums;

namespace Waves.Core.Models;

public class WavesQueryLocalPlayerInfoRequest
{
    [JsonPropertyName("oauthCode")]
    public string OAutoCode { get; set; }
}

public class QueryLocalRoleInfoRequest
{
    [JsonPropertyName("oauthCode")]
    public string OAutoCode { get; set; }

    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; }

    [JsonPropertyName("region")]
    public string Region { get; set; }
}

public class QueryPlayerInfo
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, string> Data { get; set; }

    [JsonIgnore]
    public List<ILocalGamerPlayer> Items { get; set; }
}

public class WavesQueryPlayerItem : ILocalGamerPlayer
{
    [JsonPropertyName("roleId")]
    public string Id { get; set; }

    [JsonPropertyName("roleName")]
    public string RoleName { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("sex")]
    public int Sex { get; set; }

    [JsonPropertyName("headPhoto")]
    public int HeadPhoto { get; set; }

    [JsonIgnore]
    public string ServerName { get; set; }
    public GameType Type { get; set; } = GameType.Waves;
}

public class QueryRoleInfo
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, string> Data { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonIgnore]
    public List<ILocalGameRole> Items { get; set; }
}

public class Album
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Count")]
    public int Count { get; set; }

    [JsonPropertyName("TotalCount")]
    public int TotalCount { get; set; }
}

public class Base
{
    [JsonPropertyName("Name")]
    public string Name { get; set; }

    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("CreatTime")]
    public long CreatTime { get; set; }

    [JsonPropertyName("ActiveDays")]
    public int ActiveDays { get; set; }

    [JsonPropertyName("Level")]
    public int Level { get; set; }

    [JsonPropertyName("WorldLevel")]
    public int WorldLevel { get; set; }

    [JsonPropertyName("RoleNum")]
    public int RoleNum { get; set; }

    [JsonPropertyName("SoundBox")]
    public int SoundBox { get; set; }

    [JsonPropertyName("Energy")]
    public int Energy { get; set; }

    [JsonPropertyName("MaxEnergy")]
    public int MaxEnergy { get; set; }

    [JsonPropertyName("StoreEnergy")]
    public int StoreEnergy { get; set; }

    [JsonPropertyName("StoreEnergyRecoverTime")]
    public int StoreEnergyRecoverTime
    {
        get => field;
        set
        {
            if (value == 0)
            {
                this.StoreEnergyRecoverEndTime = "已充满";
                field = value;
                return;
            }
            var time = DateTimeOffset.FromUnixTimeMilliseconds(value).ToLocalTime().DateTime;
            if (time > DateTime.Now)
            {
                var dateTimeOffset = time - DateTime.Now;
                this.StoreEnergyRecoverEndTime =
                    $"{dateTimeOffset.Hours}:{dateTimeOffset.Minutes}:{dateTimeOffset.Seconds}S";
                field = value;
                return;
            }
            this.StoreEnergyRecoverEndTime = "已充满";
            field = value;
        }
    }

    [JsonPropertyName("MaxStoreEnergy")]
    public int MaxStoreEnergy { get; set; }

    [JsonPropertyName("EnergyRecoverTime")]
    public long EnergyRecoverTime
    {
        get => field;
        set
        {
            if (value == 0)
            {
                this.EnergyRecoverEndTime = "已充满";
                field = value;
                return;
            }
            var time = DateTimeOffset.FromUnixTimeMilliseconds(value).ToLocalTime().DateTime;
            if (time > DateTime.Now)
            {
                var dateTimeOffset = time - DateTime.Now;
                this.EnergyRecoverEndTime =
                    $"{dateTimeOffset.Hours}:{dateTimeOffset.Minutes}:{dateTimeOffset.Seconds}";
                field = value;
                return;
            }
            this.EnergyRecoverEndTime = "已充满";
            field = value;
        }
    }

    [JsonPropertyName("Liveness")]
    public int Liveness { get; set; }

    [JsonPropertyName("LivenessMaxCount")]
    public int LivenessMaxCount { get; set; }

    [JsonPropertyName("LivenessUnlock")]
    public bool LivenessUnlock { get; set; }

    [JsonPropertyName("ChapterId")]
    public int ChapterId { get; set; }

    [JsonPropertyName("WeeklyInstCount")]
    public int WeeklyInstCount { get; set; }

    [JsonPropertyName("Boxes")]
    public Boxes Boxes { get; set; }

    [JsonPropertyName("BasicBoxes")]
    public BasicBoxes BasicBoxes { get; set; }

    [JsonPropertyName("PhantomBoxes")]
    public PhantomBoxes PhantomBoxes { get; set; }

    [JsonPropertyName("BirthMon")]
    public int BirthMon { get; set; }

    [JsonPropertyName("BirthDay")]
    public int BirthDay { get; set; }

    [JsonIgnore]
    public string EnergyRecoverEndTime { get; set; }

    [JsonIgnore]
    public string StoreEnergyRecoverEndTime { get; set; }
}

public class BasicBoxes
{
    [JsonPropertyName("2")]
    public int _2 { get; set; }

    [JsonPropertyName("1")]
    public int _1 { get; set; }

    [JsonPropertyName("3")]
    public int _3 { get; set; }

    [JsonPropertyName("4")]
    public int _4 { get; set; }
}

public class BattlePass
{
    [JsonPropertyName("Level")]
    public int Level { get; set; }

    [JsonPropertyName("WeekExp")]
    public int WeekExp { get; set; }

    [JsonPropertyName("WeekMaxExp")]
    public int WeekMaxExp { get; set; }

    [JsonPropertyName("IsUnlock")]
    public bool IsUnlock { get; set; }

    [JsonPropertyName("IsOpen")]
    public bool IsOpen { get; set; }

    [JsonPropertyName("Exp")]
    public int Exp { get; set; }

    [JsonPropertyName("ExpLimit")]
    public int ExpLimit { get; set; }
}

public class Boxes
{
    [JsonPropertyName("2")]
    public int _2 { get; set; }

    [JsonPropertyName("1")]
    public int _1 { get; set; }

    [JsonPropertyName("3")]
    public int _3 { get; set; }

    [JsonPropertyName("4")]
    public int _4 { get; set; }
}

public class Decoration
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Quality")]
    public int Quality { get; set; }

    [JsonPropertyName("PartId")]
    public int PartId { get; set; }
}

public class EquipSkin
{
    [JsonPropertyName("SkinId")]
    public int SkinId { get; set; }

    [JsonPropertyName("Quality")]
    public int Quality { get; set; }
}

public class FrameModel
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Quality")]
    public int Quality { get; set; }
}

public class MotorData
{
    [JsonPropertyName("Level")]
    public int Level { get; set; }

    [JsonPropertyName("Exp")]
    public int Exp { get; set; }

    [JsonPropertyName("NextExp")]
    public int NextExp { get; set; }

    [JsonPropertyName("Skins")]
    public List<Skin> Skins { get; set; }

    [JsonPropertyName("Stickers")]
    public List<Sticker> Stickers { get; set; }

    [JsonPropertyName("Decorations")]
    public List<Decoration> Decorations { get; set; }

    [JsonPropertyName("Frames")]
    public List<FrameModel> Frames { get; set; }

    [JsonPropertyName("EquipSkin")]
    public EquipSkin EquipSkin { get; set; }
}

public class MusicData
{
    [JsonPropertyName("Albums")]
    public List<Album> Albums { get; set; }
}

public class PhantomBoxes
{
    [JsonPropertyName("1")]
    public int _1 { get; set; }

    [JsonPropertyName("2")]
    public int _2 { get; set; }

    [JsonPropertyName("3")]
    public int _3 { get; set; }
}

public class WavesLocalGameRoleItem : ILocalGameRole
{
    [JsonPropertyName("MotorData")]
    public MotorData MotorData { get; set; }

    [JsonPropertyName("MusicData")]
    public MusicData MusicData { get; set; }

    [JsonPropertyName("Base")]
    public Base Base { get; set; }

    [JsonPropertyName("BattlePass")]
    public BattlePass BattlePass { get; set; }

    [JsonIgnore]
    public string ServerName { get; set; }
    public GameType Type { get; set; }
}

public class Skin
{
    [JsonPropertyName("SkinId")]
    public int SkinId { get; set; }

    [JsonPropertyName("Quality")]
    public int Quality { get; set; }
}

public class Sticker
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Quality")]
    public int Quality { get; set; }

    [JsonPropertyName("PartId")]
    public int PartId { get; set; }
}
