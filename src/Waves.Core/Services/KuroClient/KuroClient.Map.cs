using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Waves.Api.Models;
using Waves.Api.Models.Launcher;
using Waves.Api.Models.Maps;
using Waves.Core.Socket;

namespace Waves.Core.Services;

partial class KuroClient
{
    public HttpClient MapClient { get; private set; }

    public async Task InitMapPostion()
    {
        MapClient = new HttpClient()
        {
            BaseAddress = new Uri("https://api.kurobbs.com")
        };
        if (!(await MapPreCheckAsync()))
        {
            return;
        }
        var user = await GetKuroRoleBindingInfoAsync();
        WebSocketMapClient client = new WebSocketMapClient();
        await client.StartAsync(BuildUri());
    }

    public string BuildUri()
    {
        var builder = new UriBuilder("wss://api.kurobbs.com/ws-map");
        var query = $"devcode={this.AccountService.Current.TokenDid}&token={this.AccountService.Current.Token}&source=android";
        builder.Query = query;
        //wss://api.kurobbs.com/ws-map?devcode=v3gMmf9EnuSrdMCgZHrxauEWB2VZoyEj&token=eyJhbGciOiJIUzI1NiJ9.eyJjcmVhdGVkIjoxNzc4NDI1OTAwNjU5LCJ1c2VySWQiOjE3MzAxNDg0fQ.G0viXGZuvAKWLizzeDR15ocCYQO6ktEXj0TSsDH4hrE&source=android
        return builder.Uri.AbsoluteUri;
    }

    public async Task<bool> MapPreCheckAsync()
    {
        var request = BuildMapRequest(HttpMethod.Post, "/map/core/gamer/role/preCheck");
        var result = await MapClient.SendAsync(request);
        var data = await result.Content.ReadFromJsonAsync(MapJsonContext.Default.MapApiResponseBoolean);
        if(data != null) 
        {
            return data.Data;
        }
        return false;
    }

    public async Task<KuroRoleBindingInfoData?> GetKuroRoleBindingInfoAsync()
    {
        var request = BuildMapRequest(HttpMethod.Post, "/map/core/gamer/role/getBindRoleInfo", null, "");
        var result = await MapClient.SendAsync(request);
        var data = await result.Content.ReadFromJsonAsync(MapJsonContext.Default.MapApiResponseKuroRoleBindingInfoData);
        if(data != null) 
        {
            return data.Data;
        }
        return null;
    }

    public HttpRequestMessage BuildMapRequest(HttpMethod method,string url,string body = null,string @paramQuery = null)
    {
        var postData = new HttpRequestMessage(method,url);
        if(AccountService.Current != null)
        {
            postData.Headers.TryAddWithoutValidation("token", AccountService.Current.Token);
            postData.Headers.TryAddWithoutValidation("devcode", AccountService.Current.TokenDid);
            postData.Headers.TryAddWithoutValidation("source", "android");
            postData.Headers.TryAddWithoutValidation("wiki_type", "10");
            postData.Headers.TryAddWithoutValidation("Referer", "https://www.kurobbs.com/");
        }
        if (method == HttpMethod.Post)
        {
            postData.Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");

        }else if(method == HttpMethod.Get && !string.IsNullOrEmpty(@paramQuery))
        {
            postData.RequestUri = new Uri($"{url}?{@paramQuery}", UriKind.Relative);
        }
        return postData;
    }
}
