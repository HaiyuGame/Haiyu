namespace HaiyuServer.Models.Tables.Music;

[SqlSugar.SugarTable(TableName ="musicTable")]
public class MusicItemTable: TableBase
{

    public string GroupID { get; set;  }
}
