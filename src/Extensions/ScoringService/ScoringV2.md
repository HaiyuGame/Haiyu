# 声骸评分 v2

## 范围
- 静态角色适配分，不是伤害模拟或换装提升率。
- 内置 20 种属性，其中 13 种可作为副词条。固定值与百分比分开。
- 主词条以五星 +25 为参考，支持 COST 1/3/4。低强化声骸按实际数值评分；非五星明确跳过。

## 毕业评分（当前界面）
- `ScoreGraduation` 使用角色模板自己的 `targetEffectiveStatCount`（默认4）计算固定分母：合法词条池中最高N项系数之和，而不是当前装备碰巧命中的N项。
- 单条收益 = min(实际值 / 当前角色规则的 `normalizationReference`，1) × 系数；基准留空时沿用游戏满档。游戏档位仍只负责校验合法数值，修改评分基准不会允许50%暴击副词条。
- 主、副百分制分分别封顶100后加权，避免第五条收益掩盖错误主词条；额外收益记录在 `Progress`，可超过100。
- 理论质量分独立使用游戏满档和最高五项系数，不受角色毕业基准影响。
- 四有效不直接等于毕业：四条低档位、次要收益词条仍可能低于参考收益。
- `gradeThresholds` 独立保存在角色模板：SSS/SS/S/A/B及Graduation（毕业线）。所有评级均为工具/用户定义，不是游戏官方或其他工具的评级。
- 面板目标与声骸毕业状态并列，互不修改分数；共鸣效率仍为静态系数，不根据面板动态切换。
- 原 `ScoreEcho` 保留五项分母口径，旧调用不静默换算法；界面与编辑预览统一调用毕业评分。

## 原始静态评分算法
- 单条贡献 = min(实际值 / 角色基准（留空使用内置最大值），1) × 折算系数。
- 副词条分母 = 完整合法副词条池中最高五项系数之和，不随当前有效词条数量改变。
- 主词条分母 = 对应 COST 最优可选主词条系数 + 对应固定主词条系数。
- 主、副各为百分制，再按合计为 100% 的占比合成。
- 无效词条系数为 0，不额外扣分。未知属性、重复副词条、超上限、超解锁槽位均报错。
- 养成目标独立检查最终面板。实际值直接读取库街区角色详情的 roleAttributeList；缺失时显示暂无面板，不用声骸合计伪造。

## 内置参考
资料核对日期：2026-10-19。攻略给出培养优先级，不给出本项目这些具体评分系数。
数字系数由本项目定义，用户可编辑；静态共鸣效率系数不模拟达到阈值后的动态收益。

- [属性表](https://www.prydwen.gg/wuthering-waves/guides/echo-stats/)
- [今汐](https://www.prydwen.gg/wuthering-waves/characters/jinhsi/)
- [椿](https://www.prydwen.gg/wuthering-waves/characters/camellya/)
- [相里要](https://www.prydwen.gg/wuthering-waves/characters/xiangli-yao/)
- [守岸人](https://www.prydwen.gg/wuthering-waves/characters/the-shorekeeper/)

参考模板针对通用静态配置，武器、共鸣链和队伍可能改变优先级。守岸人目标为战斗口径250%共鸣效率；若使用战斗外面板，应自行调整阈值与增益口径。
其他角色提供明确标记的“自定义起点”，不冒充该角色的研究结论。

## 兼容
`scoringVersion: 2` 为新版。历史无版本 JSON 视作 v1，仅保留旧计算链路与历史测试；界面只导入、编辑 v2，不静默改写旧惩罚参数。
新版 `referenceValue`、`mainStatMaximumBenefit`、`subStatMaximumBenefit` 不参与计算；保留字段用于历史兼容。对外 JSON 使用源生成上下文，界面选中状态忽略序列化。

## 本次回归
- 四条满基准同权词条：毕业分100，理论质量80。
- 四条半基准：毕业分50，不因数量直接判毕业。
- 五条满基准：展示分100，毕业参考进度125%。
- 角色独立基准、评级序列化、非法门槛、正收益种类不足、面板与装备分离均有自动化覆盖。
- 已只读复核当前账号卡提希娅、守岸人、椿共15件真实声骸；逐条折算与固定分母独立核对。

## 验证
- `dotnet test src/Tests/ScoringService.Tests/ScoringService.Tests.csproj`
- `dotnet run --project src/Tests/KuroGameDownloadProgram/KuroGameDownloadProgram.csproj`：只读当前库街区选中账号，角色与模板编号精确匹配，独立展开算式核对。凭据与账号标识不记录。
- 2026-10-19 已实测守岸人、椿，共10件五星声骸。赫卡忒脱敏装备快照用于离线回归，无账号、服务器或设备数据。
- `.tmp/sorting-live/` 只保存本地测试装备属性；测试状态与结果不向远端上传。

展示评级按一位小数判档：SSS≥95、SS≥85、S≥75、A≥60、B≥40，其余C。评级与短评为本工具约定。
