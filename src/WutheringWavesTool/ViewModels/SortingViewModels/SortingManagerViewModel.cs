using Haiyu.Common.Contracts;
using Haiyu.Contracts.Services;
using Haiyu.Model;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Waves.Core.Services;

namespace Haiyu.ViewModels.SortingViewModels;

/// <summary>角色读取、模板选择与评分结果，不保存账号凭据或自动写入内置模板。</summary>
public sealed partial class SortingManagerViewModel : ObservableObject, IDisposable
{
    private readonly SortingWorkspace workspace;
    private readonly SortingViewFactory factory;
    private readonly KuroSortingService bridge;
    private readonly WindowSession session;
    private readonly CancellationTokenSource lifetime = new();
    private IReadOnlyList<SortingEcho> echoes = [];
    private CharacterPanel panel = new();
    private string portraitOverride = "";
    private bool disposed;
    public ObservableCollection<SortingRoleOption> Roles { get; } = [];
    public ObservableCollection<SortingTemplateCard> Templates { get; } = [];
    public ObservableCollection<SortingEchoCard> Results { get; } = [];
    public ObservableCollection<SortingTargetViewModel> Targets { get; } = [];
    public ObservableCollection<SortingStatOption> Catalog { get; } =
        new(EchoStatCatalog.All.Select(x => new SortingStatOption(x)));

    [ObservableProperty]
    public partial SortingRoleOption? SelectedRole { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = "选择角色，开始评分";

    [ObservableProperty]
    public partial string Average { get; set; } = "—";

    [ObservableProperty]
    public partial string CountText { get; set; } = "暂无声骸";

    [ObservableProperty]
    public partial SortingTemplateCard? SelectedTemplate { get; set; }

    [ObservableProperty]
    public partial string Grade { get; set; } = "—";

    [ObservableProperty]
    public partial string RatingComment { get; set; } = "";

    [ObservableProperty]
    public partial string HitSummary { get; set; } = "";
    public string RoleTitle => SelectedRole?.RoleName ?? "选择角色";
    public string RoleSubtitle =>
        SelectedRole is null ? "" : $"{SelectedRole.AttributeName} · Lv.{SelectedRole.Level}";
    public string RolePortrait =>
        portraitOverride.Length > 0 ? portraitOverride : SelectedRole?.PortraitUrl ?? "";
    public string RoleIcon => SelectedRole?.IconUrl ?? "";
    public bool CanInteract => !IsBusy;
    public bool HasResults => Results.Count > 0;
    public bool HasTargets => Targets.Count > 0;

    public SortingManagerViewModel(
        SortingWorkspace workspace,
        SortingViewFactory factory,
        KuroSortingService bridge,
        WindowSession session
    )
    {
        this.workspace = workspace;
        this.factory = factory;
        this.bridge = bridge;
        this.session = session;
        factory.TemplatesChanged += TemplatesChanged;
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanInteract));

