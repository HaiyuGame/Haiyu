using Haiyu.Common.Contracts;
using Haiyu.Contracts.Services;
using Haiyu.Model;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;

namespace Haiyu.ViewModels.SortingViewModels;

/// <summary>非模态模板编辑，所有改动仅作用于副本；保存后通知管理窗口。</summary>
public sealed partial class SortingTemplateViewModel(
    SortingWorkspace workspace,
    SortingViewFactory factory,
    WindowSession session
) : ObservableObject, IDisposable
{
    private EntryGroup template = new();
    private SortingEditorRequest? request;
    private bool disposed;
    public ObservableCollection<SortingEntryViewModel> Rules { get; } = [];
    public ObservableCollection<SortingEntryViewModel> VisibleRules { get; } = [];
    public ObservableCollection<SortingTargetViewModel> Targets { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Message { get; set; } = "";

    [ObservableProperty]
    public partial string Preview { get; set; } = "—";
    private double mainPercent = 30;
    public double MainPercent
    {
        get => mainPercent;
        set
        {
            if (!double.IsFinite(value))
                return;
            if (SetProperty(ref mainPercent, Math.Round(Math.Clamp(value, 0, 100))))
                UpdatePreview();
        }
    }

    [ObservableProperty] public partial double TargetCount { get; set; } = 4;
    partial void OnTargetCountChanged(double value) => UpdatePreview();
    [ObservableProperty] public partial double GradeSSS { get; set; } = 95;
    partial void OnGradeSSSChanged(double value) => UpdatePreview();
    [ObservableProperty] public partial double GradeSS { get; set; } = 85;
    partial void OnGradeSSChanged(double value) => UpdatePreview();
    [ObservableProperty] public partial double GradeS { get; set; } = 75;
    partial void OnGradeSChanged(double value) => UpdatePreview();
    [ObservableProperty] public partial double GradeA { get; set; } = 60;
    partial void OnGradeAChanged(double value) => UpdatePreview();
    [ObservableProperty] public partial double GradeB { get; set; } = 40;
    partial void OnGradeBChanged(double value) => UpdatePreview();
    [ObservableProperty] public partial double GraduationLine { get; set; } = 85;
    partial void OnGraduationLineChanged(double value) => UpdatePreview();
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial int RuleSectionIndex { get; set; }

    partial void OnRuleSectionIndexChanged(int value) => RefreshVisibleRules();

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value) => RefreshVisibleRules();

    public bool CanDeleteTemplate => !template.Id.StartsWith("builtin-", StringComparison.Ordinal);

    private void RefreshVisibleRules()
    {
        VisibleRules.Clear();
        var usage = RuleSectionIndex == 0 ? EntryUsage.SubStatScoring : EntryUsage.MainStatScoring;
        var cost = RuleSectionIndex switch
        {
            1 => 1,
            2 => 3,
            3 => 4,
            _ => 0,
        };
        foreach (
            var row in Rules.Where(x =>
                x.Model.Usage == usage
                && (
                    cost == 0
                    || EchoStatCatalog.MainMaximum(x.Model.EntryNo, x.Model.Type, cost) > 0
                )
                && (
                    string.IsNullOrWhiteSpace(SearchText)
                    || x.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
                )
            )
        )
            VisibleRules.Add(row);
    }

    public string SourceUrl => template.SourceUrl;
    public string Notes => template.Notes;
    public bool HasSource =>
        Uri.TryCreate(SourceUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https";

    public void Initialize(SortingEditorRequest parameter)
    {
        request = null;
        foreach (var row in Rules)
            row.PropertyChanged -= RuleChanged;
        Rules.Clear();
        Targets.Clear();
        template = SortingText.Copy(parameter.Template);
        Name = template.Name;
        TargetCount = template.TargetEffectiveStatCount;
        GradeSSS = (double)template.GradeThresholds.SSS; GradeSS = (double)template.GradeThresholds.SS;
        GradeS = (double)template.GradeThresholds.S; GradeA = (double)template.GradeThresholds.A;
        GradeB = (double)template.GradeThresholds.B; GraduationLine = (double)template.GradeThresholds.Graduation;
        MainPercent = (double)(template.MainStatWeight * 100);
        foreach (
            var rule in template
                .Items.Where(x => x.Usage != EntryUsage.DevelopmentTarget)
                .OrderBy(x => x.Usage)
                .ThenByDescending(x => x.Weight)
        )
        {
            var row = new SortingEntryViewModel(rule);
            row.PropertyChanged += RuleChanged;
            Rules.Add(row);
        }
        foreach (var target in template.Items.Where(x => x.Usage == EntryUsage.DevelopmentTarget))
            Targets.Add(new(target));
        request = parameter;
        OnPropertyChanged(nameof(CanDeleteTemplate));
        RefreshVisibleRules();
        OnPropertyChanged(nameof(SourceUrl));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(HasSource));
        UpdatePreview();
    }

    private void RuleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        UpdatePreview();

    private EntryGroup Build()
    {
        if (new[] { TargetCount, GradeSSS, GradeSS, GradeS, GradeA, GradeB, GraduationLine }.Any(x => !double.IsFinite(x) || x < 0 || x > 100) || TargetCount != Math.Truncate(TargetCount) || TargetCount < 1 || TargetCount > 5)
            throw new ArgumentException("请填写1～5的目标词条数和有效评级门槛");
        template.TargetEffectiveStatCount = (int)TargetCount;
        template.GradeThresholds = new() { SSS = (decimal)GradeSSS, SS = (decimal)GradeSS, S = (decimal)GradeS, A = (decimal)GradeA, B = (decimal)GradeB, Graduation = (decimal)GraduationLine };
        template.Name = Name.Trim();
        template.MainStatWeight = decimal.Round((decimal)MainPercent / 100, 2);
        template.SubStatWeight = 1 - template.MainStatWeight;
        template.Items = Rules.Select(x => x.Model).Concat(Targets.Select(x => x.Model)).ToList();
        return template;
    }

    private void UpdatePreview()
    {
        if (request is null || disposed)
            return;
        try
        {
            var group = Build();
            var echoes = request.Echoes.Where(x => x.Data.Rarity == 5).ToArray();
            Preview =
                echoes.Length == 0
                    ? "无装备预览"
                    : $"当前装备均分 {echoes.Average(x => workspace.Calculator.ScoreGraduation(group, x.Data).Score.TotalScore):F1}";
        }
        catch (ArgumentException ex)
        {
            Preview = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            var group = SortingText.Copy(Build());
            ScoringEntryService.ValidateGroup(group);
            // 内置参考只读，第一次保存自动成为用户模板，保留来源信息。
            if (group.Id.StartsWith("builtin-", StringComparison.Ordinal))
                group.Id = Guid.NewGuid().ToString("N");
            await workspace.Store.SaveGroupAsync(group, overwrite: true);
            if (disposed)
                return;
            template.Id = group.Id;
            OnPropertyChanged(nameof(CanDeleteTemplate));
            factory.NotifyTemplatesChanged();
            Message = "已保存";
        }
        catch (Exception ex)
        {
            if (!disposed)
                Message = ex is ArgumentException or InvalidOperationException
                    ? ex.Message
                    : "保存失败，请重试";
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddRuleAsync() => await ChangeRuleAsync(null);

    [RelayCommand]
    private async Task EditRuleAsync(SortingEntryViewModel row) => await ChangeRuleAsync(row);

    private async Task ChangeRuleAsync(SortingEntryViewModel? original)
    {
        if (IsBusy || disposed)
            return;
        IsBusy = true;
        try
        {
            var usage =
                original?.Model.Usage
                ?? (RuleSectionIndex == 0 ? EntryUsage.SubStatScoring : EntryUsage.MainStatScoring);
            var entry = await factory.ShowEntryAsync(
                session,
                new SortingRuleRequest(usage, original?.Model)
            );
            if (disposed || entry is null)
                return;
            if (
                Rules.Any(x =>
                    x != original
                    && x.Model.Usage == usage
                    && x.Model.EntryNo == entry.EntryNo
                    && x.Model.Type == entry.Type
                )
            )
            {
                Message = "该分类已有此词条，请直接编辑";
                return;
            }
            if (original is not null)
            {
                original.PropertyChanged -= RuleChanged;
                Rules.Remove(original);
            }
            var row = new SortingEntryViewModel(entry);
            row.PropertyChanged += RuleChanged;
            Rules.Add(row);
            SearchText = "";
            // 新增主词条若只在其他 COST 出现，切换到它所属分类。
            if (usage == EntryUsage.MainStatScoring)
            {
                var costs = new[] { 1, 3, 4 };
                if (
                    RuleSectionIndex < 1
                    || EchoStatCatalog.MainMaximum(
                        entry.EntryNo,
                        entry.Type,
                        costs[RuleSectionIndex - 1]
                    ) == 0
                )
                    RuleSectionIndex =
                        Array.FindIndex(
                            costs,
                            cost => EchoStatCatalog.MainMaximum(entry.EntryNo, entry.Type, cost) > 0
                        ) + 1;
            }
            RefreshVisibleRules();
            UpdatePreview();
            Message = "词条已更新，保存模板后生效";
        }
        catch
        {
            if (!disposed)
                Message = "打开词条设置失败，请重试";
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private void RemoveRule(SortingEntryViewModel row)
    {
        row.PropertyChanged -= RuleChanged;
        Rules.Remove(row);
        RefreshVisibleRules();
        UpdatePreview();
        Message = "词条已移除，保存模板后生效";
    }

    [RelayCommand]
    private void RestoreRules()
    {
        var group = Build();
        EchoStatCatalog.CompleteRules(group);
        foreach (
            var rule in group.Items.Where(x =>
                x.Usage != EntryUsage.DevelopmentTarget
                && !Rules.Any(row => ReferenceEquals(row.Model, x))
            )
        )
        {
            var row = new SortingEntryViewModel(rule);
            row.PropertyChanged += RuleChanged;
            Rules.Add(row);
        }
        RefreshVisibleRules();
        UpdatePreview();
        Message = "已补齐缺失词条，已有系数保持不变";
    }

    [RelayCommand]
    private async Task DeleteTemplateAsync()
    {
        if (!CanDeleteTemplate || IsBusy || disposed)
            return;
        IsBusy = true;
        try
        {
            if (!await factory.ConfirmDeleteAsync(session, template.Name) || disposed)
                return;
            await workspace.Store.DeleteGroupAsync(template.Id);
            factory.NotifyTemplatesChanged();
            session.Close();
        }
        catch
        {
            if (!disposed)
                Message = "删除失败，请重试";
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddTargetAsync()
    {
        try
        {
            var entry = await factory.ShowEntryAsync(session);
            if (disposed || entry is null)
                return;
            if (Targets.Any(x => x.Model.EntryNo == entry.EntryNo && x.Model.Type == entry.Type))
            {
                Message = "该目标已存在";
                return;
            }
            Targets.Add(new(entry));
        }
        catch
        {
            if (!disposed)
                Message = "打开目标设置失败，请重试";
        }
    }

    [RelayCommand]
    private void RemoveTarget(SortingTargetViewModel row) => Targets.Remove(row);

    [RelayCommand]
    private void Copy()
    {
        try
        {
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
            data.SetText(JsonSerializer.Serialize(Build(), ScoringJsonContext.Default.EntryGroup));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
            Message = "模板已复制";
        }
        catch
        {
            Message = "复制失败，请重试";
        }
    }

    [RelayCommand]
    private void Close() => session.Close();

    public void Dispose()
    {
        disposed = true;
        foreach (var row in Rules)
            row.PropertyChanged -= RuleChanged;
    }
}
