
using System;
using System.Collections.Generic;
using System.Text;
using Waves.Api.Models.Rpc;
using Waves.Api.Models.Rpc.Launcher;

namespace Haiyu.Services;

public partial class RpcMethodService
{
    public Task<string> GetRpcVersionAsync(string key, List<RpcParams>? _param = null)
    {
        VerifyToken(_param);
        AppInfo info = new AppInfo();
        info.RpcVersion = "1.0.0";
        info.AppVersion = App.AppVersion;
        info.FrameworkVersion = RuntimeInformation.FrameworkDescription;
        info.SdkVersion = $"1.8.251106002";
        info.WebVersion = CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "未安装";
        return Task.FromResult(JsonSerializer.Serialize(info,RpcContext.Default.AppInfo));
    }

    public Task<string> GetRpcMethodsAsync(string key, List<RpcParams>? _param = null)
    {
        var list =  Enum.GetNames(typeof(RpcMethodKey)).ToList();
        return Task.FromResult(JsonSerializer.Serialize(list, RpcContext.Default.ListString));
    }
}
