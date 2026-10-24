using System.Globalization;
using System.Text;
using Haiyu.KuroClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Serilog;
using Waves.Core.Contracts;
using Waves.Core.Services;
using Waves.Settings;
using Calculator = ScoringService.ScoringService;

namespace KuroGameDownloadProgram.Tests;

public static class SortingTest
{
    public static async Task StartTask()
    {
        Console.OutputEncoding = Encoding.UTF8;
        string stage = "读取当前账号";
        try
        {
            using var host = Host.CreateDefaultBuilder().ConfigureServices(services =>
            {
                services.AddSingleton<AppSettings>();
                services.AddSingleton<IKuroAccountService, KuroAccountService>();
                services.AddSingleton<IKuroClient, KuroClient>();
                services.AddSingleton<KuroSortingService>();
                services.AddKeyedSingleton<LoggerService>("AppLog", static (_, _) =>
                {
                    var logger = new LoggerService();
                    logger.InitLogger(AppSettings.LogPath, RollingInterval.Day);
                    return logger;
                });
            }).Build();
            var accounts = host.Services.GetRequiredService<IKuroAccountService>();
            var users = await accounts.GetUsersAsync();
            var selectedId = await accounts.AppSettings.GetLastSelectUserAsync();
            var user = users?.FirstOrDefault(x => x.TokenId == selectedId);
            if (user is null && string.IsNullOrWhiteSpace(selectedId) && users?.Count == 1) user = users[0];
            Check(user is not null, "请先在应用中登录并选择账号。");
            accounts.SetCurrentUser(user!, isWrite: false);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            stage = "初始化库街区";
            var client = host.Services.GetRequiredService<IKuroClient>();
            client.HttpClient.Timeout = TimeSpan.FromSeconds(30);
            await client.InitAsync().WaitAsync(timeout.Token);
            var bridge = host.Services.GetRequiredService<KuroSortingService>();
            stage = "读取角色列表";
            var roles = await bridge.GetRolesAsync(timeout.Token);
            Console.WriteLine($"当前账号角色：{roles.Count}（账号及凭据不输出）");
            foreach (var role in roles) Console.WriteLine($"  {role.RoleName} · Lv.{role.Level} · 角色编号 {role.RoleId}");
            if (Environment.GetCommandLineArgs().Contains("--list")) return;
            int count = 0;
            foreach (var role in roles.Where(x => CharacterScorePresets.All.Any(p => p.RoleId == x.RoleId.ToString(CultureInfo.InvariantCulture))))
            {
                var template = CharacterScorePresets.ForRole(role.RoleId.ToString(CultureInfo.InvariantCulture), role.RoleName);
                stage = "读取角色声骸";
                var snapshot = await bridge.GetSnapshotAsync(role.RoleId, template, timeout.Token);
                var echoes = snapshot.Echoes;
                var development = new Calculator().EvaluateDevelopment(template, snapshot.Panel);
                foreach (var target in development.Targets) Console.WriteLine($"面板：{target.EntryName} {target.ActualValue} / 目标 {target.TargetValue} · {(target.IsMet ? "已达标" : "未达标")}");
                Console.WriteLine($"\n{role.RoleName} · 模板 {template.Name}\n参考：{template.SourceUrl}\n系数由本工具定义，非网站原始评分；静态适配，不代表伤害提升。");
                var calculator = new Calculator();
                foreach (var echo in echoes)
                {
                    if (echo.Data.Rarity != 5)
                    {
                        Console.WriteLine($"  {echo.Name} · {echo.Data.Rarity}星 · 跳过（当前主词条标准为五星）");
                        continue;
                    }
                    stage = "评分及独立算式核对";
                    var result = calculator.ScoreEcho(template, echo.Data);
                    Verify(template, echo.Data, result);
                    var graduation = calculator.ScoreGraduation(template, echo.Data);
                    var expectedMax = template.Items.Where(x => x.Usage == EntryUsage.SubStatScoring).Select(x => x.Weight ?? 0).OrderDescending().Take(template.TargetEffectiveStatCount).Sum();
                    Equal(expectedMax, graduation.Score.SubStats.MaximumBenefit);
                    Equal(Math.Min(result.SubStats.Benefit / expectedMax * 100, 100), graduation.Score.SubStats.Score);
                    Console.WriteLine($"  毕业 {graduation.Score.TotalScore:F2} · {ScoreRatings.Grade(graduation.Score.TotalScore, template.GradeThresholds)} · 理论质量 {graduation.TheoreticalScore:F2} · {ScoreRatings.Comment(graduation.Score.TotalScore, template.GradeThresholds)}");
                    Console.WriteLine($"  {echo.Name} · COST {echo.Data.Cost} · +{echo.Data.Level}：主 {result.MainStats.Score:F2} · 副 {result.SubStats.Score:F2} · 综合 {result.TotalScore:F2}");
                    foreach (var detail in result.SubStats.Details)
                        Console.WriteLine($"    {EchoStatCatalog.Find(detail.EntryNo, detail.Type)?.Name} {detail.Value} → {detail.Benefit:F5}");
                    var folder = Path.Combine(Environment.CurrentDirectory, ".tmp", "sorting-live");
                    Directory.CreateDirectory(folder);
                    // 仅记录装备属性与公开角色模板，不写入账号、Token、服务器或请求数据。
                    await File.WriteAllTextAsync(Path.Combine(folder, $"{role.RoleId}-{echo.Data.Id}.json"),
                        System.Text.Json.JsonSerializer.Serialize(echo.Data, ScoringJsonContext.Default.EchoData));
                    count++;
                }
            }
            Check(count > 0, "当前账号没有匹配内置角色模板的五星声骸。");
            Console.WriteLine($"\n通过：{count}件真实声骸；满分基准、逐条换算、总分均与独立算式一致。");
            Console.WriteLine("养成目标已使用角色详情roleAttributeList检查，不使用声骸合计代替。");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"测试未完成：{stage} · {ex.GetType().Name}。");
            if (ex is ArgumentException or FormatException or InvalidOperationException) Console.Error.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void Verify(EntryGroup template, EchoData echo, EchoScoreResult result)
    {
        decimal W(string id, EntryType type, EntryUsage usage) => template.Items.FirstOrDefault(x => x.EntryNo == id && x.Type == type && x.Usage == usage)?.Weight ?? 0;
        decimal Sub(StatValue x) => x.Value / EchoStatCatalog.Find(x.EntryNo, x.Type)!.SubMaximum * W(x.EntryNo, x.Type, EntryUsage.SubStatScoring);
        var subMax = template.Items.Where(x => x.Usage == EntryUsage.SubStatScoring).Select(x => x.Weight ?? 0).OrderDescending().Take(5).Sum();
        var mainMax = EchoStatCatalog.All.Where(x => x.Type == EntryType.Percentage && EchoStatCatalog.MainMaximum(x.Id, x.Type, echo.Cost) > 0)
            .Max(x => W(x.Id, x.Type, EntryUsage.MainStatScoring)) + W(echo.Cost == 1 ? "hp" : "atk", EntryType.Value, EntryUsage.MainStatScoring);
        var mainGain = echo.MainStats.Sum(x => x.Value / EchoStatCatalog.MainMaximum(x.EntryNo, x.Type, echo.Cost) * W(x.EntryNo, x.Type, EntryUsage.MainStatScoring));
        Equal(subMax, result.SubStats.MaximumBenefit);
        Equal(mainMax, result.MainStats.MaximumBenefit);
        Equal(echo.SubStats.Sum(Sub) / subMax * 100, result.SubStats.Score);
        Equal(mainGain / mainMax * 100, result.MainStats.Score);
        Equal(0, result.MainStats.Penalty + result.SubStats.Penalty);
        Equal(result.MainStats.Score * template.MainStatWeight + result.SubStats.Score * template.SubStatWeight, result.TotalScore);
        Check(result.TotalScore is >= 0 and <= 100, "分数超出范围。");
    }
    private static void Equal(decimal expected, decimal actual) => Check(Math.Abs(expected - actual) < 0.00000001m, "独立算式不一致。");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
