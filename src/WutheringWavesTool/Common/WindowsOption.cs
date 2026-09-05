namespace Haiyu.Common;

public sealed record WindowsOption
{
    public double? Width { get; init; }
    public double? Height { get; init; }
    public double? MinWidth { get; init; }
    public double? MinHeight { get; init; }
    public double? MaxWidth { get; init; }
    public double? MaxHeight { get; init; }
    public bool? IsResizable { get; init; }
    public bool? IsMaximizable { get; init; }
    public bool? IsMinimizable { get; init; }
    public bool CenterOnScreen { get; init; }

    public static WindowsOption DefaultWindowsOption =>
        new()
        {
            Width = 1150,
            Height = 650,
            IsResizable = false,
            IsMaximizable = false,
            CenterOnScreen = true,
        };

    public static WindowsOption OOBEWindowOption =>
        new()
        {
            Width = 800,
            Height = 500,
            IsResizable = false,
            IsMaximizable = false
        };

}
