using Haiyu.Common.Contracts;

namespace Haiyu.ViewModel.WavesRecordManager;

public class WavesRecordManagerViewModel:WindowViewModelBase
{
    public WavesRecordManagerViewModel(WindowSession session)
    {
        this.WinSession = session;
    }

    public WindowSession WinSession { get; internal set; }
}
