using SqlSugar;

namespace HaiyuServer.Models.Tables;

[SqlSugar.SugarTable(TableName ="logTable")]
public class LogTable
{
    [SugarColumn(ColumnName ="id")]
    public string Id { get; set; }

    [SugarColumn(ColumnName ="methodName")]
    public string MethodName { get; set; }

    [SugarColumn(ColumnName ="message")]
    public string Message { get; set; }

    [SugarColumn(ColumnName ="level")]
    public int Level { get; set; }

    [SugarColumn(ColumnName ="timestamp")]
    public DateTime Timestamp { get; set; }
}
