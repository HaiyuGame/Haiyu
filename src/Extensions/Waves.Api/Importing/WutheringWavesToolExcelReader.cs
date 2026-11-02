using System.Globalization;
using MiniExcelLibs;
using Waves.Api.Models.Record;
using Waves.Api.Models.Wrappers;

namespace Waves.Api.Importing;

/// <summary>读取 WutheringWavesTool 导出的工作簿，不将文件名或工作表名称视为目标账号。</summary>
public static class WutheringWavesToolExcelReader
{
    public static WavesAnalysisPlayerCard Read(string filePath, CancellationToken token = default)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("请选择 WutheringWavesTool 导出的 .xlsx 文件。");

        var pools = new List<WavesAnalysisPlayerCardItem>();
        foreach (var sheetName in MiniExcel.GetSheetNames(filePath))
        {
            token.ThrowIfCancellationRequested();
            var poolType = GetPoolType(sheetName.Trim());
            var records = new List<RecordCardItemWrapper>();
            var row = 1;
            // 返回值虽声明为 dynamic，但这里只做静态字典访问，不触发 DLR 或反射映射。
            foreach (object value in MiniExcel.Query(filePath, useHeaderRow: true, sheetName: sheetName))
            {
                token.ThrowIfCancellationRequested();
                row++;
                var cells = value as IDictionary<string, object>
                    ?? throw new InvalidDataException($"工作表“{sheetName}”的记录格式无效。");
                if (poolType == 0)
                {
                    if (cells.ContainsKey("资源ID"))
                        throw new InvalidDataException($"尚未支持卡池工作表：{sheetName}");
                    break; // 忽略账号说明等非卡池工作表。
                }
                foreach (var header in new[] { "名称", "类型", "星级", "时间", "数量", "资源ID", "卡池" })
                {
                    if (!cells.ContainsKey(header))
                        throw new InvalidDataException($"工作表“{sheetName}”缺少列：{header}");
                }
                string Cell(string header) => Convert.ToString(cells[header], CultureInfo.InvariantCulture)?.Trim() ?? "";
                if (cells.Values.All(cell => string.IsNullOrWhiteSpace(Convert.ToString(cell, CultureInfo.InvariantCulture))))
                    continue;

                try
                {
                    int Integer(string header)
                    {
                        // Excel 将资源 ID 保存为浮点数，有些文件使用科学记数法。
                        if (!decimal.TryParse(Cell(header), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                            || value != decimal.Truncate(value) || value <= 0 || value > int.MaxValue)
                            throw new FormatException($"{header}必须是正整数");
                        return (int)value;
                    }

                    var name = Cell("名称");
                    var type = Cell("类型");
                    var poolName = Cell("卡池");
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(poolName))
                        throw new FormatException("名称、类型和卡池不能为空");
                    var quality = Integer("星级");
                    if (quality is < 3 or > 5)
                        throw new FormatException("星级应为 3、4 或 5");
                    var rawTime = Cell("时间");
                    DateTime time;
                    if (cells["时间"] is DateTime date)
                        time = date;
                    else if (!DateTime.TryParseExact(rawTime, ["yyyy-MM-dd HH:mm:ss", "yyyy/M/d H:mm:ss", "yyyy-MM-dd H:mm:ss"],
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
                    {
                        throw new FormatException("时间格式应为 yyyy-MM-dd HH:mm:ss");
                    }

                    records.Add(new RecordCardItemWrapper
                    {
                        Name = name, ResourceType = type, QualityLevel = quality,
                        ResourceId = Integer("资源ID"), Count = Integer("数量"),
                        // 保留来源中的卡池文本，和接口记录的 CardPoolType 字段一致。
                        CardPoolType = poolName,
                        Time = time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        RecordTime = time, Year = time.Year, Month = time.Month, Day = time.Day,
                    });
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                {
                    throw new InvalidDataException($"工作表“{sheetName}”第 {row} 行：{ex.Message}", ex);
                }
            }
            if (records.Count > 0)
                pools.Add(new() { PoolType = poolType, Resource = records.OrderByDescending(x => x.RecordTime).ToArray() });
        }

        if (pools.Count == 0)
            throw new InvalidDataException("文件中没有可导入的 WutheringWavesTool 抽卡记录。");
        return new() { SessionId = "", Items = pools };
    }

    private static int GetPoolType(string name) => name switch
    {
        "角色活动唤取" => 1,
        "武器活动唤取" => 2,
        "角色常驻唤取" => 3,
        "武器常驻唤取" => 4,
        "新手唤取" => 5,
        "新手自选唤取" => 6,
        "新手自选唤取（感恩定向唤取）" or "感恩定向唤取" => 7,
        "角色新旅唤取" => 8,
        "武器新旅唤取" => 9,
        "角色联动唤取" => 10,
        "武器联动唤取" => 11,
        _ => 0,
    };
}
