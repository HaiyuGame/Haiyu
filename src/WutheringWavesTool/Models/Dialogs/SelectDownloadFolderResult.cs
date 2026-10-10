using Waves.Core.Models.Options;
namespace Haiyu.Models.Dialogs;

public class SelectDownloadFolderResult
{
    public ContentDialogResult? Result { get; internal set; }

    public GameResourceParameter? Parameter { get; init; }
    public string InstallFolder { get; set; }
    public GameResourceSummary? Summary { get; internal set; }
}
