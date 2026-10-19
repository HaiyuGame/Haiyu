using ScoringService;
using ScoringService.Models;

namespace Haiyu.Services;

/// <summary>评分存储与模板入口；内置数据随程序提供，用户自定义保存在独立目录。</summary>
public sealed class SortingWorkspace
{
    public ScoringService.ScoringService Calculator { get; } = new();
    public ScoringEntryService Store { get; } = new(new SortingOption
    {
        GroupFolder = AppSettings.SortingGroupFolder, ItemFolder = AppSettings.SortingItemFolder
    });
}
