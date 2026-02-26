namespace HaiyuServer.Models;

/// <summary>
/// 数据表基类
/// </summary>
public partial class TableBase
{
    [SqlSugar.SugarColumn(ColumnName ="id",IsPrimaryKey =true)]
    public string Id { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "cTime")]
    public DateTime CreateTime { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "uTime")]
    public DateTime UpdateTime { get; set; }
}
