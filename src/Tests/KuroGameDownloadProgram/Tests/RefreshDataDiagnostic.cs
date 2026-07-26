using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Waves.Api.Models.Communitys;
using Waves.Core;
using Waves.Core.Contracts;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Waves.Core.Settings;
using WavesLauncher.Core.Contracts;

namespace KuroGameDownloadProgram.Tests;

internal static class RefreshDataDiagnostic
{
    public static async Task RunAsync(
        string[] args,
        CancellationToken cancellationToken = default
    )
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("库街区 refreshData 应用链路诊断");
        Console.WriteLine("DI → InitAsync → Auto 默认账号 → 角色列表 → requestToken → refreshData");
        Console.WriteLine();

        Directory.CreateDirectory(Path.GetDirectoryName(AppSettings.LogPath)!);
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddGameContext();
                services.AddSingleton<AppSettings>();
                services.AddSingleton<IKuroAccountService, KuroAccountService>();
                services.AddSingleton<IKuroClient, KuroClient>();
                services.AddKeyedSingleton<LoggerService>(
                    "AppLog",
                    static (_, _) =>
                    {
                        var logger = new LoggerService();
                        logger.InitLogger(AppSettings.LogPath, RollingInterval.Day);
                        return logger;
                    }
                );
            })
            .Build();

        var accountService = host.Services.GetRequiredService<IKuroAccountService>();
        var kuroClient = host.Services.GetRequiredService<IKuroClient>();

        Console.WriteLine("===== 1. 初始化 KuroClient =====");
        await kuroClient.InitAsync();
        var concreteClient = (KuroClient)kuroClient;
        Console.WriteLine($"IP: {FormatValue(concreteClient.Ip)}");
        Console.WriteLine($"IP length: {concreteClient.Ip?.Length ?? 0}");
        Console.WriteLine(
            $"IP has surrounding whitespace: {concreteClient.Ip != concreteClient.Ip?.Trim()}"
        );
        Console.WriteLine();

        Console.WriteLine("===== 2. 读取并选择 Auto 默认账号 =====");
        var accounts = await accountService.GetUsersAsync();
        Console.WriteLine($"LocalUserFolder: {AppSettings.LocalUserFolder}");
        Console.WriteLine($"账号数量: {accounts?.Count ?? 0}");
        if (accounts is null || accounts.Count == 0)
        {
            Console.WriteLine("没有读取到本地账号，测试终止。");
            return;
        }

        await accountService.SetAutoUser();
        var account = accountService.CurrentAccount;
        Console.WriteLine($"Auto 当前账号存在: {account is not null}");
        Console.WriteLine($"Auto 当前账号 userId: {Mask(account?.UserId)}");
        Console.WriteLine($"token length: {account?.Token?.Length ?? 0}");
        Console.WriteLine($"did: {Mask(account?.DeviceId)}");
        Console.WriteLine($"did length: {account?.DeviceId?.Length ?? 0}");
        if (account is null)
        {
            var configuredId = await accountService.AppSettings.GetLastSelectUserAsync();
            Console.WriteLine($"LastSelectUser: {Mask(configuredId)}");
            Console.WriteLine("Auto 默认账号没有匹配到本地账号，测试终止。");
            return;
        }
        Console.WriteLine();

        Console.WriteLine("===== 3. 使用真实接口读取鸣潮角色 =====");
        var rolesResponse = await kuroClient.GetGamerAsync(
            account,
            GameType.Waves,
            cancellationToken
        );
        Console.WriteLine($"role/list code: {rolesResponse?.Code}");
        Console.WriteLine($"role/list success: {rolesResponse?.Success}");
        Console.WriteLine($"role/list msg: {rolesResponse?.Msg}");
        Console.WriteLine($"角色数量: {rolesResponse?.Data?.Count ?? 0}");
        if (rolesResponse?.Data is null || rolesResponse.Data.Count == 0)
        {
            Console.WriteLine("角色列表为空，测试终止。");
            return;
        }

        foreach (var item in rolesResponse.Data)
        {
            Console.WriteLine(
                $"  roleId={item.RoleId}, serverId={item.ServerId}, "
                    + $"gameId={item.GameId}, isDefault={item.IsDefault}, roleName={item.RoleName}"
            );
        }

        GameRoilDataItem role =
            rolesResponse.Data.FirstOrDefault(static item => item.IsDefault)
            ?? rolesResponse.Data[0];
        Console.WriteLine(
            $"选中角色: roleId={role.RoleId}, serverId={role.ServerId}, gameId={role.GameId}"
        );
        Console.WriteLine();

        Console.WriteLine("===== 4. 使用真实 UpdateRefreshToken =====");
        var refreshToken = await kuroClient.UpdateRefreshToken(account, role, cancellationToken);
        Console.WriteLine($"requestToken result exists: {refreshToken is not null}");
        Console.WriteLine($"b-at: {Mask(refreshToken?.AccessToken)}");
        Console.WriteLine($"b-at length: {refreshToken?.AccessToken?.Length ?? 0}");
        if (refreshToken is null || string.IsNullOrWhiteSpace(refreshToken.AccessToken))
        {
            Console.WriteLine("requestToken 未返回有效 b-at，测试终止。");
            return;
        }
        Console.WriteLine();

        Console.WriteLine("===== 5. 使用真实 RefreshGamerDataAsync =====");
        Console.WriteLine(
            $"表单参数: gameId={role.GameId}&roleId={role.RoleId}&serverId={role.ServerId}"
        );
        var result = await kuroClient.RefreshGamerDataAsync(account, role, cancellationToken);
        Console.WriteLine($"refreshData response exists: {result is not null}");
        Console.WriteLine($"refreshData code: {result?.Code}");
        Console.WriteLine($"refreshData success: {result?.Success}");
        Console.WriteLine($"refreshData msg: {result?.Msg}");
        Console.WriteLine($"refreshData data: {result?.Data}");
    }

    private static string FormatValue(string? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<null-or-empty>";
        }
        if (value.Length <= 8)
        {
            return $"<redacted:length={value.Length}>";
        }
        return $"{value[..4]}…{value[^4..]} (length={value.Length})";
    }
}
