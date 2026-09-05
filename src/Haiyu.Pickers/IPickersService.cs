namespace Haiyu.Pickers;

public interface IPickersService
{
    Task<PickFileResult?> GetFileOpenPicker(IReadOnlyCollection<string> extensions,nint value);

    Task<PickFileResult?> GetFileSavePicker(IReadOnlyCollection<string> extensions, string saveName, nint value);

    Task<PickFolderResult?> GetFolderPicker(nint value);
}
