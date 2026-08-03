using FluentWPF.Controls;
using FluentWPF.Controls.SystemBackdrops;
using Haiyu.Publish.ViewModels;

namespace Haiyu.Publish;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        this.SystemBackdrop = new MicaBackdrop();
    }
}
