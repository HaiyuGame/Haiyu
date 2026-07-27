using Haiyu.Common;
using Haiyu.Plugin.Common;
using Haiyu.Plugin.Contracts;
using ProxyExtensions.Models;
using System.Net;

namespace Haiyu.ViewModel;

partial class SettingViewModel
{
    private bool _isLoadingGithubSettings;

    [ObservableProperty]
    public partial List<string> UpdateAppType { get; set; } = ["Github", "Mirror"];

    [ObservableProperty]
    public partial ObservableCollection<GithubIpDisplayGroup> GithubIpGroups { get; set; } = [];

    [ObservableProperty]
    public partial string? GithubCdn { get; set; }

    [ObservableProperty]
    public partial bool GithubFrontingEnabled { get; set; }

    [ObservableProperty]
    public partial bool GithubCdnEnabled { get; set; }

    [ObservableProperty]
    public partial bool HasGithubIpGroups { get; set; }

    [ObservableProperty]
    public partial string SelectUpdateAppType { get; set; }

    async partial void OnSelectUpdateAppTypeChanged(string value)
    {
        if (await AppSettings.GetUpdateTypeAsync() != value)
        {
            await AppSettings.SetUpdateTypeAsync(value);
        }
    }

    [ObservableProperty]
    public partial string? MirrorKey { get; set; }

    public async Task LoadUpdateAppType()
    {
        if (DesktopBridge.IsRunningAsMsix())
        {
            CheckUpdateVisibility = false;
            return;
        }

        CheckUpdateVisibility = true;
        var updateType = await AppSettings.GetUpdateTypeAsync();
        foreach (var item in UpdateAppType.Index())
        {
            if (updateType == item.Item)
            {
                SelectUpdateAppType = item.Item;
            }
        }

        if (SelectUpdateAppType == null)
        {
            SelectUpdateAppType = UpdateAppType[0];
        }

        MirrorKey = await AppSettings.GetMirrorKeyAsync();
        await LoadGithubIpConfigAsync();
    }

    private async Task LoadGithubIpConfigAsync()
    {
        _isLoadingGithubSettings = true;
        try
        {
            var settings = await GithubIpSettings.GetMergedGithubIpsAsync();

            GithubIpGroups = settings
                .Where(x => !string.IsNullOrWhiteSpace(x.Host))
                .Select(CreateGithubIpDisplayGroup)
                .ToObservableCollection();
            this.GithubCdn = await GithubIpSettings.GetgithubCdnAsync(this.CTS.Token);
            GithubFrontingEnabled = await GithubIpSettings.GetgithubFrontingEnabledAsync(this.CTS.Token);
            GithubCdnEnabled = await GithubIpSettings.GetgithubCdnEnabledAsync(this.CTS.Token);
            HasGithubIpGroups = GithubIpGroups.Count > 0;
        }
        finally
        {
            _isLoadingGithubSettings = false;
        }
    }

    async partial void OnGithubCdnEnabledChanged(bool oldValue, bool newValue)
    {
        if (_isLoadingGithubSettings || oldValue == newValue)
        {
            return;
        }

        await GithubIpSettings.SetgithubCdnEnabledAsync(newValue, this.CTS.Token);
    }

    async partial void OnGithubFrontingEnabledChanged(bool oldValue, bool newValue)
    {
        if (_isLoadingGithubSettings || oldValue == newValue)
        {
            return;
        }

        await GithubIpSettings.SetgithubFrontingEnabledAsync(newValue, this.CTS.Token);
    }

    private static GithubIpDisplayGroup CreateGithubIpDisplayGroup(IPEndPointWrapper setting)
    {
        return new GithubIpDisplayGroup
        {
            Host = setting.Host,
            IpEditorText = string.Join(
                ", ",
                setting.Ips.Where(x => !string.IsNullOrWhiteSpace(x))
            ),
        };
    }

    [RelayCommand]
    async Task SaveGithubIpConfig()
    {
        List<IPEndPointWrapper> settings = [];
        foreach (var group in GithubIpGroups.Where(x => !string.IsNullOrWhiteSpace(x.Host)))
        {
            var ips = group
                .IpEditorText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ips.Count == 0)
            {
                continue;
            }

            var invalidIp = ips.FirstOrDefault(ip => !IPAddress.TryParse(ip, out _));
            if (invalidIp != null)
            {
                TipShow.ShowMessage(LanguageService.FormatByText(LanguageService.GetStringByText("{0} 存在无效 IP：{1}"), group.Host, invalidIp), Symbol.Important);
                return;
            }

            settings.Add(
                new IPEndPointWrapper
                {
                    Host = group.Host.Trim(),
                    Ips = ips,
                }
            );
        }

        await GithubIpSettings.SetgithubIpsAsync(settings);
        await LoadGithubIpConfigAsync();
        TipShow.ShowMessage(LanguageService.GetStringByText("Github 域前置配置已保存"), Symbol.Accept);
    }

    [RelayCommand]
    async Task SaveeGithubCdn()
    {
        var cdn = GithubCdn?.Trim();
        if (string.IsNullOrWhiteSpace(cdn))
        {
            TipShow.ShowMessage(LanguageService.GetStringByText("Github CDN 配置不能为空"), Symbol.Important);
            return;
        }

        if (!cdn.Contains("{downloadUrl}", StringComparison.Ordinal))
        {
            TipShow.ShowMessage(LanguageService.GetStringByText("Github CDN 配置必须包含 {downloadUrl}"), Symbol.Important);
            return;
        }

        await GithubIpSettings.SetgithubCdnAsync(cdn, this.CTS.Token);
        GithubCdn = await GithubIpSettings.GetgithubCdnAsync(this.CTS.Token);
        TipShow.ShowMessage(LanguageService.GetStringByText("Github CDN 配置已保存"), Symbol.Accept);
    }

    [RelayCommand]
    async Task SaveGithubDownloadOptions()
    {
        await GithubIpSettings.SetgithubFrontingEnabledAsync(GithubFrontingEnabled, this.CTS.Token);
        await GithubIpSettings.SetgithubCdnEnabledAsync(GithubCdnEnabled, this.CTS.Token);
        TipShow.ShowMessage(LanguageService.GetStringByText("Github 下载选项已保存"), Symbol.Accept);
    }

    [RelayCommand]
    async Task UpdateVersion()
    {
        await AppContext.UpdateAppAsync(true);
    }

    [RelayCommand]
    async Task SetMirrorKey()
    {
        await AppSettings.SetMirrorKeyAsync(MirrorKey);
        if (
            Instance.Host.Services.GetRequiredKeyedService<IUpdateService>("Mirror")
            is IMirrorUpdateService mirror
        )
        {
            mirror.SetMirrorKey(MirrorKey);
        }

        TipShow.ShowMessage(LanguageService.GetStringByText("设置成功！"), Symbol.Accept);
    }
}
