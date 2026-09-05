using Haiyu.Common.Contracts;

namespace Haiyu.Pages.Dialogs;

public sealed partial class DeleteFileDialog : ContentDialog,IDialog
{
    public DeleteFileDialog(
        DeleteFileViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
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
