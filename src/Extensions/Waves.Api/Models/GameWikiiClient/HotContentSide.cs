using System.Text.Json.Serialization;

namespace Waves.Api.Models.GameWikiiClient;


public class CountDown
{
    [JsonPropertyName("dateRange")]
    public List<string> DateRange { get; set; }

    [JsonPropertyName("repeat")]
    public Repeat Repeat { get; set; }

    [JsonPropertyName("precision")]
    public string Precision { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }
}

public class DataRanges
{
    [JsonPropertyName("progressType")]
    public int ProgressType { get; set; }

    [JsonPropertyName("dataRange")]
    public List<string> DataRange { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}

public class SideEventLinkConfig
{
    [JsonPropertyName("linkUrl")]
    public string LinkUrl { get; set; }

    [JsonPropertyName("linkType")]
    public int LinkType { get; set; }

    [JsonPropertyName("entryId")]
    public string EntryId { get; set; }
}

public class Repeat
{
    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("isNeverEnd")]
    public bool IsNeverEnd { get; set; }

    [JsonPropertyName("repeatInterval")]
    public int RepeatInterval { get; set; }

    [JsonPropertyName("dataRanges")]
    public List<DataRanges> DataRanges { get; set; }
}

public class HotContentSide
{
    [JsonPropertyName("linkConfig")]
    public SideEventLinkConfig LinkConfig { get; set; }

    [JsonPropertyName("contentUrl")]
    public string ContentUrl { get; set; }

    [JsonPropertyName("contentUrlRealName")]
    public string ContentUrlRealName { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("countDown")]
    public CountDown CountDown { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}

