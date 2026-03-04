using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Waves.Api.Models.Launcher;

[JsonSerializable(typeof(CdnList))]
[JsonSerializable(typeof(Changelog))]
[JsonSerializable(typeof(ComponentList))]
[JsonSerializable(typeof(Defaults))]
[JsonSerializable(typeof(Experiment))]
[JsonSerializable(typeof(FunctionCode))]
[JsonSerializable(typeof(Resource))]
[JsonSerializable(typeof(ResourcesGray))]
[JsonSerializable(typeof(LIndex))]
[JsonSerializable(typeof(LauncherBackgroundData))]
[JsonSerializable(typeof(KRSDKLauncherCache))]
[JsonSerializable(typeof(List<KRSDKLauncherCache>))]
public partial class LauncherConfig:JsonSerializerContext
{

}


public class Changelog
{
    [JsonPropertyName("zh-Hans")]
    public string ZhHans { get; set; }
}

public class ComponentList
{
    [JsonPropertyName("fileCheckListMd5")]
    public string FileCheckListMd5 { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("Id")]
    public string Id { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }
}

public class Defaults
{
    [JsonPropertyName("cdnList")]
    public List<CdnList> CdnList { get; set; }

    [JsonPropertyName("changelog")]
    public Changelog Changelog { get; set; }

    [JsonPropertyName("resource")]
    public Resource Resource { get; set; }
}

public class Experiment
{
    [JsonPropertyName("componentList")]
    public List<ComponentList> ComponentList { get; set; }
}

public class FunctionCode
{
    [JsonPropertyName("background")]
    public string Background { get; set; }
}

public class Resource
{
    [JsonPropertyName("fileCheckListMd5")]
    public string FileCheckListMd5 { get; set; }

    [JsonPropertyName("md5")]
    public string Md5 { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}

public class ResourcesGray
{
    [JsonPropertyName("graySwitch")]
    public int GraySwitch { get; set; }
}

public class LIndex
{
    [JsonPropertyName("resourcesGray")]
    public ResourcesGray ResourcesGray { get; set; }

    [JsonPropertyName("default")]
    public Defaults Default { get; set; }

    [JsonPropertyName("functionCode")]
    public FunctionCode FunctionCode { get; set; }

    [JsonPropertyName("experiment")]
    public Experiment Experiment { get; set; }

    [JsonPropertyName("crashInitSwitch")]
    public int CrashInitSwitch { get; set; }
}
