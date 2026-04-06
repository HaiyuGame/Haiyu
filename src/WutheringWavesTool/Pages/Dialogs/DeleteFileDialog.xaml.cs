namespace Haiyu.Pages.Dialogs;

public sealed partial class DeleteFileDialog : ContentDialog,IDialog
{
    public DeleteFileDialog()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<DeleteFileViewModel>();
    }

    public DeleteFileViewModel ViewModel { get; set; }

    public void SetData(object data)
    {
        if(data is string contextName)
        {
            ViewModel.SetDeleteFileArgs(contextName);
        }
    }
}
