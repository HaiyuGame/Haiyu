namespace Waves.Core.Adaptives;

public class Int32Adaptive : IDataAdaptive<int, string>
{
    public string? GetBack(int forward)
    {
        return forward.ToString();
    }

    public int GetForward(string value)
    {
        if(int.TryParse(value, out var result))
        {
            return result;
        }
        return 0;
    }
}
