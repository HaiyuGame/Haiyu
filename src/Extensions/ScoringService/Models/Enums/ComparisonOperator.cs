namespace ScoringService.Models.Enums;

/// <summary>角色实际面板值与养成目标值的比较方式。</summary>
public enum ComparisonOperator
{
    /// <summary>实际值严格大于目标值。</summary>
    GreaterThan,
    /// <summary>实际值大于或等于目标值。</summary>
    GreaterThanOrEqual,
    /// <summary>实际值严格小于目标值。</summary>
    LessThan,
    /// <summary>实际值小于或等于目标值。</summary>
    LessThanOrEqual,
    /// <summary>实际值等于目标值。</summary>
    Equal
}
