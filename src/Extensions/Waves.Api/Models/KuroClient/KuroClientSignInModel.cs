using System.Text.Json.Serialization;

namespace Waves.Api.Models.KuroClient;

public class KuroClientSignInModel
{
    [JsonPropertyName("continueDays")]
    public int ContinueDays { get; set; }

    [JsonPropertyName("gainVoList")]
    public List<KuroClientSignInItem> GainVoList { get; set; }

    [JsonPropertyName("geeTest")]
    public bool GeeTest { get; set; }

    [JsonPropertyName("totalSignInDay")]
    public int TotalSignInDay { get; set; }
}

public class KuroClientSignInItem
{
    [JsonPropertyName("gainTyp")]
    public int GainTyp { get; set; }

    [JsonPropertyName("gainValue")]
    public int GainValue { get; set; }
}
