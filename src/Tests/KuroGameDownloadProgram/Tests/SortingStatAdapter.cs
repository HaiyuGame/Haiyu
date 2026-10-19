using System.Globalization;
using System.Text;
using ScoringService.Models;
using ScoringService.Models.Enums;

namespace KuroGameDownloadProgram.Tests;

/// <summary>计算测试使用的库街区属性转换器，不依赖账号或网络。</summary>
internal static class SortingStatAdapter
{
    /// <summary>按名称、数值单位和规则用途映射属性；未知属性保留为独立编号，交由评分明细提示。</summary>
    internal static List<StatValue> Convert(
        IEnumerable<(string Name, string Value)> properties,
        EntryGroup template,
        EntryUsage usage)
    {
        var rules = template.Items.Where(x => x.Usage == usage).ToArray();
        var result = new List<StatValue>();
        foreach (var (name, rawValue) in properties)
        {
            var (type, value) = ParseValue(rawValue);
            var normalizedName = NormalizeName(name);
            if (normalizedName.Length == 0)
                throw new FormatException("库街区属性名称为空。");

            var matches = rules.Where(x => x.Type == type &&
                (NormalizeName(x.EntryName) == normalizedName || x.EntryNo == name.Trim())).ToArray();
            if (matches.Length > 1)
                throw new InvalidOperationException($"属性“{name}”匹配了多条规则，请检查模板名称。");

            var id = matches.Length == 1 ? matches[0].EntryNo : $"unmapped:{normalizedName}";
            if (result.Any(x => x.EntryNo == id && x.Type == type))
                throw new InvalidOperationException($"属性“{name}”重复，测试中不自动合并。");
            result.Add(new() { EntryNo = id, Type = type, Value = value });
        }
        return result;
    }

    /// <summary>解析固定数值和百分比，统一全角字符；10% 返回百分比类型和数值 10。</summary>
    internal static (EntryType Type, decimal Value) ParseValue(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var text = raw.Normalize(NormalizationForm.FormKC).Trim();
        var isPercentage = text.EndsWith('%');
        if (isPercentage)
            text = text[..^1].TrimEnd();
        // 仅允许规范千位分组，避免将 1,2 之类格式误读成 12。
        var number = text.TrimStart('+');
        var integerPart = number.Split('.')[0];
        if (integerPart.Contains(','))
        {
            var groups = integerPart.Split(',');
            if (groups[0].Length is < 1 or > 3 ||
                groups.Skip(1).Any(x => x.Length != 3) ||
                groups.Any(x => x.Any(c => c is < '0' or > '9')))
                throw new FormatException("库街区属性的千位分组格式错误。");
        }
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new FormatException("库街区属性数值格式错误或为负数。");
        return (isPercentage ? EntryType.Percentage : EntryType.Value, value);
    }

    /// <summary>统一库街区与模板的常见名称差异，数值类型仍单独匹配。</summary>
    private static string NormalizeName(string name)
    {
        var text = string.Concat(name.Normalize(NormalizationForm.FormKC).Where(c => !char.IsWhiteSpace(c)))
            .Replace("百分比", "", StringComparison.Ordinal)
            .Replace("加成", "", StringComparison.Ordinal)
            .Replace("%", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal);
        if (text.StartsWith("固定", StringComparison.Ordinal))
            text = text[2..];
        return text switch
        {
            "攻击" => "攻击力",
            "防御" => "防御力",
            "生命" => "生命值",
            _ => text
        };
    }
}
