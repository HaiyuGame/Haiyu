using Haiyu.Models.Options;

namespace Haiyu.Services.Contracts;

public interface IWindowManager
{
    public Task CreateWindow<T>(WindowManagerOption managerOption)
        where T : IWindowPage;

}
