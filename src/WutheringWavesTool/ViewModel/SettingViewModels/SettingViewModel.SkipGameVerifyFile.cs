namespace Haiyu.ViewModel;

partial class SettingViewModel
{

    [ObservableProperty]
    public partial ObservableCollection<SkipGameVerifyWrapper> SkipVerifyFiles { get; set; }

    [ObservableProperty]
    public partial bool? AutoSkipVerifyDelete { get; set; }

    [ObservableProperty]
    public partial string InputSkipVerifyPath { get; set; }

    async Task SaveVerifyFiles()
    {
        var list = this.SkipVerifyFiles.Select(x => x.FilePath).ToList();
        await AppSettings.SetskipVerifyFilesAsync(list, this.CTS.Token);
       
    }

    async partial void OnAutoSkipVerifyDeleteChanged(bool? oldValue, bool? newValue)
    {
        if (oldValue == null || newValue == null)
            return;
        await AppSettings.SetverifySkilDeleteAsync(newValue.Value, this.CTS.Token);
    }

    async private void SkipGameVerifyFileMethod(object recipient, SkipGameVerifyWrapper message)
    {
        this.SkipVerifyFiles.Remove(message);
        await SaveVerifyFiles();
    }

    [RelayCommand]
    async Task AddVerifyFile()
    {
        if (!File.Exists(InputSkipVerifyPath))
            return;
        this.SkipVerifyFiles.Add(new(InputSkipVerifyPath));
        await SaveVerifyFiles();
    }


}
