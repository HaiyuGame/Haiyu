using SqlSugar;

namespace HaiyuServer.Models.Tables.Package;

/// <summary>
/// 游戏历史版本
/// </summary>
[SqlSugar.SugarTable(TableName ="appPackageTable")]
public class AppPackageTable: TableBase
{
    [SqlSugar.SugarColumn(ColumnName = "appId")]
    public long AppId { get; set; }

    [SugarColumn(ColumnName ="displayName")]
    public string DisplayName { get; set; }

}
