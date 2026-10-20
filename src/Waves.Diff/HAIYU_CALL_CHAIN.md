# Haiyu 实际参数下的 hpatchz 调用链

> 实现更新：本页是补充功能前的调用链调查。后续已在 Waves.Diff 补齐校验选择、原地暂存发布与 HDIFFSF20；用户指定旧源删除继续由 Haiyu 处理，库未复制原 exe 的 OldPaths 删除逻辑。当前行为参见 README.md。

分析日期：2026-10-20。只分析和运行合成样本，没有修改补丁引擎或 Haiyu 的执行逻辑。

样本为仓库内 `hpatchz.exe`，SHA-256：`CB73298AF870554D7502E91658AD7D7956B08D184D8CE58CA52E04D8CE7DF971`。证据由该样本随附 IDA 数据库的伪代码、Haiyu 源码以及同参数进程运行共同构成。

## 1. C# 侧真正执行的链

```text
InstallKrdiffResource.RunAsync
    └─ DiffDecompressTask.DecompressKrdiffFile
         └─ DiffDecompressManagerV2.StartAsync
              └─ hpatchz.exe oldFolder diffFile newFolder -f -d -k-KEY

InstallKrdiffGroupResource.ExecuteAsync
    └─ 同一个 DiffDecompressTask / DiffDecompressManagerV2
```

源码定位：

- `InstallKrdiffResource.cs:100`：普通补丁没有传 tempFolder，因此 oldFolder 和 newFolder 相同。
- `InstallKrdiffGroupResource.cs:154`：分组补丁传递 decompressTempFolder；oldFolder 为游戏目录，newFolder 为临时目录。
- `DiffDecompressTask.cs:16`：`newFolder = tempFolder ?? folder`。
- `DiffDecompressManagerV2.cs:48`：上述固定的六个命令行参数。

截至本次读取，Waves.Core 已出现 Waves.Diff 的项目引用，但两个实际安装入口仍然经过原 exe；新增 DiffDecompressV2Task 仍为空类。

## 2. 参数的实际效果

`main`（0x14000B250）先将参数转换为 UTF-8，再进入 `sub_14000B560`。

| 输入 / 默认值 | 实际分支 |
| --- | --- |
| 三个路径 | oldPath / diffFile / outNewPath |
| `-f` | 允许覆盖，并在旧路径等于新路径时选择临时输出分支 |
| `-d` | 对 stdout/stderr 调用 setvbuf；不是目录模式、删除模式或解压算法开关 |
| `-k-KEY` | `sub_14000B360(KEY)` 创建库洛回调上下文并打开父进程创建的命名映射 |
| 未传 `-m` / `-s` | 默认流式策略、4 MiB 缓存预算；底层有自适应预读与缓存优化 |
| 未传 `-n` | 总文件资源预算默认 24；旧引用资源限制内部会扣除其他句柄，并按缓存情况调整 |
| 未传 `-C` | new + copy 校验开启，old 与 diff 自校验关闭 |

校验默认值不只是帮助文字：入口从 `0x140065560` 装载四个标志 0/1/1/0；同参数运行日志也明确打印 `checksumSets: new copy`。

## 3. 格式分发不是由扩展名决定

```text
sub_14000B560
    └─ sub_140002780（打开文件并探测目录容器）
         └─ sub_140002970（HDIFF19 头及内嵌补丁信息）
    ├─ 目录容器：sub_14000DF50
    └─ 非目录容器：sub_14000D390（单文件补丁）
```

Haiyu 的两个正常安装入口都传游戏目录作为旧路径。裸 HDIFF13 / BSDIFF / VCDIFF 的单文件分支会把 oldPath 当文件打开，所以这些裸格式不是当前目录安装路径应优先扩写的能力。文件根容器也受实际路径类型检查限制。

但 HDIFF19 的内部还有两条真实可达分支：

```text
HDIFF19
    ├─ sub_140012B90 识别 HDIFFSF20
    │    └─ sub_140012A50：单流重建
    └─ sub_140011420 识别 HDIFF13
         └─ sub_140012960 → sub_1400116A0：四流重建
```

因此，目录调用本身并不排除内嵌 HDIFFSF20。压缩类型同样来自补丁头，而不是固定命令行参数。当前没有真实生产包证明所有版本只使用 HDIFF13/zstd；本次运行样本为合成 HDIFF19 + HDIFF13，不能据此宣布 HDIFFSF20 已运行验证。

