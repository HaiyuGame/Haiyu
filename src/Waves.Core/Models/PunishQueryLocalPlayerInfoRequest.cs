using System.Text.Json.Serialization;
using Waves.Core.Contracts;
using Waves.Core.Models.Enums;

namespace Waves.Core.Models;


public class PunishQueryPlayerItem : ILocalGamerPlayer
{

    [JsonPropertyName("playerId")]
    public int PlayerId
    {
        get => field;
        set
        {
            this.Id = value.ToString();
            //赋值ID
            field = value;
        }
    }

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; }

    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; }

    [JsonPropertyName("playerHonorLevel")]
    public int PlayerHonorLevel { get; set; }

    [JsonPropertyName("serverId")]
    public int ServerId { get; set; }
    public GameType Type { get; set; } = GameType.Punish;

    [JsonIgnore]
    public string ServerName { get;  set; }
    public string Id { get; set; }
}


public class PunishLocalGameRoleItem : ILocalGameRole
{
    [JsonPropertyName("roleId")]
    public int Id { get; set; }

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; }

    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; }

    [JsonPropertyName("playerHonorLevel")]
    public int PlayerHonorLevel { get; set; }

    [JsonPropertyName("actionPoint")]
    public int ActionPoint { get; set; }

    [JsonPropertyName("actionPointTotal")]
    public int ActionPointTotal { get; set; }

    [JsonPropertyName("actionPointFullTime")]
    public int ActionPointFullTime { get; set; }

    [JsonPropertyName("actionPointNextExpiredTime")]
    public int ActionPointNextExpiredTime { get; set; }

    [JsonPropertyName("dormQuestUnlocked")]
    public bool DormQuestUnlocked { get; set; }

    [JsonPropertyName("activenessUnlocked")]
    public bool ActivenessUnlocked { get; set; }

    [JsonPropertyName("bossSingleStatus")]
    public int BossSingleStatus { get; set; }

    [JsonPropertyName("bossSingleUnlocked")]
    public bool BossSingleUnlocked { get; set; }

    [JsonPropertyName("arenaStatus")]
    public int ArenaStatus { get; set; }

    [JsonPropertyName("arenaUnlocked")]
    public bool ArenaUnlocked { get; set; }

    [JsonPropertyName("transfiniteStatus")]
    public int TransfiniteStatus { get; set; }

    [JsonPropertyName("transfiniteUnlocked")]
    public bool TransfiniteUnlocked { get; set; }

    [JsonPropertyName("strongholdStatus")]
    public int StrongholdStatus { get; set; }

    [JsonPropertyName("strongholdUnlocked")]
    public bool StrongholdUnlocked { get; set; }

    [JsonPropertyName("serverTimezone")]
    public int ServerTimezone { get; set; }
    public GameType Type { get; set; }

    [JsonIgnore]
    public string ServerName { get; set; }
}