using Haiyu.ServiceHost;

namespace Haiyu.Services.Contracts;

/// <summary>
/// Haiyu Rpc插件服务（Rpc Socket）
/// </summary>
public interface IPluginServices
{
    public RpcService RpcService { get; }

    public Task<PluginModel> GetPluginAsync();
}
