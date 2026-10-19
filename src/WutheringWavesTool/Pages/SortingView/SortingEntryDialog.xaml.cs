using Haiyu.Common.Contracts;
using Haiyu.Model;
using ScoringService;
using ScoringService.Models.Enums;

namespace Haiyu.Pages.SortingView;

public sealed partial class SortingEntryDialog : ContentDialog, IDialog
{
    private readonly DialogSession session;
    public ObservableCollection<SortingStatOption> Catalog { get; } = new(EchoStatCatalog.All.Select(x => new SortingStatOption(x)));
    public SortingEntryDialog(DialogSession session)
    {
        this.session = session; InitializeComponent();
        RequestedTheme = Instance.Host.Services.GetRequiredService<IThemeService>().CurrentTheme;
        Entry.SelectedItem = Catalog.First(x => x.Id == "energyRegen");
    }
    private SortingRuleRequest? ruleRequest;
    public void SetData(object data)
    {
        if (data is not SortingRuleRequest request) return;
        ruleRequest = request;
        Title = request.Item is null ? "新增词条" : "编辑词条";
        PrimaryButtonText = request.Item is null ? "添加" : "保存";
        Comparison.Visibility = Target.Visibility = Visibility.Collapsed;
        RuleFields.Visibility = Visibility.Visible;
        Catalog.Clear();
        foreach (var stat in EchoStatCatalog.All.Where(x => x.Supports(request.Usage))) Catalog.Add(new(stat));
        Entry.SelectedItem = Catalog.FirstOrDefault(x => x.Definition.Id == request.Item?.EntryNo && x.Definition.Type == request.Item?.Type) ?? Catalog.FirstOrDefault();
        Weight.Value = (double)(request.Item?.Weight ?? 1);
        Reference.Value = request.Item?.NormalizationReference is decimal reference ? (double)reference : double.NaN;
        UpdateNormalization();
    }
    private void Entry_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateNormalization();
    private void UpdateNormalization()
    {
        if (Normalization is null || Entry.SelectedItem is not SortingStatOption option || ruleRequest is null) return;
        var rule = EchoStatCatalog.Rule(option.Definition, ruleRequest.Usage);
        Normalization.Text = new Haiyu.ViewModels.SortingViewModels.SortingEntryViewModel(rule).Limit;
    }
    private void Add_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (ruleRequest is not null)
        {
            if (Entry.SelectedItem is not SortingStatOption selected || !double.IsFinite(Weight.Value) || Weight.Value < 0 || Weight.Value > 1)
            { Error.Text = "请选择词条，系数填写 0～1"; args.Cancel = true; return; }
            if (!double.IsNaN(Reference.Value) && (!double.IsFinite(Reference.Value) || Reference.Value <= 0 || Reference.Value > 1000000000))
            { Error.Text = "评分基准填写正数，或留空使用默认值"; args.Cancel = true; return; }
            var edited = EchoStatCatalog.Rule(selected.Definition, ruleRequest.Usage, (decimal)Weight.Value);
            edited.NormalizationReference = double.IsNaN(Reference.Value) ? null : (decimal)Reference.Value;
            session.Close(edited);
            return;
        }
        if (Entry.SelectedItem is not SortingStatOption option || !double.IsFinite(Target.Value) || Target.Value < 0)
        { Error.Text = "请选择词条并填写目标值"; args.Cancel = true; return; }
        var rule = EchoStatCatalog.Rule(option.Definition, EntryUsage.DevelopmentTarget);
        rule.TargetValue = (decimal)Target.Value;
        rule.Comparison = Comparison.SelectedIndex switch
        { 1 => ComparisonOperator.GreaterThan, 2 => ComparisonOperator.LessThanOrEqual, 3 => ComparisonOperator.LessThan, 4 => ComparisonOperator.Equal, _ => ComparisonOperator.GreaterThanOrEqual };
        session.Close(rule);
    }
}