    partial void OnSelectedRoleChanged(SortingRoleOption? value)
    {
        portraitOverride = "";
        panel = new();
        OnPropertyChanged(nameof(RoleTitle));
        OnPropertyChanged(nameof(RoleSubtitle));
        OnPropertyChanged(nameof(RolePortrait));
        OnPropertyChanged(nameof(RoleIcon));
        echoes = [];
        Results.Clear();
        Targets.Clear();
        Average = "—";
        CountText = "暂无声骸";
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasTargets));
        if (!disposed && !IsBusy)
            _ = LoadTemplatesAndScoreAsync();
    }

    private async void TemplatesChanged(object? sender, EventArgs e)
    {
        if (!disposed && !IsBusy)
            await LoadTemplatesAndScoreAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsBusy || disposed)
            return;
        IsBusy = true;
        Results.Clear();
        echoes = [];
        Average = "—";
        OnPropertyChanged(nameof(HasResults));
        try
        {
            var previous = SelectedRole?.RoleId;
            var roles = await bridge
                .GetRolesAsync(lifetime.Token)
                .WaitAsync(TimeSpan.FromSeconds(45), lifetime.Token);
            if (disposed)
                return;
            Roles.Clear();
            foreach (var role in roles)
                Roles.Add(new(role));
            SelectedRole =
                Roles.FirstOrDefault(x => x.RoleId == previous)
                ?? Roles.FirstOrDefault(x =>
                    CharacterScorePresets.All.Any(p => p.RoleId == x.RoleId.ToString())
                )
                ?? Roles.FirstOrDefault();
            Message = Roles.Count > 0 ? "已读取角色" : "暂无角色";
        }
        catch (Exception ex)
        {
            if (!disposed)
                Message = Error(ex);
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
        if (!disposed && SelectedRole is not null)
            await LoadTemplatesAndScoreAsync();
    }

    private async Task LoadTemplatesAndScoreAsync()
    {
        if (SelectedRole is null || IsBusy || disposed)
            return;
        IsBusy = true;
        try
        {
            var roleId = SelectedRole.RoleId.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
            var saved = await workspace.Store.GetGroupTemplateAsync();
            if (disposed)
                return;
            var previous = SelectedTemplate?.Model.Id;
            Templates.Clear();
            foreach (var template in saved.Where(x => x.RoleId == roleId && x.ScoringVersion == 2))
                Templates.Add(new(template));
            Templates.Add(new(CharacterScorePresets.ForRole(roleId, SelectedRole.RoleName)));
            Select(Templates.FirstOrDefault(x => x.Model.Id == previous) ?? Templates[0]);
        }
        catch (Exception ex)
        {
            if (!disposed)
                Message = Error(ex);
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
        if (!disposed)
            await ScoreAsync();
    }

    public void Select(SortingTemplateCard? card)
    {
        if (card is null)
            return;
        foreach (var item in Templates)
            item.IsSelect = item == card;
        SelectedTemplate = card;
        Targets.Clear();
        foreach (var target in card.Model.Items.Where(x => x.Usage == EntryUsage.DevelopmentTarget))
            Targets.Add(new(SortingText.Copy(target)));
        OnPropertyChanged(nameof(HasTargets));
        Results.Clear();
        Average = "—";
        Grade = "—";
        RatingComment = "";
        HitSummary = "";
        OnPropertyChanged(nameof(HasResults));
    }

    [RelayCommand]
    public async Task ScoreAsync()
    {
        if (IsBusy || disposed || SelectedRole is null || SelectedTemplate is null)
            return;
        IsBusy = true;
        Results.Clear();
        Average = "—";
        OnPropertyChanged(nameof(HasResults));
        Grade = "—";
        RatingComment = "";
        HitSummary = "";
        foreach (var target in Targets)
            target.Actual = double.NaN;
        try
        {
            var group = SelectedTemplate.Model;
            var snapshot = await bridge
                .GetSnapshotAsync(SelectedRole.RoleId, group, lifetime.Token)
                .WaitAsync(TimeSpan.FromSeconds(45), lifetime.Token);
            if (disposed)
                return;
            echoes = snapshot.Echoes;
            panel = snapshot.Panel;
            portraitOverride = snapshot.PortraitUrl;
            OnPropertyChanged(nameof(RolePortrait));
            foreach (var target in Targets)
            {
                var actual = panel.Stats.FirstOrDefault(x =>
                    x.EntryNo == target.Model.EntryNo && x.Type == target.Model.Type
                );
                target.Actual = actual is null ? double.NaN : (double)actual.Value;
            }
            var cards = new List<SortingEchoCard>();
            var unsupported = echoes.Count(x => x.Data.Rarity != 5);
            foreach (var echo in echoes.Where(x => x.Data.Rarity == 5))
            {
                var graduation = workspace.Calculator.ScoreGraduation(group, echo.Data);
                cards.Add(
                    new(
                        echo.Name,
                        echo.Data,
                        graduation.Score,
                        echo.IconUrl, group.GradeThresholds, graduation.TheoreticalScore, graduation.Progress
                    )
                );
            }
            foreach (var card in cards)
                Results.Add(card);
            var mean = cards.Count > 0 ? cards.Average(x => x.RawScore) : 0;
            Average =
                cards.Count > 0
                    ? decimal.Round(mean, 1, MidpointRounding.AwayFromZero).ToString("F1")
                    : "—";
            Grade = cards.Count > 0 ? ScoreRatings.Grade(mean, group.GradeThresholds) : "—";
            RatingComment = cards.Count > 0 ? ScoreRatings.Comment(mean, group.GradeThresholds) : "";
            HitSummary =
                cards.Count > 0
                    ? $"有效副词条 {cards.Sum(x => x.EffectiveCount)}/{cards.Sum(x => x.SubStatCount)}"
                    : "";
            CountText = $"{cards.Count} 件声骸";
            Message =
                unsupported > 0 ? $"已评分，另有 {unsupported} 件非五星声骸"
                : cards.Count > 0 ? "已更新"
                : "当前角色暂无已装备声骸";
            OnPropertyChanged(nameof(HasResults));
        }
        catch (Exception ex)
        {
            if (!disposed)
            {
                echoes = [];
                Message = Error(ex);
            }
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedTemplate is not null)
            factory.ShowEditor(new(SortingText.Copy(SelectedTemplate.Model), echoes));
    }

    [RelayCommand]
    private async Task DeleteTemplateAsync()
    {
        if (SelectedTemplate is null || IsBusy || disposed)
            return;
        var group = SelectedTemplate.Model;
        if (group.Id.StartsWith("builtin-", StringComparison.Ordinal))
        {
            Message = "内置参考模板保留；自定义模板可删除";
            return;
        }
        IsBusy = true;
        var deleted = false;
        try
        {
            if (!await factory.ConfirmDeleteAsync(session, group.Name) || disposed)
                return;
            await workspace.Store.DeleteGroupAsync(group.Id);
            deleted = true;
            Message = "模板已删除";
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
        if (deleted && !disposed)
            factory.NotifyTemplatesChanged();
    }

    [RelayCommand]
    private void Duplicate()
    {
        if (SelectedTemplate is null)
            return;
        var group = SortingText.Copy(SelectedTemplate.Model);
        group.Id = Guid.NewGuid().ToString("N");
        group.Name += " · 副本";
        factory.ShowEditor(new(group, echoes));
    }

    [RelayCommand]
    private void Copy()
    {
        if (!HasResults)
        {
            Message = "请先读取声骸";
            return;
        }
        SetClipboard(
            $"{SelectedRole?.RoleName} · {SelectedTemplate?.Name} · 静态适配均分 {Average}/100 · {Grade} · {HitSummary}\n{RatingComment}\n"
                + string.Join("\n\n", Results.Select(x => x.CopyText))
        );
    }

    [RelayCommand]
    private void CopyTemplate()
    {
        if (SelectedTemplate is not null)
            SetClipboard(
                JsonSerializer.Serialize(
                    SelectedTemplate.Model,
                    ScoringJsonContext.Default.EntryGroup
                )
            );
    }

    private void SetClipboard(string text)
    {
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Message = "已复制";
        }
        catch
        {
            Message = "复制失败，请重试";
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            var file = await session.Context.PickersService.GetFileOpenPicker([".json"]);
            if (file is null || disposed)
                return;
            if (new FileInfo(file.Path).Length > 1024 * 1024)
                throw new ArgumentException("模板文件过大");
            var group =
                JsonSerializer.Deserialize(
                    await File.ReadAllTextAsync(file.Path, lifetime.Token),
                    ScoringJsonContext.Default.EntryGroup
                ) ?? throw new ArgumentException("模板为空");
            if (group.ScoringVersion != 2)
                throw new ArgumentException("请导入新版折算模板");
            ScoringEntryService.ValidateGroup(group);
            group.Id = Guid.NewGuid().ToString("N");
            await workspace.Store.SaveGroupAsync(group);
            Message = "已导入";
        }
        catch (Exception ex)
        {
            if (!disposed)
                Message = Error(ex);
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
        if (!disposed)
            await LoadTemplatesAndScoreAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (SelectedTemplate is null || IsBusy)
            return;
        IsBusy = true;
        try
        {
            var group = SortingText.Copy(SelectedTemplate.Model);
            var file = await session.Context.PickersService.GetFileSavePicker(
                [".json"],
                "声骸评分模板.json"
            );
            if (file is null || disposed)
                return;
            await File.WriteAllTextAsync(
                file.Path,
                JsonSerializer.Serialize(group, ScoringJsonContext.Default.EntryGroup),
                lifetime.Token
            );
            Message = "已导出";
        }
        catch (Exception ex)
        {
            if (!disposed)
                Message = Error(ex);
        }
        finally
        {
            if (!disposed)
                IsBusy = false;
        }
    }

    private static string Error(Exception ex) =>
        ex switch
        {
            OperationCanceledException => "读取已取消",
            TimeoutException => "读取超时，请重试",
            ArgumentException or InvalidOperationException or FormatException => ex.Message,
            JsonException => "模板格式错误",
            _ => "操作失败，请检查网络或登录状态",
        };

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        factory.TemplatesChanged -= TemplatesChanged;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
