using Haiyu.Models.Dialogs;
using Haiyu.Models.Enums;

namespace Haiyu.Pages.Dialogs;

public sealed partial class UpdateGameDialog : ContentDialog,
            IResultDialog<UpdateGameResult>
{
    public UpdateGameDialog()
    {
        InitializeComponent();
        this.ViewModel = Instance.Host.Services.GetRequiredService<UpdateGameViewModel>();
        this.RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
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
            if (Instance.Host.Services.GetRequiredKeyedService<IGameContext>(tuple.Item1) is IGameContext context)
            {
                this.ViewModel.SetData(context,tuple.Item2);
            }
        }
       
    }
}
