namespace HaiyuServer.Models.Tables.Music;

[SqlSugar.SugarTable(TableName ="musicGroupTable")]
public class MusicGroupTable: TableBase
{
    
    public string Name { get; set; }

    public string CoverPath { get; set; }

}
