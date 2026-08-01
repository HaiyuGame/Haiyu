
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Waves.Api.Models.Rpc;
using Waves.Api.Models.Rpc.Launcher;

namespace Haiyu.ServiceHost.Services;

public partial class RpcMethodService
{
    public Task<string> GetRpcVersionAsync(string key, List<RpcParams>? _param = null)
    {
        VerifyToken(_param);
        AppInfo info = new AppInfo();
        info.RpcVersion = "1.0.0";
        info.AppVersion = "1.3.5";
        info.FrameworkVersion = RuntimeInformation.FrameworkDescription;
        info.SdkVersion = $"1.8.251106002";
        info.WebVersion = "";
        return Task.FromResult(JsonSerializer.Serialize(info,RpcContext.Default.AppInfo));
    }

    public Task<string> GetRpcMethodsAsync(string key, List<RpcParams>? _param = null)
    {
        var list =  Enum.GetNames(typeof(RpcMethodKey)).ToList();
        return Task.FromResult(JsonSerializer.Serialize(list, RpcContext.Default.ListString));
    }
}
