using Haiyu.Common.Contracts;
using Haiyu.Models.Options;

namespace Haiyu.Services.Contracts;

public interface IWindowManager
{
    public Task<IEnumerable<WindowContext>> GetWindowContextsAsync();

    /// <summary>
    /// 主窗口
    /// </summary>
    public WindowContext Shell { get; set; }

    /// <summary>
    /// 创建主窗口Shell
    /// </summary>
    public void CreateShellWindow();

    public Task CreateWindow<T>(WindowManagerOption managerOption)
        where T : IWindowPage;

}
