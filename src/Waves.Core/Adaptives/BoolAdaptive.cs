namespace Waves.Core.Adaptives
{
    public class BoolAdaptive : IDataAdaptive<bool, string?>
    {
        public static BoolAdaptive Instance { get; } = new();

        public string GetBack(bool forward)
        {
            return forward.ToString();
        }

        public bool GetForward(string? value)
        {
            return Convert.ToBoolean(value);
        }
    }
}
