using ScoringService.Models;
using Waves.Core.Services;

namespace Haiyu.Model;

/// <summary>非模态编辑窗口参数，携带当前装备用于即时预览。</summary>
public sealed record SortingEditorRequest(EntryGroup Template, IReadOnlyList<SortingEcho> Echoes);

/// <summary>词条编辑用途和可选的原规则；对话框返回独立副本。</summary>
public sealed record SortingRuleRequest(ScoringService.Models.Enums.EntryUsage Usage, EntryItem? Item = null);
