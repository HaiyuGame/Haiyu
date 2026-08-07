namespace Haiyu.KuroClient;

partial class KuroClient
{
    public async Task<GamerBassData?> GetGamerBassDataAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
            { "channelId", "19" },
            { "countryCode", "1" },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/aki/roleBox/akiBox/baseData",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        for (int i = 0; i < 5; i++)
        {
            var result = await HttpClient.SendAsync(request, token);
            var jsonStr = await result.Content.ReadAsStringAsync(token);

            var resultCode = JsonSerializer.Deserialize(
                jsonStr,
                CommunityContext.Default.GamerBassString
            );
            if (resultCode == null || resultCode.Code != 200)
            {
                continue;
            }

            var bassData = JsonSerializer.Deserialize(
                resultCode.Data,
                CommunityContext.Default.GamerBassData
            );
            return bassData;
        }
        return null;
    }

    public async Task<GamerRoleData?> GetGamerRoleDataAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
            { "channelId", "19" },
            { "countryCode", "1" },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/aki/roleBox/akiBox/roleData",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200)
        {
            return null;
        }

        var jsonData = resultCode.Data;
        return JsonSerializer.Deserialize(jsonData, CommunityContext.Default.GamerRoleData);
    }

    public async Task<GamerCalabashData?> GetGamerCalabashDataAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
            { "channelId", "19" },
            { "countryCode", "1" },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/aki/roleBox/akiBox/calabashData",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200)
        {
            return null;
        }

        var jsonData = resultCode.Data;

        return JsonSerializer.Deserialize(jsonData, CommunityContext.Default.GamerCalabashData);
    }

    public async Task<GamerTowerModel?> GetGamerTowerIndexDataAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/aki/roleBox/akiBox/towerDataDetail",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200)
        {
            return null;
        }

        var jsonData = resultCode.Data;
        return JsonSerializer.Deserialize(jsonData, CommunityContext.Default.GamerTowerModel);
    }

    public async Task<GamerExploreIndexData?> GetGamerExploreIndexDataAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/aki/roleBox/akiBox/exploreIndex",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200)
        {
            return null;
        }

        var jsonData = resultCode.Data;

        return JsonSerializer.Deserialize(jsonData, CommunityContext.Default.GamerExploreIndexData);
    }

    public async Task<GamerChallengeIndexData?> GetGamerChallengeIndexDataAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/aki/roleBox/akiBox/challengeIndex",
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content,
            true
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200)
        {
            return null;
        }

        var jsonData = resultCode.Data;
        return JsonSerializer.Deserialize(
            jsonData,
            CommunityContext.Default.GamerChallengeIndexData
        );
    }

    public async Task<GamerDataBool?> RefreshGamerDataAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/aki/roleBox/akiBox/refreshData",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);

        return JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.GamerDataBool);
    }

    public async Task<GamerRoilDetily?> GetGamerRoilDetily(
        KuroAccount account,
        GameRoilDataItem roil,
        long roleId,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId },
            { "serverId", roil.ServerId },
            { "channelId", "19" },
            { "countryCode", "1" },
            { "id", $"{roleId}" },
        };
        var request = await BuildLoginRequest(
            "https://api.kurobbs.com/aki/roleBox/akiBox/getRoleDetail",
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200) { }

        var jsonData = resultCode.Data;

        return JsonSerializer.Deserialize(jsonData, CommunityContext.Default.GamerRoilDetily);
    }

    public async Task<GamerChallengeDetily?> GetGamerChallengeDetails(
        KuroAccount account,
        GameRoilDataItem roil,
        int countryCode,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId.ToString() },
            { "serverId", roil.ServerId.ToString() },
            { "channelId", "19" },
            { "countryCode", countryCode.ToString() },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/aki/roleBox/akiBox/challengeDetails",
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200)
        {
            return null;
        }

        var jsonData = resultCode.Data;
        var result2 = JsonSerializer.Deserialize(
            jsonData,
            CommunityContext.Default.GamerChallengeDetily
        );
        return result2;
    }

    public async Task<GamerSkin?> GetGamerSkinAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        var session = await UpdateRefreshToken(account, roil, token);
        var header = GetWebHeader(account, session?.AccessToken);
        var content = new Dictionary<string, string>()
        {
            { "gameId", roil.GameId.ToString() },
            { "roleId", roil.RoleId.ToString() },
            { "serverId", roil.ServerId.ToString() },
        };
        var request = await BuildRequestAsync(
            "https://api.kurobbs.com/aki/roleBox/akiBox/skinData",
            HttpMethod.Post,
            header,
            new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
            content
        );
        var result = await this.HttpClient.SendAsync(request, token);
        var jsonStr = await result.Content.ReadAsStringAsync(token);
        var resultCode = JsonSerializer.Deserialize(
            jsonStr,
            CommunityContext.Default.GamerBassString
        );
        if (resultCode == null || resultCode.Code != 200)
        {
            return null;
        }

        var jsonData = resultCode.Data;
        return JsonSerializer.Deserialize(jsonData, CommunityContext.Default.GamerSkin);
    }

    public async Task<GamerSlashDetailData?> GetGamerSlashDetailAsync(
        KuroAccount account,
        GameRoilDataItem roil,
        CancellationToken token = default
    )
    {
        try
        {
            var session = await UpdateRefreshToken(account, roil, token);
            var header = GetWebHeader(account, session?.AccessToken);
            var content = new Dictionary<string, string>()
            {
                { "gameId", roil.GameId.ToString() },
                { "roleId", roil.RoleId.ToString() },
                { "serverId", roil.ServerId.ToString() },
            };
            var request = await BuildRequestAsync(
                "https://api.kurobbs.com/aki/roleBox/akiBox/slashDetail",
                HttpMethod.Post,
                header,
                new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
                content
            );
            var result = await this.HttpClient.SendAsync(request, token);
            var jsonStr = await result.Content.ReadAsStringAsync(token);
            var resultCode = JsonSerializer.Deserialize(
                jsonStr,
                CommunityContext.Default.GamerBassString
            );
            if (resultCode == null || resultCode.Code != 200)
            {
                return null;
            }
            var jsonData = resultCode.Data;
            return JsonSerializer.Deserialize(
                jsonData,
                CommunityContext.Default.GamerSlashDetailData
            );
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<BriefHeader?> GetBriefHeaderAsync(
        KuroAccount account,
        CancellationToken token = default
    )
    {
        try
        {
            var header = GetWebHeader(account);
            var content = new Dictionary<string, string>() { };
            var request = await BuildRequestAsync(
                "https://api.kurobbs.com/aki/resource/period/list",
                HttpMethod.Get,
                header,
                new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
                content
            );
            var result = await this.HttpClient.SendAsync(request, token);
            var jsonStr = await result.Content.ReadAsStringAsync(token);

            return JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.BriefHeader);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ResourceBrefItem> GetVersionBrefItemAsync(
        KuroAccount account,
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    )
    {
        return await GetBrefItemAsync(
            account,
            "https://api.kurobbs.com/aki/resource/version",
            roleId,
            serverId,
            versionId,
            token
        );
    }

    public async Task<ResourceBrefItem> GetWeekBrefItemAsync(
        KuroAccount account,
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    )
    {
        return await GetBrefItemAsync(
            account,
            "https://api.kurobbs.com/aki/resource/week",
            roleId,
            serverId,
            versionId,
            token
        );
    }

    public async Task<ResourceBrefItem> GetMonthBrefItemAsync(
        KuroAccount account,
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    )
    {
        return await GetBrefItemAsync(
            account,
            "https://api.kurobbs.com/aki/resource/month",
            roleId,
            serverId,
            versionId,
            token
        );
    }

    private async Task<ResourceBrefItem> GetBrefItemAsync(
        KuroAccount account,
        string url,
        string roleId,
        string serverId,
        string versionId,
        CancellationToken token = default
    )
    {
        try
        {
            var header = GetWebHeader(account);
            var content = new Dictionary<string, string>()
            {
                { "period", versionId },
                { "roleId", roleId },
                { "serverId", serverId },
            };
            var request = await BuildRequestAsync(
                url,
                HttpMethod.Post,
                header,
                new MediaTypeHeaderValue("application/x-www-form-urlencoded"),
                content
            );
            var result = await this.HttpClient.SendAsync(request, token);
            var jsonStr = await result.Content.ReadAsStringAsync(token);

            return JsonSerializer.Deserialize(jsonStr, CommunityContext.Default.ResourceBrefItem);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
