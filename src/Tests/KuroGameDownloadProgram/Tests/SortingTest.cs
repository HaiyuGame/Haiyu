using System.Globalization;
using System.Text;
using Haiyu.KuroClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScoringService.Models;
using ScoringService.Models.Enums;
using Serilog;
using Waves.Core.Contracts;
using Waves.Core.Models.Enums;
using Waves.Core.Services;
using Waves.Settings;
using Calculator = ScoringService.ScoringService;

namespace KuroGameDownloadProgram.Tests
{
    public static class SortingTest
    {
        public static async Task StartTask()
        {
            Console.OutputEncoding = Encoding.UTF8;
            var stage = "读取模板";
            try
            {
                Console.WriteLine("===== 当前库街区账号：真实声骸计算测试 =====");
                Check(Directory.Exists(AppSettings.SortingGroupFolder), "请先在评分模板目录保存模板。");
                var scoringService = new Calculator();
                await scoringService.InitAsync(new()
                {
                    GroupFolder = AppSettings.SortingGroupFolder,
                    ItemFolder = AppSettings.SortingItemFolder
                });
                var templates = (await scoringService.EntryService.GetGroupTemplateAsync())
                    .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
                Check(templates.Length > 0, "评分模板目录中没有模板。");
                foreach (var template in templates)
                    Calculator.ValidateTemplate(template);

                stage = "读取当前本地账号";
                Check(Directory.Exists(AppSettings.LocalUserFolder), "本地账号目录不存在，请先在应用中登录。");
                using var host = Host.CreateDefaultBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<AppSettings>();
                        services.AddSingleton<IKuroAccountService, KuroAccountService>();
                        services.AddSingleton<IKuroClient, KuroClient>();
                        services.AddKeyedSingleton<LoggerService>("AppLog", static (_, _) =>
                        {
                            var logger = new LoggerService();
                            logger.InitLogger(AppSettings.LogPath, RollingInterval.Day);
                            return logger;
                        });
                    }).Build();
                var accountService = host.Services.GetRequiredService<IKuroAccountService>();
                var users = await accountService.GetUsersAsync();
                Check(users is { Count: > 0 }, "未读取到本地账号，请先在应用中登录。");
                var lastUserId = await accountService.AppSettings.GetLastSelectUserAsync();
                var selectedUser = users!.FirstOrDefault(x => x.TokenId == lastUserId);
                // 未设置默认账号且只有一个存储账号时，可明确选用该账号；不回写设置。
                if (selectedUser is null && string.IsNullOrWhiteSpace(lastUserId) && users.Count == 1)
                    selectedUser = users[0];
                Check(selectedUser is not null, "当前选中账号未匹配本地存储，请先在应用中选择账号。");
                accountService.SetCurrentUser(selectedUser!, isWrite: false);
                var account = accountService.CurrentAccount!;
                Console.WriteLine("已读取当前存储账号（账号标识、Token 和设备信息不输出）。");

                stage = "初始化库街区客户端";
                var client = (KuroClient)host.Services.GetRequiredService<IKuroClient>();
                using var httpClient = client.HttpClient;
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await client.InitAsync().WaitAsync(timeout.Token);

                stage = "读取鸣潮绑定及角色列表";
                var bindings = await client.GetGamerAsync(account, (int)GameType.Waves, timeout.Token);
                Check(bindings is { Success: true, Data.Count: > 0 }, "鸣潮绑定读取失败或为空，请检查登录状态。");
                var binding = bindings!.Data.FirstOrDefault(x => x.IsDefault) ?? bindings.Data[0];
                var roleData = await client.GetGamerRoleDataAsync(account, binding, timeout.Token);
                Check(roleData?.RoleList is { Count: > 0 }, "鸣潮角色列表为空，请检查库街区数据展示设置。");

                // 优先测试与角色编号精确匹配的模板；样例模板只用于链路验证并明确提示。
                var candidates = roleData!.RoleList.OrderByDescending(x => x.Level).ThenBy(x => x.RoleId).ToArray();
                var matched = (from template in templates
                               from role in candidates
                               where template.RoleId == role.RoleId.ToString(CultureInfo.InvariantCulture)
                               select (Template: template, Role: role)).ToArray();
                var sample = templates.FirstOrDefault(x => x.Id == "demo" && x.RoleId == "demo-role");
                Check(matched.Length > 0 || sample is not null,
                    "模板没有匹配已拥有的角色，请将模板 roleId 设置为游戏角色编号。");
                var chosenTemplate = matched.Length > 0 ? matched[0].Template : sample!;
                var chosenRole = matched.Length > 0 ? matched[0].Role : candidates[0];
                Console.WriteLine($"角色：{chosenRole.RoleName}，等级：{chosenRole.Level}");
                Console.WriteLine($"模板：{chosenTemplate.Name}");
                if (matched.Length == 0)
                    Console.WriteLine("注意：当前使用 demo 演示模板，仅验证真实数据计算链路，不代表该角色正式评分或真实档位。");

                stage = "读取角色声骸详情";
                var detail = await client.GetGamerRoilDetily(account, binding, chosenRole.RoleId, timeout.Token);
                Check(detail?.Role?.RoleId == chosenRole.RoleId, "角色详情缺失或返回了不同角色。");
                var phantoms = detail!.PhantomData?.EquipPhantomList;
                Check(phantoms is { Count: > 0 }, "选中角色没有已装备的声骸。");

