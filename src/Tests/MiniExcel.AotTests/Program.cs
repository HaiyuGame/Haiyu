using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using MiniExcelLibs;
using Waves.Api.Importing;
using Waves.Api.Exporting;
using Waves.Api.Models.Record;
using Waves.Api.Models.Wrappers;

if (args.Length != 1)
    throw new ArgumentException("Usage: MiniExcel.AotTests <xlsx-path>");
var path = Path.GetFullPath(args[0]);
var expected = new Dictionary<string, int>
{
    ["角色活动唤取"] = 1013, ["武器活动唤取"] = 319,
    ["角色常驻唤取"] = 29, ["武器常驻唤取"] = 182,
    ["新手唤取"] = 0, ["新手自选唤取"] = 0,
    ["新手自选唤取（感恩定向唤取）"] = 0,
    ["角色新旅唤取"] = 0, ["武器新旅唤取"] = 0,
};
string[] columns = ["名称", "类型", "星级", "时间", "数量", "资源ID", "卡池"];
var sheets = MiniExcel.GetSheetNames(path);
Check(sheets.Count == 10, "worksheet count");
var total = 0;
var chinese = false;
var ids = new HashSet<int>();
foreach (var (sheet, expectedRows) in expected)
{
    Check(sheets.Contains(sheet), $"missing sheet {sheet}");
    var count = 0;
    // 不使用 dynamic 成员访问、Query<T> 或反射对象映射。
    foreach (object value in MiniExcel.Query(path, useHeaderRow: true, sheetName: sheet))
    {
        var row = value as IDictionary<string, object>
            ?? throw new InvalidDataException("Expected dictionary rows");
        foreach (var column in columns)
            Check(row.ContainsKey(column), $"missing column {column}");
        string Text(string column) => Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? "";
        int Integer(string column)
        {
            var number = Convert.ToDecimal(row[column], CultureInfo.InvariantCulture);
            Check(number == decimal.Truncate(number) && number > 0, $"invalid integer {column}");
            return checked((int)number);
        }
        Check(Text("名称").Length > 0 && Text("卡池").Length > 0, "empty strings");
        chinese |= Text("名称").Any(c => c > 127);
        var quality = Integer("星级");
        Check(quality is >= 3 and <= 5, "quality");
        Check(Integer("数量") == 1, "count");
        ids.Add(Integer("资源ID"));
        var time = row["时间"] is DateTime date
            ? date
            : DateTime.ParseExact(Text("时间"), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Check(time.Year is >= 2020 and <= 2030, "timestamp");
        count++;
    }
    Check(count == expectedRows, $"{sheet}: expected {expectedRows}, got {count}");
    Console.WriteLine($"PASS: {sheet}: {count} rows");
    total += count;
}
Check(total == 1543 && chinese && ids.Count > 1, "all records decoded");

var imported = WutheringWavesToolExcelReader.Read(path);
Check(imported.SessionId == "", "parser does not infer target account from filename");
Check(imported.Items.Count == 4, "four non-empty pools");
Check(imported.Items.Sum(pool => pool.Resource.Count()) == total, "production parser record count");
foreach (var pool in imported.Items)
{
    Check(pool.Resource.All(record => record.ResourceId > 0 && record.RecordTime != default), "production model mapping");
    Check(pool.Resource.All(record => record.Time == record.RecordTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)), "production timestamps");
}
Console.WriteLine("PASS: production importer maps 1543 records into 4 pools without assigning a target account");

