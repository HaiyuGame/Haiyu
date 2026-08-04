using System.Text.Json.Serialization.Metadata;

namespace Waves.Settings;

/// <summary>
/// 标记设置项，由 Haiyu.Analyzers.SettingsGenerator 生成 Get/Set partial 方法。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SettingsAttribute<T> : Attribute
{
    public string? Name { get; set; }
    public Type? Type { get; set; }
    public bool Nullable { get; set; }
    public string? DefaultValue { get; set; }
    public JsonTypeInfo<T>? JsonTypeInfo { get; set; }
    public Type? JsonTypeInfoContextType { get; set; }
    public string? JsonTypeInfoPropertyName { get; set; }
}
