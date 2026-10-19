namespace ScoringService;

/// <summary>本工具的展示评级，与角色伤害和游戏官方评级无关；按显示的一位小数判档。</summary>
public static class ScoreRatings
{
    public static string Grade(decimal score) => Rounded(score) switch
    { >= 95 => "SSS", >= 85 => "SS", >= 75 => "S", >= 60 => "A", >= 40 => "B", _ => "C" };
    public static string Comment(decimal score) => Grade(score) switch
    {
        "SSS" => "词条出众，值得珍藏", "SS" => "优秀搭配，值得保留", "S" => "表现亮眼，可放心培养",
        "A" => "搭配合适，仍有提升空间", "B" => "可作过渡，优先优化副词条", _ => "适配较低，建议调整词条"
    };
    public static void Validate(global::ScoringService.Models.ScoreGradeThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (!(100 >= thresholds.SSS && thresholds.SSS > thresholds.SS && thresholds.SS > thresholds.S &&
            thresholds.S > thresholds.A && thresholds.A > thresholds.B && thresholds.B >= 0) || thresholds.Graduation is < 0 or > 100)
            throw new ArgumentException("评级门槛需满足100≥SSS>SS>S>A>B≥0，毕业线为0～100。");
    }
    public static string Grade(decimal score, global::ScoringService.Models.ScoreGradeThresholds thresholds)
    {
        Validate(thresholds);
        var value = Rounded(score);
        return value >= thresholds.SSS ? "SSS" : value >= thresholds.SS ? "SS" : value >= thresholds.S ? "S" :
            value >= thresholds.A ? "A" : value >= thresholds.B ? "B" : "C";
    }
    public static string Comment(decimal score, global::ScoringService.Models.ScoreGradeThresholds thresholds) =>
        score >= thresholds.Graduation ? "声骸已毕业，仍可继续优化" : "声骸尚有提升空间";
    private static decimal Rounded(decimal score)
    {
        if (score is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(score));
        return decimal.Round(score, 1, MidpointRounding.AwayFromZero);
    }
}