                stage = "转换属性、评分及独立算式核对";
                var matchedCount = 0;
                var verifiedCount = 0;
                for (var index = 0; index < phantoms!.Count; index++)
                {
                    var phantom = phantoms[index];
                    var mainProperties = (phantom.MainProps ?? []).Select(x => (x.AttributeName, x.AttributeValue)).ToArray();
                    var subProperties = (phantom.SubProps ?? []).Select(x => (x.AttributeName, x.AttributeValue)).ToArray();
                    var echo = new EchoData
                    {
                        Id = $"slot-{index + 1}",
                        MainStats = SortingStatAdapter.Convert(mainProperties, chosenTemplate, EntryUsage.MainStatScoring),
                        SubStats = SortingStatAdapter.Convert(subProperties, chosenTemplate, EntryUsage.SubStatScoring)
                    };
                    var score = scoringService.ScoreEcho(chosenTemplate, echo);
                    VerifySection(chosenTemplate, echo.MainStats, EntryUsage.MainStatScoring,
                        chosenTemplate.MainStatMaximumBenefit, score.MainStats);
                    VerifySection(chosenTemplate, echo.SubStats, EntryUsage.SubStatScoring,
                        chosenTemplate.SubStatMaximumBenefit, score.SubStats);
                    Equal(score.MainStats.Score * chosenTemplate.MainStatWeight +
                        score.SubStats.Score * chosenTemplate.SubStatWeight, score.TotalScore);
                    matchedCount += score.MainStats.Details.Count(x => x.IsMatched) + score.SubStats.Details.Count(x => x.IsMatched);
                    verifiedCount++;

                    Console.WriteLine($"\n[{index + 1}] {phantom.PhantomProp?.Name ?? "未知声骸"} / Cost {phantom.Cost} / 等级 {phantom.Level}");
                    PrintSection("主词条", mainProperties, score.MainStats);
                    PrintSection("副词条", subProperties, score.SubStats);
                    Console.WriteLine($"  综合评分：{score.TotalScore:F2} / 100（独立算式核对通过）");
                }
                Check(matchedCount > 0, "实际声骸未匹配到任何评分规则，请检查模板名称和类型。");
                Console.WriteLine($"\n测试通过：{verifiedCount} 件声骸，{matchedCount} 条属性匹配模板，评分及惩罚换算核对通过。");
                Console.WriteLine("未匹配规则的属性保留在明细中，贡献为零；不会自动当成惩罚属性。");
                Console.WriteLine("养成检查未执行：当前角色详情模型未包含最终面板，未用声骸属性合计代替角色面板。");
            }
            catch (Exception exception)
            {
                // 不输出异常全文、请求头或响应原文，避免将账号凭据带入测试日志。
                Console.Error.WriteLine($"计算测试失败，阶段：{stage}，异常类型：{exception.GetType().Name}。");
                Environment.ExitCode = 1;
            }
        }

        /// <summary>从输入与模板重新展开算式，核对贡献、固定分母和百分制结果。</summary>
        private static void VerifySection(EntryGroup template, List<StatValue> stats,
            EntryUsage usage, decimal maximumBenefit, StatScoreResult result)
        {
            decimal benefit = 0, penalty = 0;
            foreach (var stat in stats)
            {
                var rule = template.Items.SingleOrDefault(x => x.Usage == usage &&
                    x.EntryNo == stat.EntryNo && x.Type == stat.Type);
                if (rule is null)
                    continue;
                benefit += stat.Value / rule.ReferenceValue!.Value * rule.Weight.GetValueOrDefault();
                penalty += stat.Value / rule.ReferenceValue!.Value * rule.PenaltyCoefficient.GetValueOrDefault();
            }
            Equal(benefit, result.Benefit);
            Equal(penalty, result.Penalty);
            Equal(maximumBenefit, result.MaximumBenefit);
            Equal(Math.Clamp((benefit - penalty) / maximumBenefit * 100, 0, 100), result.Score);
            Check(result.Details.Count == stats.Count, "评分明细数量与输入属性数量不一致。");
        }

        /// <summary>按原始属性顺序展示数值、是否匹配、收益及惩罚，未知属性也保留名称。</summary>
        private static void PrintSection(string title, (string Name, string Value)[] properties, StatScoreResult result)
        {
            Console.WriteLine($"  {title}：{result.Score:F2} / 100，收益 {result.Benefit:F4}，惩罚 {result.Penalty:F4}，分母 {result.MaximumBenefit}");
            for (var i = 0; i < properties.Length; i++)
            {
                var item = result.Details[i];
                Console.WriteLine($"    {properties[i].Name} {properties[i].Value} → 收益 {item.Benefit:F4}，惩罚 {item.Penalty:F4}" +
                    (item.IsMatched ? "" : "（未配置规则）"));
            }
        }

        private static void Equal(decimal expected, decimal actual)
            => Check(Math.Abs(expected - actual) < 0.00000001m, "计算结果与独立算式不一致。");

        private static void Check(bool condition, string message)
        {
            if (condition)
                return;
            Console.Error.WriteLine(message);
            throw new InvalidOperationException("计算测试断言失败。");
        }
    }
}
