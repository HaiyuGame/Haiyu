namespace Haiyu.Controls;

/// <summary>只展示分析结果，不负责加载记录或管理宿主生命周期。</summary>
public sealed partial class WavesRecordAnalysisControl : UserControl
{
    public WavesRecordAnalysisControl() => InitializeComponent();

    public WavesRecordAnalysisViewModel ViewModel
    {
        get => (WavesRecordAnalysisViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(WavesRecordAnalysisViewModel), typeof(WavesRecordAnalysisControl),
        new PropertyMetadata(null));
}
