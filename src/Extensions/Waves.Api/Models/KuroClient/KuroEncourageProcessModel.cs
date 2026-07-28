using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Waves.Api.Models.KuroClient;

public class EncourageDailyTask
{
    [JsonPropertyName("completeTimes")]
    public int CompleteTimes { get; set; }

    [JsonPropertyName("gainGold")]
    public int GainGold { get; set; }

    [JsonPropertyName("needActionTimes")]
    public int NeedActionTimes { get; set; }

    [JsonPropertyName("process")]
    public double Process { get; set; }

    [JsonPropertyName("remark")]
    public string Remark { get; set; }

    [JsonPropertyName("skipType")]
    public int SkipType { get; set; }

    [JsonPropertyName("times")]
    public int Times { get; set; }
}

public class KuroEncourageProcessModel
{
    [JsonPropertyName("currentDailyGold")]
    public int CurrentDailyGold { get; set; }

    [JsonPropertyName("growTask")]
    public List<EncourageGrowTask> GrowTask { get; set; }

    [JsonPropertyName("dailyTask")]
    public List<EncourageDailyTask> DailyTask { get; set; }

    [JsonPropertyName("maxDailyGold")]
    public int MaxDailyGold { get; set; }
}

public class EncourageGrowTask
{
    [JsonPropertyName("completeTimes")]
    public int CompleteTimes { get; set; }

    [JsonPropertyName("gainGold")]
    public int GainGold { get; set; }

    [JsonPropertyName("needActionTimes")]
    public int NeedActionTimes { get; set; }

    [JsonPropertyName("process")]
    public double Process { get; set; }

    [JsonPropertyName("remark")]
    public string Remark { get; set; }

    [JsonPropertyName("skipType")]
    public int SkipType { get; set; }

    [JsonPropertyName("times")]
    public int Times { get; set; }
}
