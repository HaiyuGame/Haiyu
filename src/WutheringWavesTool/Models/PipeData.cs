namespace Haiyu.Models;

public class PieData : PieSeries
{
    public double[] Values
    {
        get => [Value];
        set => Value = value?.FirstOrDefault() ?? 0;
    }

    public double Offset
    {
        get => OuterRadiusOffset;
        set => OuterRadiusOffset = value;
    }
}