## 4. 目录重建内部链

```text
sub_14000DF50
  ├─ 打开补丁、检查旧路径类型
  ├─ sub_140002970：解析 HDIFF19 + 内嵌 diff 信息
  ├─ sub_14000C7C0：选择解压插件
  ├─ sub_1400036D0：路径、引用列表、尺寸、hash、samePair、执行列表
  ├─ listener.begin：绑定输出目录策略
  ├─ sub_140003480：设置校验集合（当前调用为 new + copy）
  ├─ sub_14000D2F0：申请/调整缓存
  ├─ sub_140003F60：旧文件引用资源表 + 句柄限制 + 逻辑拼接流
  ├─ sub_1400059C0：新目录输出监听器
  │    └─ sub_140006080：按 NewPaths 顺序分发
  │         ├─ 空文件/目录：sub_140005090
  │         ├─ 相同文件：sub_1400051C0 → 输出策略的 copySameFile
  │         └─ 差异文件：sub_140005310（打开当前输出）
  ├─ sub_140004530：选择 HDIFF13 / HDIFFSF20 重建
  │    └─ 写入 sub_140005E60：按新文件尺寸切分逻辑输出流
  │         ├─ sub_1400055E0：写入/读取输出、累计 hash、更新进度
  │         ├─ sub_140005460：关闭文件，核对固定 fadler64
  │         └─ sub_140006080：继续处理后续路径节点
  ├─ sub_140005870：核对 new 与 copy 的累计校验
  ├─ 关闭旧/新文件句柄
  └─ listener.finish：提交或清理输出
```

HDIFF13 内部通过 cover 指定旧位置、新位置和长度；旧数据读取后叠加 RLE，插入部分从 insert 流写出。它是旧数据与补丁共同参与的重建，并非把压缩包直接解压出来。

`sub_140012550` 会根据缓存空间选择整份小旧数据预读，或者 cover/旧数据缓存方案；因此流式模式也不代表每次都直接随机读取磁盘。

### 重要的处理顺序差异

原 exe 的 samePair、空节点和新引用文件按 NewPaths 索引交错处理。当前 Waves.Diff 则先处理全部 samePair，再重建全部新引用。成功内容在既有样本中一致，但进度顺序以及失败时已经写出的文件集合不完全相同。

## 5. 普通补丁：原地目录更新

入口发现 oldPath == outNewPath 后：

1. 选择一个旁路临时目录名称，例如 `old001.tmp`。
2. 调用 `sub_14000DF50`，使用专门的输出策略 `stru_14006BC90`。
3. 新引用文件和空节点写入临时目录。samePair 在重建阶段只读取/校验旧内容，不先复制到临时目录。
4. 重建及 new/copy 累计校验成功后，进入 `sub_140008BE0`。
5. 对 samePair 旧源计数并倒序处理：仍有多处引用时复制，最后一处可以移动到临时目录，避免无谓的大文件复制。
6. `sub_140008840` 清理补丁 OldPaths 中列出的旧节点，再把临时目录的新文件移动回旧目录。未列入 OldPaths 的额外文件不在该清理范围。
7. 重建失败时不进入上述成功提交逻辑，并清理临时输出。提交阶段本身为一系列文件操作，不是全目录原子事务。

这是与当前 Waves.Diff 最重要的差异：后者只对冲突旧源采用逐文件临时路径，不执行同样的 OldPaths 清理和整阶段输出策略。正常数据重建成功不等于目录更新行为完全一致。

## 6. 分组补丁：输出目录不同

oldPath != outNewPath 时使用普通输出策略 `stru_14006BE00`：

- `sub_140008680` / `sub_140004CB0` 将 samePair 从旧目录复制到新目录并参与校验。
- 重建数据写新目录，旧目录不由这个 exe 分支提交更新。
- exe 返回零后，Haiyu 的 `InstallKrdiffGroupResource` 删除 SrcFiles（175–183 行）、登记 DstFiles、删除补丁文件（194 行）。循环结束后由 Haiyu 执行目标删除和 File.Move（206–207 行）。

因此原 exe 与 Haiyu 分别承担不同阶段；替换时应保留边界，避免在补丁库和安装层重复删除、重复搬运。

