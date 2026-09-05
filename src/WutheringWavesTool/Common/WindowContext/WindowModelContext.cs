namespace Haiyu.Common.WindowContext;

public class WindowModelContext:WindowContext
{
    public WindowModelContext(IServiceScope service, string key) : base(service, key)
    {
    }

    public nint OwnerId { get; set; }
}
