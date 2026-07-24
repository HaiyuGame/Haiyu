namespace Haiyu.Pickers;

public interface IPickersService
{
    Task<PickFileResult?> GetFileOpenPicker(IReadOnlyCollection<string> extensions);

    Task<PickFileResult?> GetFileSavePicker(IReadOnlyCollection<string> extensions, string saveName);

    Task<PickFolderResult?> GetFolderPicker();
}
