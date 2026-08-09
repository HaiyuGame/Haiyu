namespace Haiyu.KuroClient;

partial class KuroClient
{
    /// <summary>
    /// 库街区库洛币签到，1511,200为正常，其他则为失败
    /// </summary>
    /// <param name="account"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public async Task<KuroClientReturnCode<KuroClientSignInModel>?> SignInClientAsync(
        KuroAccount account,
        CancellationToken cts = default
    )
    {
        return await SendTaskRequestAsync(
            account,
            "https://api.kurobbs.com/user/signIn",
            new Dictionary<string, string>() { { "gameId", "2" }, { "geeTestData", "" } },
            KuroClientContext.Default.KuroClientReturnCodeKuroClientSignInModel,
            cts
        );
    }

    /// <summary>
    /// 查找帖子
    /// </summary>
    /// <param name="account"></param>
    /// <param name="option"></param>
    /// <param name="cts"></param>
    /// <returns></returns>
    public async Task<KuroClientReturnCode<KuroClientHomeFeedModel>?> FeedHomeListsAsync(
        KuroAccount account,
        HomeFeedOption option,
        CancellationToken cts = default
    )
    {
        return await SendTaskRequestAsync(
            account,
            "https://api.kurobbs.com/forum/list",
            option.ConvertParam(),
            KuroClientContext.Default.KuroClientReturnCodeKuroClientHomeFeedModel,
            cts
        );
    }

    /// <summary>
    /// 点赞帖子
    /// </summary>
    /// <param name="account"></param>
    /// <param name="option"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<KuroClientReturnCode<bool>?> PostIdLikeAsync(
        KuroAccount account,
        HomeFeedLikeOption option,
        CancellationToken token = default
    )
    {
        return await SendTaskRequestAsync(
            account,
            "https://api.kurobbs.com/forum/like",
            option.ConvertParam(),
            KuroClientContext.Default.KuroClientReturnCodeBoolean,
            token
        );
    }

    public async Task<KuroClientReturnCode<bool>?> SharedPostIdAsync(
        KuroAccount account,
        HomeFeedSharedOption option,
        CancellationToken token = default
    )
    {
        return await SendTaskRequestAsync(
            account,
            "https://api.kurobbs.com/encourage/level/shareTask",
            option.ConvertParam(),
            KuroClientContext.Default.KuroClientReturnCodeBoolean,
            token
        );
    }

    public async Task<KuroClientReturnCode<KuroClientPostPageDetail>?> GetFeedPageDetailAsync(
        KuroAccount account,
        HomeFeedPostDetailOption option,
        CancellationToken token = default
    )
    {
        return await SendTaskRequestAsync(
            account,
            "https://api.kurobbs.com/forum/getPostDetail",
            option.ConvertParam(),
            KuroClientContext.Default.KuroClientReturnCodeKuroClientPostPageDetail,
            token
        );
    }

    public async Task<KuroClientReturnCode<KuroEncourageProcessModel>?> GetEncourageProcessAsync(
        KuroAccount account,
        EncourageProcessOption option,
        CancellationToken token = default
    )
    {
        return await SendTaskRequestAsync(
            account,
            "https://api.kurobbs.com/encourage/level/getTaskProcess",
            option.ConvertParam(),
            KuroClientContext.Default.KuroClientReturnCodeKuroEncourageProcessModel,
            token
        );
    }

    private async Task<KuroClientReturnCode<T>?> SendTaskRequestAsync<T>(
        KuroAccount account,
        string url,
        Dictionary<string, string> content,
        JsonTypeInfo<KuroClientReturnCode<T>> jsonTypeInfo,
        CancellationToken token
    )
    {
        var header = GetDeviceHeader(account);
        var buildRequest = await this.BuildRequestAsync(
            url,
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content,
            true,
            token
        );
        var reponseBody = await HttpClient.SendAsync(buildRequest, token);
        var json = await reponseBody.Content.ReadAsStringAsync(token);
        var taskResult = JsonSerializer.Deserialize(json, jsonTypeInfo);
        if (taskResult == null || taskResult.Code != 200)
        {
            return null;
        }
        return taskResult;
    }

    public async  Task<KuroClientReturnCode<EncourageTotalGoldModel>?> GetEncourageTotalGoldAsync(
        KuroAccount account,
        CancellationToken token = default
    )
    {
        return await SendTaskRequestAsync(
            account,
            "https://api.kurobbs.com/encourage/gold/getTotalGold",
            [],
            KuroClientContext.Default.KuroClientReturnCodeEncourageTotalGoldModel,
            token
        );
    }
}
