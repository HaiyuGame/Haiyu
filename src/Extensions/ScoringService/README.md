# 声骸评分与角色养成核心

当前阶段实现独立计算、参数校验、JSON 源生成序列化和自动化测试。
界面、库街区适配、JSON 文件增删改查、游戏实际档位表和整套声骸聚合尚待接入。
`ScoringEntryService` 目前只保留存储目录入口，`InitAsync` 不执行加载或保存。

## 模型

- `EntryItem`：模板规则。`EntryUsage` 区分主词条评分、副词条评分和角色养成。
- `EntryType`：区分百分比与固定值。规则和输入按 `(EntryNo, Type)` 匹配，不按展示名称匹配。
- `EntryGroup`：角色模板，包含规则、主副占比及各自理论最大收益。
- `EchoData`：单件声骸的主、副词条。重复属性应在导入环节处理，单件计算中重复键会报错。
- `CharacterPanel`：角色最终面板，而非声骸属性之和。调用方需统一等级、武器、战斗增益等口径。
- 计算结果含每条属性的归一化数值、收益、惩罚和是否匹配，供界面展示。

百分比 `10%` 一律存为 `10`，固定攻击力 `24` 存为 `24`；占比 `30%` 存为 `0.3`。
数值采用 `decimal`，内部保留精度，展示时再格式化为两位小数。

## 算法

```text
归一化值 = 实际值 / ReferenceValue
收益 = 归一化值 × Weight
惩罚 = 归一化值 × PenaltyCoefficient
主/副词条分 = Clamp((Σ收益 − Σ惩罚) / 对应理论最大收益 × 100, 0, 100)
总分 = 主词条分 × MainStatWeight + 副词条分 × SubStatWeight
```

同一规则的收益权重与惩罚系数互斥，系数非负；未填系数按零处理。
没有配置的实际属性计零贡献，但保留 `IsMatched = false` 的明细。
惩罚是按数值比例换算后参与减法，不是命中固定扣分。
主副分分别限制在 0～100，然后合成；因此副词条的额外负贡献不会扣掉主词条分。
归一化值本身不截断：超过基准值仍按比例计算，最终评分才限制范围，原始贡献仍可查看。
输入负数、重复规则、错误枚举、零基准、混用养成与评分参数等直接报参数错误。

### 固定分母

`MainStatMaximumBenefit` 和 `SubStatMaximumBenefit` 是配置好的理论最优收益，
与当前声骸实际命中几条有效属性无关。两者均须大于零，主副占比非负且合计为 1。

此阶段由模板作者根据目标 Cost、品质、等级、属性上限、可用槽位和互斥条件确定分母及基准。
当前核心只校验分母为正，不推导或验证游戏合法最优组合，也不自动按 Cost 切换规则。
不要把所有候选收益权重简单求和当作满分：候选属性未必能同时出现。
本例特意只设两个副词条收益候选，假设两者可以同时出现，所以分母为 `1 + 0.5 = 1.5`。
正式接入游戏规则后应增加分 Cost 等条件的评分配置和理论最优组合计算。

### 养成目标

养成仅检查 `CharacterPanel`；独立于声骸评分，不加分、不扣分。
`GreaterThan` 是严格大于，`GreaterThanOrEqual` 是大于等于；其余比较符同理。
全部目标达标时 `IsComplete = true`。缺失面板属性视为未达标，而非零值。
未配置任何目标时 `IsConfigured = false` 且 `IsComplete = false`，避免空集合被显示成养成完成。

## 可复算的基础样例

`Examples/basic-template.json` 中的所有基准值、权重均是演示参数，不是游戏实际档位或角色推荐值。

| 用途 | 属性 | 实际值 | 基准值 | 收益权重 | 惩罚系数 | 贡献 |
|---|---|---:|---:|---:|---:|---:|
| 主词条 | 攻击力百分比 | 30 | 30 | 1 | 0 | +1 |
| 副词条 | 攻击力百分比 | 8 | 10 | 1 | 0 | +0.8 |
| 副词条 | 固定攻击力 | 24 | 40 | 0.5 | 0 | +0.3 |
| 副词条 | 防御力百分比 | 6 | 10 | 0 | 0.3 | -0.18 |

```text
主词条分 = 1 / 1 × 100 = 100
副词条分 = (0.8 + 0.3 − 0.18) / 1.5 × 100 ≈ 61.33
总分 = 100 × 0.3 + 61.333… × 0.7 ≈ 72.93
```

角色面板共鸣效率为 `260` 时，不满足 `> 260`；为 `260.01` 时满足。
两种面板不会改变同一件声骸的评分。

## 调用

```csharp
using System.Text.Json;
using ScoringService;
using ScoringService.Models;
using ScoringService.Models.Enums;

var template = JsonSerializer.Deserialize(
    File.ReadAllText(templatePath), ScoringJsonContext.Default.EntryGroup)!;

var echo = new EchoData
{
    Id = "demo-echo",
    MainStats =
    [
        new() { EntryNo = "atk", Type = EntryType.Percentage, Value = 30 }
    ],
    SubStats =
    [
        new() { EntryNo = "atk", Type = EntryType.Percentage, Value = 8 },
        new() { EntryNo = "atk", Type = EntryType.Value, Value = 24 },
        new() { EntryNo = "def", Type = EntryType.Percentage, Value = 6 }
    ]
};
var panel = new CharacterPanel
{
    Stats = [new() { EntryNo = "recharge", Type = EntryType.Percentage, Value = 260.01m }]
};

var service = new global::ScoringService.ScoringService();
var result = service.Evaluate(template, echo, panel);
// result.EchoScore.TotalScore ≈ 72.93
// result.Development.IsComplete == true
```

纯计算无需调用 `InitAsync`。

## 测试

在仓库根目录运行：

```powershell
dotnet test src/Tests/ScoringService.Tests/ScoringService.Tests.csproj
```

覆盖手工算式、按数值/系数惩罚、固定值与百分比隔离、主副规则隔离、固定分母、
百分制上下界、养成边界与所有比较方式、面板缺失、多目标、非法参数以及 JSON 源生成往返。
测试关闭 JSON 默认反射，验证源生成上下文可独立使用。
