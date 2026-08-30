using System.Text.Json.Serialization;

namespace ABI.Models;

/// <summary>
/// ABI 进行系统自检
/// </summary>
public class ABISystemConfigRequest
{
    [JsonPropertyName("baseDirectory")]
    public string BaseDirectory { get; set;  }


    [JsonPropertyName("systemSettingPath")]
    public string SystemSettingPath { get; set;  }
}

/// <summary>
/// ABI 进行系统自检进度
/// </summary>
public class ABISystemConfigProgress
{
    [JsonPropertyName("isRuning")]
    public bool IsRuning { get; set; }

    [JsonPropertyName("isComplete")]
    public bool IsComplete { get; set;  }
}
