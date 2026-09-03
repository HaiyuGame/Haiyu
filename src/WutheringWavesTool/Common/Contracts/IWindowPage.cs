namespace Haiyu.Common.Contracts;

public interface IWindowPage : IDisposable
{
    public void SetWindow(Window window);

    public void SetData(object value);
}
