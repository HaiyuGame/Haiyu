using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;
using System.Timers;
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
    public string RoleName { get; set; }

    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; }

    [JsonPropertyName("playerHonorLevel")]
    public int PlayerHonorLevel { get; set; }

    [JsonPropertyName("serverId")]
    public int ServerId { get; set; }
    public GameType Type { get; set; } = GameType.Punish;

    [JsonIgnore]
    public string ServerName { get; set; }
    public string Id { get; set; }
}

public partial class PunishLocalGameRoleItem : ObservableObject, ILocalGameRole
{
    [JsonPropertyName("roleId")]
    [ObservableProperty]
    public partial int RoleId { get; set; }

    [JsonPropertyName("playerName")]
    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [JsonPropertyName("playerLevel")]
    [ObservableProperty]
    public partial int PlayerLevel { get; set; }

    [JsonPropertyName("playerHonorLevel")]
    [ObservableProperty]
    public partial int PlayerHonorLevel { get; set; }

    [JsonPropertyName("actionPoint")]
    [ObservableProperty]
    public partial int ActionPoint { get; set; }

    [JsonPropertyName("actionPointTotal")]
    [ObservableProperty]
    public partial int ActionPointTotal { get; set; }

    [JsonPropertyName("actionPointFullTime")]
    [ObservableProperty]
    public partial int ActionPointFullTime { get; set; }

    [JsonPropertyName("actionPointNextExpiredTime")]
    [ObservableProperty]
    public partial int ActionPointNextExpiredTime { get; set; }


    [JsonIgnore]
    [ObservableProperty]
    public partial string ActionPointNextExpiredEndTime { get; set; }

    [JsonPropertyName("dormQuestUnlocked")]
    [ObservableProperty]
    public partial bool DormQuestUnlocked { get; set; }

    [JsonPropertyName("dormQuestAchievedCount")]
    [ObservableProperty]
    public partial int DormQuestAchievedCount { get; set; }

    [JsonPropertyName("activenessUnlocked")]
    [ObservableProperty]
    public partial bool ActivenessUnlocked { get; set; }

    [JsonPropertyName("activeness")]
    [ObservableProperty]
    public partial int Activeness { get; set; }

    [JsonPropertyName("activenessTotal")]
    public int ActivenessTotal { get; set; }

    [JsonPropertyName("bossSingleStatus")]
    [ObservableProperty]
    public partial int BossSingleStatus { get; set; }

    [JsonPropertyName("bossSingleUnlocked")]
    [ObservableProperty]
    public partial bool BossSingleUnlocked { get; set; }

    [JsonPropertyName("bossSingleRewardCount")]
    public int BossSingleRewardCount { get; set; }

    [JsonPropertyName("bossSingleRewardCountTotal")]
    [ObservableProperty]
    public partial int BossSingleRewardCountTotal { get; set; }

    [JsonPropertyName("bossSingleLevelTypeSelected")]
    [ObservableProperty]
    public partial bool BossSingleLevelTypeSelected { get; set; }

    [JsonPropertyName("bossSingleEndTime")]
    [ObservableProperty]
    public partial int BossSingleEndTime { get; set; }

    [JsonPropertyName("arenaStatus")]
    [ObservableProperty]
    public partial int ArenaStatus { get; set; }

    [JsonPropertyName("arenaUnlocked")]
    [ObservableProperty]
    public partial bool ArenaUnlocked { get; set; }

    [JsonPropertyName("arenaRewardCount")]
    [ObservableProperty]
    public partial int ArenaRewardCount { get; set; }

    [JsonPropertyName("arenaRewardCountTotal")]
    [ObservableProperty]
    public partial int ArenaRewardCountTotal { get; set; }

    [JsonPropertyName("arenaFightStartTime")]
    [ObservableProperty]
    public partial int ArenaFightStartTime { get; set; }

    [JsonPropertyName("arenaEndTime")]
    [ObservableProperty]
    public partial int ArenaEndTime { get; set; }

    [JsonPropertyName("transfiniteStatus")]
    [ObservableProperty]
    public partial int TransfiniteStatus { get; set; }

    [JsonPropertyName("transfiniteUnlocked")]
    [ObservableProperty]
    public partial bool TransfiniteUnlocked { get; set; }

    [JsonPropertyName("strongholdStatus")]
    [ObservableProperty]
    public partial int StrongholdStatus { get; set; }

    [JsonPropertyName("strongholdUnlocked")]
    [ObservableProperty]
    public partial bool StrongholdUnlocked { get; set; }

    [JsonPropertyName("serverTimezone")]
    [ObservableProperty]
    public partial int ServerTimezone { get; set; }

    [JsonIgnore]
    [ObservableProperty]
    public partial string ServerName { get; set; }

    [JsonIgnore]
    public GameType Type { get; set; }

    [JsonIgnore]
    [ObservableProperty]
    public partial string BossSingleEndtimeText { get;  set; }

    [JsonIgnore]
    [ObservableProperty]
    public partial string ArenaEndTimeText { get;  set; }

    partial void OnActionPointNextExpiredTimeChanged(int value)
    {
        if (value == 0)
        {
            this.ActionPointNextExpiredEndTime = "已充满";
        }
        else
        {
            var time = DateTimeOffset.FromUnixTimeMilliseconds(value).ToLocalTime().DateTime;
            if (time > DateTime.Now)
            {
                var dateTimeOffset = time - DateTime.Now;
                this.ActionPointNextExpiredEndTime =
                    $"{dateTimeOffset.Hours}:{dateTimeOffset.Minutes}:{dateTimeOffset.Seconds}";
                return;
            }
            this.ActionPointNextExpiredEndTime = "已充满";
        }
    }

    partial void OnBossSingleEndTimeChanged(int value)
    {
        if (value == 0)
        {
            this.BossSingleEndtimeText = "已充满";
        }
        else
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(value).ToLocalTime().DateTime;
            if (time > DateTime.Now)
            {
                var dateTimeOffset = time - DateTime.Now;
                this.BossSingleEndtimeText =
                    $"{dateTimeOffset.Hours:D2}:{dateTimeOffset.Minutes:D2}:{dateTimeOffset.Seconds:D2}";
                return;
            }
            this.BossSingleEndtimeText = "已充满";
        }
    }

    partial void OnArenaEndTimeChanged(int value)
    {
        CalcelArenaTime();
    }

    partial void OnArenaFightStartTimeChanged(int value)
    {
        CalcelArenaTime();
    }


    void CalcelArenaTime()
    {
        if(ArenaEndTime == 0 || ArenaFightStartTime == 0)
        {
            return;
        }
        var starTime = DateTimeOffset.FromUnixTimeMilliseconds(ArenaFightStartTime).ToLocalTime().DateTime;
        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(ArenaEndTime).ToLocalTime().DateTime;
        var dateTimeOffset = endTime - starTime;
        this.ArenaEndTimeText =
            $"{dateTimeOffset.Hours}:{dateTimeOffset.Minutes}:{dateTimeOffset.Seconds}";
        return;
    }
}