var fixtureDirectory = Path.Combine(Path.GetTempPath(), "Haiyu-ExcelImportTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureDirectory);
try
{
    var exportPath = Path.Combine(fixtureDirectory, "all-records.xlsx");
    imported.RecordName = "同名/档案:*?[]" + new string('长', 40);
    imported.SessionId = "000101406769";
    var additional = new WavesAnalysisPlayerCard
    {
        RecordName = imported.RecordName, SessionId = "other-account",
        Items = [new() { PoolType = 1, Resource = [new RecordCardItemWrapper
        {
            Name = "=1+1", ResourceId = 1234, QualityLevel = 5, Count = 1,
            ResourceType = "角色", CardPoolType = "角色精准调谐", Time = "2026-10-28 12:00:00",
            RecordTime = new DateTime(2026, 10, 28, 12, 0, 0),
        }] }],
    };
    var exported = WavesRecordExcelExporter.Write(exportPath,
    [
        imported, additional,
        new() { RecordName = "档案索引", SessionId = "empty", Items = [] },
        new() { RecordName = "", SessionId = "unnamed", Items = [] },
    ]);
    Check(exported.Archives == 4 && exported.Records == 1544, "export statistics");
    var exportSheets = MiniExcel.GetSheetNames(exportPath);
    Check(exportSheets.Count == 5, "index and four archive sheets");
    Check(exportSheets.Distinct(StringComparer.OrdinalIgnoreCase).Count() == exportSheets.Count, "unique sheet names");
    Check(exportSheets.All(name => name.Length <= 31 && !name.Any(c => "[]:*?/\\".Contains(c))), "valid sheet names");
    var indexRows = MiniExcel.Query(exportPath, useHeaderRow: true, sheetName: "档案索引")
        .Cast<object>().Cast<IDictionary<string, object>>().ToList();
    Check(indexRows.Count == 4 && (string)indexRows[0]["SessionId"] == "000101406769", "index preserves text identifiers");
    Check((string)indexRows[0]["档案名称"] == imported.RecordName, "full archive name retained in index");
    var detailRows = MiniExcel.Query(exportPath, useHeaderRow: true, sheetName: (string)indexRows[0]["工作表"])
        .Cast<object>().Cast<IDictionary<string, object>>().ToList();
    Check(detailRows.Count == 1543, "export all imported records");
    Check(detailRows.All(row => Convert.ToInt32(row["资源ID"]) > 0 && Convert.ToInt32(row["卡池类型"]) > 0), "export resource and pool IDs");
    var formulaRows = MiniExcel.Query(exportPath, useHeaderRow: true, sheetName: (string)indexRows[1]["工作表"])
        .Cast<object>().Cast<IDictionary<string, object>>().ToList();
    Check((string)formulaRows.Single()["名称"] == "=1+1", "formula-looking names remain text");
    using (var exportZip = ZipFile.OpenRead(exportPath))
    {
        foreach (var entry in exportZip.Entries.Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) && entry.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var stream = entry.Open();
            Check(!XDocument.Load(stream).Descendants().Any(e => e.Name.LocalName == "f"), "no injected formulas");
        }
    }
    var originalBytes = File.ReadAllBytes(exportPath);
    try
    {
        WavesRecordExcelExporter.Write(exportPath, []);
        throw new Exception("Empty export accepted");
    }
    catch (InvalidOperationException) { }
    try
    {
        WavesRecordExcelExporter.Write(exportPath, [imported], new CancellationToken(true));
        throw new Exception("Canceled export accepted");
    }
    catch (OperationCanceledException) { }
    Check(originalBytes.SequenceEqual(File.ReadAllBytes(exportPath)), "empty/canceled export preserves existing file");
    Console.WriteLine("PASS: Excel export, 4 archives/1544 records, duplicate/invalid/empty names, empty archives, text IDs, formula literals, canceled export");

    var badHeader = Path.Combine(fixtureDirectory, "bad-header.xlsx");
    File.Copy(path, badHeader);
    Mutate(badHeader, "xl/sharedStrings.xml", document =>
    {
        var column = document.Descendants().First(e => e.Name.LocalName == "t" && e.Value == "资源ID");
        column.Value = "缺失资源列";
    });
    ExpectInvalid(badHeader, "缺少列");

    var badId = Path.Combine(fixtureDirectory, "bad-id.xlsx");
    File.Copy(path, badId);
    Mutate(badId, "xl/worksheets/sheet1.xml", document =>
    {
        var cell = document.Descendants().First(e => e.Name.LocalName == "c" && (string?)e.Attribute("r") == "F2");
        cell.Elements().First(e => e.Name.LocalName == "v").Value = "-1";
    });
    ExpectInvalid(badId, "第 2 行");

    var unknownPool = Path.Combine(fixtureDirectory, "unknown-pool.xlsx");
    File.Copy(path, unknownPool);
    Mutate(unknownPool, "xl/workbook.xml", document =>
        document.Descendants().First(e => e.Name.LocalName == "sheet").SetAttributeValue("name", "未知卡池"));
    ExpectInvalid(unknownPool, "尚未支持卡池");
    try
    {
        WutheringWavesToolExcelReader.Read(path, new CancellationToken(true));
        throw new Exception("Cancellation not honored");
    }
    catch (OperationCanceledException) { }
    Console.WriteLine("PASS: missing header, invalid ID, unknown pool and cancellation");
}
finally
{
    foreach (var fixture in Directory.GetFiles(fixtureDirectory)) File.Delete(fixture);
    Directory.Delete(fixtureDirectory);
}

// 独立检查原始 XML 的数量，避免只拿预设数字验证。
using (var zip = ZipFile.OpenRead(path))
{
    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var rawCount = 0;
    for (var i = 1; i <= 9; i++)
    {
        using var stream = zip.GetEntry($"xl/worksheets/sheet{i}.xml")!.Open();
        rawCount += XDocument.Load(stream).Descendants(ns + "row").Count() - 1;
    }
    Check(rawCount == total, "raw XML record count");
}
Console.WriteLine($"PASS: {total} records; Chinese strings, numeric IDs, dates, empty sheets and raw XML counts");
Console.WriteLine($"DynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported}; DynamicCodeCompiled={RuntimeFeature.IsDynamicCodeCompiled}");
if (!RuntimeFeature.IsDynamicCodeSupported)
    Console.WriteLine("PASS: Native AOT execution");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}

static void ExpectInvalid(string filePath, string message)
{
    try
    {
        WutheringWavesToolExcelReader.Read(filePath);
        throw new Exception("Malformed workbook accepted");
    }
    catch (InvalidDataException ex)
    {
        Check(ex.Message.Contains(message, StringComparison.Ordinal), ex.Message);
    }
}

static void Mutate(string path, string entryName, Action<XDocument> change)
{
    using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
    var entry = zip.GetEntry(entryName)!;
    XDocument document;
    using (var input = entry.Open()) document = XDocument.Load(input);
    change(document);
    entry.Delete();
    using var output = zip.CreateEntry(entryName).Open();
    document.Save(output);
}
