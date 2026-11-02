using System.Globalization;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using Waves.Api.Models.Record;

namespace Waves.Api.Exporting;

/// <summary>所有档案导出到一个工作簿：档案索引 + 每个档案的明细。</summary>
public static class WavesRecordExcelExporter
{
    public static (int Archives, int Records) Write(
        string filePath,
        IReadOnlyList<WavesAnalysisPlayerCard> cards,
        CancellationToken token = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (
            !string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase)
        )
            throw new ArgumentException("导出文件的扩展名应为 .xlsx。", nameof(filePath));
        if (cards.Count == 0)
            throw new InvalidOperationException("暂无本地档案可导出。");
        token.ThrowIfCancellationRequested();

        var sheets = new Dictionary<string, object>();
        var index = new List<Dictionary<string, object>>();
        sheets.Add("档案索引", index);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "档案索引",
            "History",
        };
        var total = 0;
        foreach (var card in cards)
        {
            token.ThrowIfCancellationRequested();
            var name = string.IsNullOrWhiteSpace(card.RecordName) ? "未命名档案" : card.RecordName;
            var sheetName = UniqueSheetName(name, usedNames);
            var count = card.Items.Sum(pool => pool.Resource.Count());
            if (count > 1_048_575)
                throw new InvalidOperationException(
                    $"档案“{name}”超过 Excel 单页记录上限，请拆分后导出。"
                );
            total = checked(total + count);
            index.Add(
                new()
                {
                    ["档案名称"] = name,
                    ["SessionId"] = card.SessionId ?? "",
                    ["工作表"] = sheetName,
                    ["记录数"] = count,
                    ["最后更新时间"] = card.LastUpdater.ToString(
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture
                    ),
                    ["更新类型"] = card.UpdateType ?? "",
                    ["更新来源"] = card.UpdateSource ?? "",
                    ["档案版本"] = card.Version,
                }
            );
            sheets.Add(sheetName, Rows(card, token));
        }

        // 先生成完整工作簿再替换目标，导出失败时不留下半成品或破坏已有文件。
        var path = Path.GetFullPath(filePath);
        var tempPath = Path.Combine(Path.GetDirectoryName(path)!, $".{Guid.NewGuid():N}.xlsx");
        try
        {
            MiniExcel.SaveAs(tempPath, sheets, configuration: new OpenXmlConfiguration
            {
                DynamicColumns =
                [
                    new DynamicExcelColumn("档案名称") { Width = 28 },
                    new DynamicExcelColumn("SessionId") { Width = 26 },
                    new DynamicExcelColumn("工作表") { Width = 28 },
                    new DynamicExcelColumn("记录数") { Width = 12 },
                    new DynamicExcelColumn("最后更新时间") { Width = 22 },
                    new DynamicExcelColumn("更新类型") { Width = 18 },
                    new DynamicExcelColumn("更新来源") { Width = 28 },
                    new DynamicExcelColumn("档案版本") { Width = 12 },
                    new DynamicExcelColumn("卡池类型") { Width = 12 },
                    new DynamicExcelColumn("名称") { Width = 28 },
                    new DynamicExcelColumn("类型") { Width = 12 },
                    new DynamicExcelColumn("星级") { Width = 10 },
                    new DynamicExcelColumn("时间") { Width = 22 },
                    new DynamicExcelColumn("数量") { Width = 10 },
                    new DynamicExcelColumn("资源ID") { Width = 16 },
                    new DynamicExcelColumn("卡池") { Width = 28 },
                ],
            });
            token.ThrowIfCancellationRequested();
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        return (cards.Count, total);
    }

    private static IEnumerable<Dictionary<string, object>> Rows(
        WavesAnalysisPlayerCard card,
        CancellationToken token
    )
    {
        foreach (var pool in card.Items.OrderBy(pool => pool.PoolType))
        {
            foreach (var record in pool.Resource.OrderByDescending(record => record.RecordTime))
            {
                token.ThrowIfCancellationRequested();
                yield return new()
                {
                    ["卡池类型"] = pool.PoolType,
                    ["名称"] = record.Name ?? "",
                    ["类型"] = record.ResourceType ?? "",
                    ["星级"] = record.QualityLevel,
                    ["时间"] = record.Time ?? "",
                    ["数量"] = record.Count,
                    ["资源ID"] = record.ResourceId,
                    ["卡池"] = record.CardPoolType ?? "",
                };
            }
        }
    }

    private static string UniqueSheetName(string value, HashSet<string> used)
    {
        var name = new string(
            value.Select(c => char.IsControl(c) || "[]:*?/\\".Contains(c) ? '_' : c).ToArray()
        )
            .Trim()
            .Trim('\'');
        if (string.IsNullOrWhiteSpace(name))
            name = "未命名档案";
        var baseName = Truncate(name, 31);
        var result = baseName;
        for (var number = 2; !used.Add(result); number++)
        {
            var suffix = $" ({number})";
            result = Truncate(baseName, 31 - suffix.Length).TrimEnd('\'') + suffix;
        }
        return result;
    }

    private static string Truncate(string value, int length)
    {
        if (value.Length <= length)
            return value;
        if (char.IsHighSurrogate(value[length - 1]))
            length--;
        return value[..length].TrimEnd('\'');
    }
}
