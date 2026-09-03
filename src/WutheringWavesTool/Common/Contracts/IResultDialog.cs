namespace Haiyu.Common.Contracts;

public interface IResultDialog<T> : IDialog
{
    public T? GetResult();
}
