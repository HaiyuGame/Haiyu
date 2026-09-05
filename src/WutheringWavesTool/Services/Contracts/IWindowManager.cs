using Haiyu.Common.Contracts;
using Haiyu.Common.WindowContext;
using Haiyu.Models.Options;

namespace Haiyu.Services.Contracts;

public interface IWindowManager
{
    public static string ShellKey => "Shell";

    public Task<IEnumerable<WindowContext>> GetWindowContextsAsync();

    /// <summary>
    /// 主窗口
    /// </summary>
    public ShellWindowContext Shell { get;  }

    public AppSettings AppSettings { get; }

    /// <summary>
    /// 创建主窗口Shell
    /// </summary>
    public Task CreateShellWindowAsync();

    public Task CreateWindow<T>(WindowManagerOption managerOption)
        where T : IWindowPage;
    public WindowContext? GetWindowContext(string key);
}