## 7. 共享内存进度

父进程先创建 KEY 的 4096 字节映射以及 `KEY_mutex`。exe 的 `sub_14000F7B0` 通过 OpenFileMapping / MapViewOfFile / OpenMutex 打开它们。

| 回调 | 作用 |
| --- | --- |
| sub_14000FA00 | 初始化文件总数、总输出字节数，清零累计状态 |
| sub_14000FA90 | 文件开始，写入当前文件大小 |
| sub_14000FB10 | 当前文件及总体已处理字节数 |
| sub_14000FB90 | 文件结束，递增完成文件数 |

映射前 48 字节为六个 ulong：完成文件数、文件总数、当前文件完成字节、当前文件总字节、累计完成字节、累计总字节。写入时尝试获取命名 mutex，超时参数为 500 ms。

原样本有一些展示层细节：文件开始回调会清零累计已处理字节槽；单文件 hash 不匹配可以只通知回调，而最终成败仍由其他校验和返回码决定。替换库无需为了兼容显示而主动复制这些缺陷，但必须区分进度完成与操作成功。

Haiyu 每秒轮询一次、mutex 最多等待约一秒。SpeedValue 是父进程读取进程 I/O 计数，以读字节 + 写字节的差分计算；它不是 exe 写入共享内存的第七个字段，也不等于单纯输出字节速度。

## 8. 同参数运行验证

使用独立合成补丁、命名映射和 mutex，完整传入 Haiyu 的 `-f -d -k-KEY`。只操作独立的验证目录。

有效样本包含：旧引用 a=ABC、samePair copy=COPY、补丁表列出的 removed、表外 unlisted.keep；新路径为 copy=COPY 与 fresh=XYZ。内嵌 HDIFF13 通过 cover + RLE 把 ABC 重建为 XYZ。

| 场景 | 原 exe | 当前 Waves.Diff |
| --- | --- | --- |
| 不同输出目录，合法补丁 | 成功，输出 copy/fresh，旧目录保留 | 同样成功 |
| 相同输出目录，合法补丁 | 成功；a/removed 被清理，表外 unlisted.keep 保留 | 成功，但 a/removed 仍保留 |
| 相同输出目录，新数据累计校验错误 | 退出码 106；原目录未出现 fresh | 抛校验异常，但 fresh 已经写入原目录 |
| 仅 old 校验槽错误，默认参数 | 成功 | old 校验异常 |
| 仅 diff 自校验槽错误，默认参数 | 成功 | diff 校验异常 |

成功样本映射最终值为 `[2,2,3,3,7,7]`。新数据累计校验失败的原生样本也可达到同样进度值，但最终退出码为 106；Haiyu 继续以退出码判断成功是必要的。

最初 run-01 合成样本缺少目录根节点，原生原地分支因临时根目录未创建返回 26。run-02 加入根目录节点后，上表的原地成功/校验失败路径均得到验证。保留这条限制，避免把不完整 fixture 的失败误判为原 exe 的正常行为。

验证工程及结果：

- `D:/WorkSpace/HaiyuGame/Haiyu/artifacts/hpatchz-callchain-probe/Probe.csproj`
- `D:/WorkSpace/HaiyuGame/Haiyu/artifacts/hpatchz-callchain-probe/run-02/results.json`
- `D:/WorkSpace/HaiyuGame/Haiyu/artifacts/hpatchz-callchain-probe/ida-evidence.json`

## 9. 由实际调用推出的优先级

1. 优先补齐普通原地更新的临时目录、samePair 计数、OldPaths 清理与提交策略。
2. 将校验策略与 Haiyu 原生默认 new/copy 对齐，严格 old/diff 校验可以作为选项，而非固定额外扫描。
3. 在现有安装入口适配回调、错误结果和取消；分组提交仍保留在安装层。
4. HDIFF19 内嵌 HDIFFSF20 是目录路径真实可达的格式缺口，应优先于裸 BSDIFF/VCDIFF；是否正在被生产包使用仍需真实包头确认。
5. CLI/SFX、裸单文件的全部格式和文件根容器，不属于当前这两处目录安装调用的优先目标。

之前 47 项回归验证了多个数据格式组合，但没有覆盖这次新增的“完整命令行 + 原地提交/清理 + 共享内存”组合；不能用那组通过结果声称实际安装行为已经完全等价。
