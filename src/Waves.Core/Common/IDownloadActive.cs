namespace Waves.Core.Common;

/// <summary>
/// 下载执行时的事件通知
/// </summary>
public interface IDownloadActive
{
    public void Init();

    public Task ActiveAsync(Task eventMethod);
}
