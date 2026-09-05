namespace Haiyu.Common.WindowContext;

public class ShellWindowContext:WindowContext
{
    public ShellWindowContext(IServiceScope service,string key) : base(service, key)
    {
    }

    public Controls.TitleBar MainTitle { get; set; }


}
