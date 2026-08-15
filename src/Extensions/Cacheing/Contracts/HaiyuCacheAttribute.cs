namespace Cacheing.Contracts;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class HaiyuCacheAttribute : Attribute
{
    public HaiyuCacheAttribute(string? key = null)
    {
        Key = key;
    }

    public string? Key { get; }

    public int ExpirationSeconds { get; set; } = 300;

    public string TargetName { get; set; } = string.Empty;
}
