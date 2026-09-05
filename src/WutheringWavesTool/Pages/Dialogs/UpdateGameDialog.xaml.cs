using Haiyu.Common.Contracts;
using Haiyu.Models.Dialogs;
using Waves.Core.Models.Enums;

namespace Haiyu.Pages.Dialogs;

public sealed partial class UpdateGameDialog : ContentDialog,
            IResultDialog<UpdateGameResult>
{
    public UpdateGameDialog(
        UpdateGameViewModel viewModel,
        IThemeService themeService
    )
    {
        InitializeComponent();
        ViewModel = viewModel;
        RequestedTheme = themeService.CurrentTheme;
    }

    public UpdateGameViewModel ViewModel { get; }

    public UpdateGameResult? GetResult()
    {
        return ViewModel.GameResult();
    }

    public void SetData(object data)
    {
        if(data is Tuple<string, UpdateGameType> tuple)
        {
            if (Instance.Host.Services.GetRequiredKeyedService<IGameContextV2>(tuple.Item1) is IGameContextV2 context)
            {
                this.ViewModel.SetData(context,tuple.Item2);
            }
        }
       
    }
}
