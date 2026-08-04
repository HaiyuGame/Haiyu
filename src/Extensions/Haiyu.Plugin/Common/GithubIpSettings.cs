using System.Text.Json;
using ProxyExtensions.Models;
using Waves.Settings;

namespace Haiyu.Plugin.Common;

[Settings<List<IPEndPointWrapper>>(
    Name = "githubIps",
    DefaultValue = "[{\"host\":\"github.com\",\"ips\":[\"20.205.243.166\",\"140.82.112.3\",\"140.82.113.3\",\"140.82.114.3\",\"140.82.121.3\"]},{\"host\":\"api.github.com\",\"ips\":[\"20.205.243.168\",\"140.82.112.5\",\"140.82.113.5\",\"140.82.114.6\",\"140.82.121.5\"]},{\"host\":\"codeload.github.com\",\"ips\":[\"20.205.243.165\",\"140.82.112.9\",\"140.82.113.10\",\"140.82.114.10\",\"140.82.121.10\"]},{\"host\":\"avatars.githubusercontent.com\",\"ips\":[\"185.199.108.133\",\"185.199.109.133\",\"185.199.110.133\",\"185.199.111.133\"]},{\"host\":\"raw.githubusercontent.com\",\"ips\":[\"185.199.108.133\",\"185.199.109.133\",\"185.199.110.133\",\"185.199.111.133\"]},{\"host\":\"release-assets.githubusercontent.com\",\"ips\":[\"185.199.108.133\",\"185.199.109.133\",\"185.199.110.133\",\"185.199.111.133\"]},{\"host\":\"objects.githubusercontent.com\",\"ips\":[\"185.199.108.133\",\"185.199.109.133\",\"185.199.110.133\",\"185.199.111.133\"]},{\"host\":\"objects-origin.githubusercontent.com\",\"ips\":[\"140.82.112.21\"]},{\"host\":\"github.githubassets.com\",\"ips\":[\"185.199.108.215\",\"185.199.109.215\",\"185.199.110.215\",\"185.199.111.215\"]},{\"host\":\"github-releases.githubusercontent.com\",\"ips\":[\"185.199.108.154\",\"185.199.109.154\",\"185.199.110.154\",\"185.199.111.154\"]},{\"host\":\"github-registry-files.githubusercontent.com\",\"ips\":[\"185.199.108.154\",\"185.199.109.154\",\"185.199.110.154\",\"185.199.111.154\"]}]",
    Nullable = true,
    JsonTypeInfoContextType = typeof(IPEndPointWrapperContext),
    JsonTypeInfoPropertyName = nameof(IPEndPointWrapperContext.ListIPEndPointWrapper)
)]
[Settings<bool>(Name = "githubFrontingEnabled", DefaultValue = "true", Nullable = false)]
[Settings<bool>(Name = "githubCdnEnabled", DefaultValue = "true", Nullable = false)]
[Settings<string>(Name = "githubCdn", DefaultValue = "https://gh.llkk.cc/{downloadUrl}", Nullable = true)]
public partial class GithubIpSettings : SettingBase
{
    public const string GitHubUserContentHost = "*.githubusercontent.com";

    private static readonly HashSet<string> LegacyUserContentHosts =
    [
        "raw.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "objects.githubusercontent.com",
        "objects-origin.githubusercontent.com",
        "github-releases.githubusercontent.com",
        "github-registry-files.githubusercontent.com",
    ];

    private const string DefaultGithubIpJson =
        "[{\"host\":\"github.com\",\"ips\":[\"20.205.243.166\",\"140.82.112.3\",\"140.82.113.3\",\"140.82.114.3\",\"140.82.121.3\"]},{\"host\":\"api.github.com\",\"ips\":[\"20.205.243.168\",\"140.82.112.5\",\"140.82.113.5\",\"140.82.114.6\",\"140.82.121.5\"]},{\"host\":\"codeload.github.com\",\"ips\":[\"20.205.243.165\",\"140.82.112.9\",\"140.82.113.10\",\"140.82.114.10\",\"140.82.121.10\"]},{\"host\":\"*.githubusercontent.com\",\"ips\":[\"185.199.108.133\",\"185.199.109.133\",\"185.199.110.133\",\"185.199.111.133\",\"185.199.108.154\",\"185.199.109.154\",\"185.199.110.154\",\"185.199.111.154\",\"140.82.112.21\"]},{\"host\":\"avatars.githubusercontent.com\",\"ips\":[\"185.199.108.133\",\"185.199.109.133\",\"185.199.110.133\",\"185.199.111.133\"]},{\"host\":\"github.githubassets.com\",\"ips\":[\"185.199.108.215\",\"185.199.109.215\",\"185.199.110.215\",\"185.199.111.215\"]}]";
    private static readonly string SettingsFilePath = Path.Combine(
        AppSettings.BassFolder,
        "githubIp.json"
    );

    public GithubIpSettings()
        : base(SettingsFilePath) { }

    public static IReadOnlyList<IPEndPointWrapper> BuiltinSettings { get; } =
        JsonSerializer.Deserialize(
            DefaultGithubIpJson,
            IPEndPointWrapperContext.Default.ListIPEndPointWrapper
        ) ?? [];

    public async Task<List<IPEndPointWrapper>> GetMergedGithubIpsAsync()
    {
        var local = await GetgithubIpsAsync() ?? [];
        var merged = new Dictionary<string, IPEndPointWrapper>(StringComparer.OrdinalIgnoreCase);
        var legacyUserContentIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in BuiltinSettings)
        {
            merged[item.Host] = new IPEndPointWrapper() { Host = item.Host, Ips = [.. item.Ips] };
        }

        foreach (var item in local.Where(x => !string.IsNullOrWhiteSpace(x.Host)))
        {
            if (IsGitHubUserContentHost(item.Host))
            {
                foreach (var ip in item.Ips.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    legacyUserContentIps.Add(ip);
                }

                continue;
            }

            merged[item.Host] = new IPEndPointWrapper()
            {
                Host = item.Host,
                Ips = [.. item.Ips.Distinct(StringComparer.OrdinalIgnoreCase)],
            };
        }

        if (merged.TryGetValue(GitHubUserContentHost, out var userContent))
        {
            foreach (var ip in legacyUserContentIps)
            {
                if (!userContent.Ips.Contains(ip, StringComparer.OrdinalIgnoreCase))
                {
                    userContent.Ips.Add(ip);
                }
            }
        }
        else if (legacyUserContentIps.Count > 0)
        {
            merged[GitHubUserContentHost] = new IPEndPointWrapper()
            {
                Host = GitHubUserContentHost,
                Ips = [.. legacyUserContentIps],
            };
        }

        return [.. merged.Values];
    }

    private static bool IsGitHubUserContentHost(string host)
    {
        if (string.Equals(host, GitHubUserContentHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return LegacyUserContentHosts.Contains(host);
    }
}
