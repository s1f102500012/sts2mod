# HextechRunes 联机稳定性代码审查

审查日期：2026-07-26
审查基线：根仓库 `efba55fe1bdc3d3ab6cc536a0e3281c99c9871fd`，并包含工作树中尚未提交的万用瞄准镜概率加算改动
目标版本：STS2 0.107.1、0.109.0
审查性质：只读代码审查；本文不表示问题已经修复，也不构成双客户端实机证明

## 0. 结论摘要

本轮没有确认需要立即停止发布的 P0 问题，但确认了 5 组应优先处理的联机稳定性缺口：

1. 0.109 的外部模型注册会在官方初始化前提前写入 SavedProperty net-id 表，破坏官方确定性排序，而且这些提前写入的属性不会进入官方兼容 Hash。
2. PlayerChoice 消息最终发送失败后，多条调用链仍会发放符文、返回选择或落地本地配置；远端则继续等待。
3. 远端选择等待缺少完整取消协议，本地关闭界面、换跑局或断线时可能遗留等待任务，`Task.WhenAll` 还会把单个取消放大为整批卡住。
4. 多人符文逐人发放没有幂等提交账本；中途失败后本幕保持“未解析”，重试可能重复发放已成功玩家的符文。
5. 最大生命换算使用跨 `await` 的进程全局布尔守卫，无关玩家的并发最大生命命令可能被错误放行，造成 `BaseMaxHp` 与实际 `MaxHp` 分离。

建议先修 S1、C1、C2、C3、A1，再处理消息相关性、编解码防御和战斗内的失败原子性。

### 严重度与证据口径

- P1：一旦进入对应异常或并发路径，存在明确的卡死、重复发放、状态分叉或协议表错位。
- P2：代码级缺口已确认，但触发依赖发送异常、第三方并发、表现层异常或特定内容组合。
- P3：防未来扩展、协议误用或诊断假阴性的加固项。
- “已确认”指源码与调用链成立；除特别说明外，本轮没有完成双客户端游戏内故障注入。

| ID | 级别 | 结论 | 置信度 | 建议批次 |
|---|---:|---|---|---|
| S1 | P1 | 0.109 初始化前的 Debug 注入污染 SavedProperty net-id，且官方 Hash 漏检 | 确定 | 1 |
| S2 | P2 | SavedProperty 自检只查全局属性名，查不到载体未进 per-type cache | 高 | 1 |
| C1 | P1 | 选择消息最终发送失败后仍提交本地结果 | 高 | 2 |
| C2 | P1 | 远端选择等待缺少取消终态，批量等待可永久悬挂 | 高 | 2 |
| C3 | P1 | 多人符文逐项发放没有幂等 journal，失败重试可重复获得 | 高 | 2 |
| C4 | P2 | singleton 等待并非按帧等待，异常 fallback 又不对称 | 高 | 2 |
| C5 | P2 | Forge、随机授予、敌方调整 payload 缺少操作相关性 | 高 | 3 |
| C6 | P2 | codec 计数字段存在溢出/OOM 面，编码与解码上限不对称 | 高 | 3 |
| A1 | P1 | 最大生命换算的全局异步守卫会压制无关命令 | 高 | 4 |
| A2 | P2 | “我们的治疗”全局深度会漏掉重叠治疗 | 高 | 4 |
| A3 | P2 | 玩家濒死狂宴先记账后 Apply，失败不回滚 | 高 | 4 |
| A4 | P2 | Goldrend 战后扣金与广播不是原子事务 | 高 | 4 |
| D1 | P2 | ColorDiscovery 把未排序候选集合写入稳定随机盐 | 高 | 5 |
| D2 | P2/策略 | 同版本号不同构建仍能通过入房门禁 | 高 | 5 |
| D3 | P3 | 外部符文配置 ordinal 与配置键只使用 `ModelId.Entry` | 中高 | 5 |

## 1. 审查范围与方法

本轮重点检查：

- 主机权威、玩家选择同步、消息发送失败与远端等待生命周期；
- SavedProperty 注册、net-id 规范化、版本兼容门禁；
- 稳定随机的候选排序、盐值和持久化状态；
- Harmony 异步前后缀、全局递归守卫、先记账后执行；
- 战斗结束后的 RewardSynchronizer 状态提交；
- 0.107.1 与 0.109.0 的原版实现差异。

双版本反编译重点核验了：

- `RunManager.InitializeShared`
- `PlayerChoiceSynchronizer`
- `RewardSynchronizer`
- `CreatureCmd.GainMaxHp/LoseMaxHp/SetMaxHp`
- `ModelIdSerializationCache`（0.109.0）
- `SavedPropertiesTypeCache`（0.107.1）
- `OneTimeInitialization`（0.109.0）

关键事实：

- 两个目标版本都会在 `RunManager.InitializeShared` 中同步创建 `PlayerChoiceSynchronizer`；因此正常 run 初始化不存在“短暂等待后才创建”的窗口。当前 null 分支应视为换跑局、已清理对象、异常安装或过期异步任务，而不是正常启动降级。
- 两个目标版本的 `PlayerChoiceSynchronizer.Dispose()` 都只注销网络 message handler，不会主动完成模组自己的事件 TCS。
- 0.109.0 的 `CacheSavedPropertiesForTypeDebug` 没有 `_initialized` 守卫。
- 原版 `RewardSynchronizer.SyncLocalGoldLost` 只有校验与发送，没有 ack、事务或回滚。

## 2. 序列化与兼容注册

### S1. [P1][确定] 0.109 初始化前的 Debug 注入污染 SavedProperty net-id，并被官方 Hash 漏检

位置：

- `src/Bootstrap/HextechSavedPropertyBootstrap.cs:7-30`
- `src/Api/HextechRunesApi.cs:41-44,68-70,94-96,138-150`
- 实际调用方：`../HextechRunesSponsorPack/src/ModEntry.cs:112-176`

当前 0.109 分支的注释假设：

> `CacheSavedPropertiesForTypeDebug(type)` 在官方 `Init()` 前会抛 `InvalidOperationException`，catch 后交给官方稍后统一收录。

反编译 0.109.0 后确认该假设不成立：

1. `CacheSavedPropertiesForTypeDebug` 直接调用 `CachePropertiesForType(type, null, null)`；
2. 它会立即写 `_savedPropertyCache`、`_propertyNameToNetIdMap` 和 `_netIdToPropertyNameMap`；
3. 官方 `ModelIdSerializationCache.Init()` 后续只对“尚不存在”的属性名追加并写入 `XxHash32`；
4. 因此前置属性不会被重新排序，也不会计入官方 Hash。

官方初始化时序是：

`ExecuteVeryEarly → ModManager.Initialize（执行模组初始化器） → ExecuteEssential → ModelDb.Init → ModelIdSerializationCache.Init`

SponsorPack 在模组初始化器中通过公开 API 注册多种符文、锻造、事件遗物和附魔图标，故该路径实际可达。若两端扩展初始化顺序不同，属性 net-id 可能不同，而官方 Hash 仍可能相同，无法在握手阶段提前发现。

最小修复：

1. 0.107 保留 `InjectTypeIntoCache` 与后续规范化。
2. 0.109 在官方缓存未初始化时不调用 Debug API，只完成模型池注册，让官方 `ModelDb.Init + ModelIdSerializationCache.Init` 统一排序与散列。
3. 0.109 初始化后：
   - 已在 per-type cache 中则幂等返回；
   - 带 `[SavedProperty]` 但未缓存则 fail-fast，禁止尾部追加。
4. 注册窗口检查必须在任何 registry/pool 写入前执行，避免抛错后留下半注册状态。
5. `RegisterEnchantmentIcon` 是视觉 API，不应承担序列化模型注入；应将两项职责拆开。

不要只依赖 `HextechSavedPropertyNetIdHooks.IsCanonicalized` 判断官方缓存状态，因为对应 Harmony 钩子安装失败时该值可能永远为 false。应使用官方 Init 前后的可观察语义，或只读 `_initialized`。

回归测试：

- 0.109 `ResetForTest` 后、Init 前调用 `InjectModelType`，断言属性表完全不变；
- 两个假外部载体交换注册顺序后执行 Init，断言 net-id 表和 Hash 相同；
- Init 后注册未缓存的 SavedProperty 载体，断言 fail-fast 且 registry 版本/内容不变；
- 0.107 仍应确认手动注入生效。

### S2. [P2][诊断缺口] 自检只核对全局属性名，无法发现载体未进 per-type cache

位置：

- `src/Bootstrap/HextechSavedPropertyBootstrap.cs:76-115,172-193`
- `tests/HextechRunes.Tests/Program.cs:759-798`

当前 `WarnOnUninjectedSavedPropertyCarriers` 只把全局 `_netIdToPropertyNameMap` 转成属性名集合。原版序列化实际还有独立的 `Type → List<PropertyInfo>` 缓存。

若漏登记的类型使用了一个已由其他模型登记的同名 SavedProperty：

- 全局属性名检查会通过；
- 该具体类型仍可能不在 per-type cache；
- 保存/同步时其属性会被静默省略。

本轮没有确认 HextechRunes 自身已存在漏登记载体，因此这是自检假阴性，不是已复现玩家故障。

最小修复：

- 对每个载体读取 `GetJsonPropertiesForType(type)`，逐项比较实际 `[SavedProperty]`；
- 去重键使用 `(type, propertyName)`；
- 保留现有 name-only manifest，它仍用于验证 wire net-id 布局；另加 carrier-cache coverage 测试。

## 3. 玩家选择协议与提交事务

### C1. [P1][高信心] 最终发送失败后，多条调用链仍提交本地结果

发送助手：

- `src/Selection/Sync/HextechRuneSelectionCoordinator.RemoteChoices.cs:105-140`

忽略最终失败的调用方：

- ActRoll：`src/Selection/Coordinator/HextechRuneSelectionCoordinator.ActRoll.cs:129-136`
- RuneChoice：`src/Selection/Coordinator/HextechRuneSelectionCoordinator.Selection.cs:53-68,108-128`
- Forge：`src/Selection/Coordinator/HextechForgeSelectionCoordinator.cs:66-92`
- RelicOption：`src/Selection/Coordinator/HextechRelicOptionSelectionCoordinator.cs:56-83`
- RandomRuneGrant：`src/Helpers/HextechRuneGrantHelper.cs:62-72`
- EnemyHexAdjustment：`src/Selection/EnemyAdjust/HextechRuneSelectionCoordinator.EnemyHexSync.cs:92-130`
- ActSelectionApplied：`src/Selection/Sync/HextechRuneSelectionCoordinator.MultiplayerAck.cs:17-34`

`TrySyncLocalHextechChoice` 第二次发送失败只返回 `false`，调用方仍会：

- 返回主机 act roll；
- 返回本地符文/锻造/遗物选择，供外层继续发放；
- 直接执行随机符文获得；
- 继续 ack barrier；
- 把敌方海克斯调整当作仅日志失败。

远端没有对应消息：无超时调用会永久等待，有轮询调用会在连接仍有效时持续等待。

最小修复：

- 发送成功必须成为任何共享状态提交的前置条件；
- 将可被忽略的 `bool` 接口改为强契约，例如成功返回 sent id，失败抛专用协议异常；
- 失败后统一 fail-closed 或断连，不能让本地继续获得内容；
- 对“第一次 stale id、第二次成功”建立明确的两端 choice-id 对齐策略，不能仅靠接收端跨 id 猜包。

测试：

- fake synchronizer 连续抛两次时，不得调用 Obtain 或落地 act 配置；
- 第一次失败、换 id 后成功，下一次原版和模组选择仍应使用相同 choice id；
- enemy adjustment 最终包失败时不得标记本幕完成。

### C2. [P1][高信心] 远端等待没有完整取消协议

位置：

- 永久等待入口：`src/Selection/Sync/HextechRuneSelectionCoordinator.RemoteChoices.cs:24-48`
- 事件 TCS：同文件 `151-220`
- 并发选择：`src/Selection/Sync/HextechRuneSelectionCoordinator.Multiplayer.cs:111-163`
- ack 中断竞速：`src/Selection/Sync/HextechRuneSelectionCoordinator.MultiplayerAck.cs:46-60`
- UI 取消源：`src/Selection/UI/HextechRuneSelectionScreen.Core.cs:135-147`、`MapPreview.cs:74-91`

问题由三部分组成：

1. `WaitForRemoteHextechChoice` 明确传入 `timeoutFrames: null`；
2. 事件等待没有 `CancellationToken`；
3. 本地关闭选择界面不会发送一个远端可识别的 canceled 终态。

多人选择使用 `Task.WhenAll`。任一本地任务取消或抛错时，`Task.WhenAll` 仍会等待其他尚未完成的远端任务。ack 路径虽然监听换跑局/断线，但 CTS 只取消中断观察任务，没有传给实际 ack 等待。

最小修复：

- 从批次入口创建 linked CTS，绑定 run 变更、断连、界面关闭和显式操作取消；
- 所有事件等待接受 token，并在 `finally` 取消/注销；
- 本地取消要广播确定的 `Canceled` payload，或让整批以同一 fail-closed 结果结束；
- 任一子选择失败时取消并 await 其余子任务，禁止遗留监听器。

测试：

- 关闭本地选择屏幕后所有端任务均完成；
- disconnect/run change 后订阅数归零；
- run A 的旧 listener 不能消费 run B 的包；
- 任一 `Task.WhenAll` 子任务失败时，其余等待都被取消并观察。

### C3. [P1][高信心] 多人符文发放存在非幂等的部分提交窗口

位置：

- `src/Selection/Sync/HextechRuneSelectionCoordinator.Multiplayer.cs:134-154`
- `src/Selection/Coordinator/HextechRuneSelectionCoordinator.Core.cs:199-226`

流程先收齐所有选择，然后逐玩家：

`await RelicCmd.Obtain(selectedRelic, player)`

全部成功后才 ack，并在更外层执行 `SetActResolved(true)`。原版 `RelicCmd.Obtain` 会先把遗物加入玩家背包，再 await `AfterObtained`。

若玩家 1 已获得成功，玩家 2 的 `AfterObtained` 抛错：

- 玩家 1 已经实际获得符文；
- 本幕仍保持未解析；
- 外层 catch 允许 room-enter/load 时重试；
- 重试会再次为玩家 1 发放。

若异常只发生在一端，还会直接形成背包分叉。现有注释把“保持未解析并重试”描述为可自愈，但缺少逐玩家 applied journal 时并不具备幂等性。

最小修复：

- 为 `(actIndex, choiceOrdinal, playerNetId)` 保存同步、可持久化的已应用标记；
- 重试时只补未提交玩家；
- applied 标记与实际 Obtain 必须定义失败恢复语义；
- 在 journal 完成前，部分提交后的异常不应继续走普通“可自愈重试”。

测试：

- 注入第二位玩家 `AfterObtained` 失败，重试不得重复发放第一位玩家；
- 保存/加载、断线重连后只补未应用玩家；
- 对同一 operation 重复投递应保持 exactly-once 结果。

### C4. [P2][高信心] singleton 等待不是按帧等待，异常 fallback 又不对称

位置：

- `src/Selection/HextechSelectionHelpers.cs:67-82`
- synchronizer 调用：ActRoll、多人主选择、Forge、RelicOption
- UI 调用：`HextechRuneSelectionCoordinator.Selection.cs:156-173`、`HextechForgeSelectionCoordinator.cs:124-140`

`Task.Yield()` 只把 continuation 重新排队，不等于 Godot `ProcessFrame`；60 次可能在同一渲染帧内耗尽。

需要区分两个对象：

- `NOverlayStack` 确实可能受场景/UI 生命周期影响，当前等待过早耗尽后本地会抛异常，而远端已经开始等消息。
- `PlayerChoiceSynchronizer` 在两个目标版本的正常 `InitializeShared` 中同步创建；它为 null 应视为异常生命周期或过期任务。此时更不应走本机落地 fallback。

当前异常 fallback 包括：

- ActRoll：主机写本地禁用配置，客户端写空禁用集；
- Forge/RelicOption：本地端开 UI，远端端取第一项；
- 主符文选择：退回未使用当前自定义协议的选择路径。

最小修复：

- UI singleton 使用真实 process-frame/墙钟等待；
- 网络服务不要复用 UI 等待助手；
- 联机中 synchronizer 缺失时中止当前 transaction，不提交共享状态；
- 所有端必须得到同一终态，禁止“本地选择/远端默认第一项”。

### C5. [P2][高信心] payload 缺少操作相关性，迟到包可被下一次操作消费

主要位置：

- Forge 判定：`src/Selection/Sync/HextechChoiceCodec.cs:528-539`
- Forge 等待：`src/Selection/Coordinator/HextechForgeSelectionCoordinator.cs:95-105`
- 跨 choice-id 接收：`src/Selection/Sync/HextechRuneSelectionCoordinator.RemoteChoices.cs:160-183,256-297`
- Enemy adjustment：`src/Selection/EnemyAdjust/HextechRuneSelectionCoordinator.EnemyHexSync.cs:133-175`
- RandomRuneGrant：`src/Selection/Sync/HextechChoiceCodec.cs:474-520`

Forge 的 `IsForgeSelection` 只判断“这是可解码的 Forge 包”，不校验当前 options 或 context。等待助手又会接受任意 choice-id 下满足 predicate 的缓存/迟到包。接收端还会按 payload 自带的旧 option IDs 重建选项。

同类缺口：

- RandomRuneGrant 没有 operation ordinal/context；
- Enemy adjustment 只校验 act，不要求收到的 `Sequence` 等于当前期望序号，旧包可把界面状态回滚；
- RelicOption 虽校验完整 options，但 options 相同时仍没有 operation nonce。

最小修复：

- Forge 先补与 RelicOption 同级的 exact expected-options 校验；
- 所有 payload 加入确定性的 operation id/context hash/ordinal；
- Enemy adjustment 要求 exact sequence，重复包幂等忽略，旧序号拒绝；
- choice id 仍应是第一相关键，跨 id 恢复只能接受当前 transaction 显式登记过的 retry id。

### C6. [P2][高信心] codec 边界算术与编码/解码契约不对称

位置：

- `src/Selection/Sync/HextechChoiceCodec.cs:393-455,486-516,547-584,696-747`
- `src/Selection/Sync/HextechStableModelIdListCodec.cs:9-85`
- 公共入口：`src/Api/HextechRunesApi.cs:116-129`

问题：

- 多处使用 `payload.Count < cursor + count`，极大 count 可使加法溢出；
- Enemy adjustment 的 `hexCount=int.MaxValue` 在长度检查溢出后可尝试创建超大 List；
- stable ModelId decoder 没有先拒绝负 cursor；
- encoder 不限制 count/字符串长度，decoder 却限制 `count <= 64`、长度 `<= 128`；
- API 可接受 65 个选项，本端编码成功，远端必定拒绝。

最小修复：

- 所有 count 在任何算术/分配前检查非负和协议上限；
- 使用 `count > payload.Count - cursor` 形式避免溢出；
- encoder 与 decoder 共用同一常量与验证函数；
- API 在显示 UI/发送前拒绝不可能 round-trip 的输入。

测试：

- 每个 count 字段喂入负数、上限、上限加一、`int.MaxValue`，只允许返回 false，不得抛错或大分配；
- 64/65 options、128/129 字符边界；
- 所有 encoder 输出都必须能被对应 decoder round-trip。

## 4. 战斗异步与失败原子性

### A1. [P1][高信心] 最大生命换算使用进程全局异步守卫

位置：

- `src/Hooks/Combat/HextechCombatHooks.State.cs:5`
- `src/Hooks/Combat/HextechCombatHooks.MaxHp.cs:5-121`
- 安装：`src/Hooks/Combat/HextechCombatHooks.Install.cs:79-97`

三个 prefix 在改写 `BaseMaxHp` 与实际参数后设置 `_handlingGoliathMaxHp = true`，直到原版异步 Task 完成才清零。

反编译确认：

- `GainMaxHp` 内部会 await `SetMaxHp`，随后还会 await `Heal`；
- `LoseMaxHp` 可能先 await `Damage`，再 await `SetMaxHp`。

因此该全局 bool 会跨多个 await 长时间保持。此时另一玩家或另一执行流发起最大生命命令，会直接跳过 BaseMaxHp 更新和系数换算。结果可能是实际 MaxHp 与 SavedProperty 中的 BaseMaxHp 分离，后续重算/读档再次改变数值；两端异步时序不同还可能造成 checksum 分叉。

最小修复：

- 使用工程内现成的 `HextechScopedDepthGuard`；
- prefix 以 `IsActive/Enter` 只阻断同一异步调用链的嵌套 `SetMaxHp`；
- postfix 用 `WrapEnteredTask`，立即解除调用者上下文，同时让原 Task 的异步续体保持守卫。

测试：

- 用 TCS 挂住第一条命令；
- 确认其内部嵌套 Set 被抑制；
- 同时启动的第二条独立命令仍经过 BaseMaxHp 换算；
- 双人同时获得/失去最大生命，保存重载后核对 BaseMaxHp、MaxHp 与 checksum。

### A2. [P2][高信心] “我们的治疗”全局深度会漏掉重叠治疗

位置：

- `src/Runes/OurHealingRune.cs:10,17-66`
- `src/Hooks/Combat/HextechCombatHooks.Healing.cs:72-121`

`_mirrorDepth` 能阻止双持互相回血的递归，但首个镜像 `CreatureCmd.Heal` 尚未完成时，任何无关治疗流都会看到 `_mirrorDepth > 0`，从而静默跳过合法镜像。

多人同时治疗或第三方模组并行治疗时，漏掉哪次取决于本地调度与表现耗时，影响 CurrentHp 及后续治疗 hook。

最小修复：

- 替换为 `HextechScopedDepthGuard`；
- 入口检查 `IsActive`，镜像链使用 `RunAsync`；
- 只阻止同一异步调用链递归，不压制独立治疗。

### A3. [P2][高信心] 玩家濒死狂宴失败时账本不回滚

位置：

- `src/Runes/NearDeathFeastRune.cs:131-140,175-178,256-275`
- 入口：`src/Hooks/Combat/HextechCombatHooks.NearDeathFeast.cs:65-106`

`SyncNearDeathStrength` 在 `Flash()` 和 `await PowerCmd.Apply<StrengthPower>` 之前先写：

`_nearDeathStrengthBonus = desiredBonus`

若 Flash 事件或 Apply 在单端失败，账本已经前移，下一次因 `delta <= 0` 不再补发。旧 F52 只把裸任务换成 `TaskHelper.RunSafely`；该 wrapper 会记录并重新抛异常，但返回 Task 又被 `_ =` 丢弃，结算链不会回滚或停止。旧 F75 修的是敌方版本，玩家版本仍有残留。

敌方实现 `HextechEnemyNearDeath.cs:205-254` 已有 `SemaphoreSlim + 预留账本 + 条件回滚`，可作为正确模式。

最小修复：

- 玩家实例增加串行门控；
- 门内保存旧账本并预留新值；
- Flash 与 PowerCmd.Apply 全部放进事务 try；
- 失败时仅在账本仍等于本次预留值时回滚，再重新抛出。

测试：

- 首次 Apply/Flash 故障后账本回滚；
- 第二次成功补足完整差额且只应用一次；
- 两条重叠更新严格串行。

### A4. [P2][高信心] Goldrend 战后扣金与广播不是原子事务

位置：

- `src/Runes/HextechGoldrendSync.cs:41-70`
- 调用：`src/Mayhem/HextechMayhem.CombatLifecycle.cs:30-38`

当前顺序：

1. 复制 pending；
2. 立即清空整个 pending；
3. 本地 `PlayerCmd.LoseGold`；
4. `RewardSynchronizer.SyncLocalGoldLost`。

若第 4 步发送失败，本地金币和历史已经改变，远端没有收到，pending 又已清空。当前只检查 `NetGameType`，没有检查连接状态。

仅把 `Clear` 后移或交换“先发/先扣”仍会留下另一侧部分提交。完整解决需要明确 operation 状态。

建议：

- pending 至少记录 `Pending/LocalApplied` 阶段，避免重试时重复扣本地；
- 发送前检查连接，异常时保留可诊断状态并记录 `[DESYNC-RISK]`；
- 更完整方案是带 operation id、ack 和远端去重的专用消息；
- 换跑局、弃局、加载路径显式清理旧 pending。

测试：

- 未连接、发送抛错、重复投递三种故障注入；
- 断线重连后金币与历史恰好修改一次；
- 弃局后新跑局不能继承旧 pending。

## 5. 确定性与版本门禁

### D1. [P2][高信心] ColorDiscovery 的稳定随机盐仍依赖候选枚举顺序

位置：

- `src/Runes/ColorDiscoveryRune.cs:83-124`
- 对照 helper：`src/Relics/Base/HextechRelicBase.CardGeneration.cs:7-22`

`HextechStableRandom.Pick` 内部会按卡牌 key 排序候选，但调用方又把原始顺序的 `CardPileKey(candidates)` 写入盐。相同候选集合只要来自 CardPool/第三方注入的枚举顺序不同，最终 hash 仍不同。

最小修复：

- `GroupBy(...).Select(...)` 后按 `HextechStableRandom.CardKey` 做 ordinal 排序；或
- 增加只用于逻辑无序集合的 `CardSetKey`，内部排序后编码。

不要全局改变 `CardPileKey`，手牌、抽牌堆等真实牌堆的顺序属于游戏状态。

测试：同一候选集合正序/逆序输入必须得到相同三项结果，同时真实牌堆 key 仍应区分顺序。

### D2. [P2/策略决策] 同版本号不同构建仍可加入同一房间

位置：

- `src/Hooks/Compat/HextechMultiplayerCompatibilityHooks.cs:50-102`
- SavedProperty 异常 finalizer：同文件 `104-160`
- 诊断签名：同文件 `193-278`

当前兼容条目故意只返回 `modId-version`。DLL/PCK/manifest/SavedProperty 签名只写日志，不参与握手；finalizer 也只识别 SavedProperty 协议异常。纯逻辑、choice codec 或 Harmony 差异不会在入房时被拦截。

这是已有的可读性取舍，不建议直接恢复一长串不可读 hash。更稳妥的折中：

- 增加短、可读、人工维护的 `NetworkProtocolVersion`，例如 `HextechRunes-0.8.x-net7`；
- SavedProperty、payload schema、联机选择事务、战斗确定性规则变化时必须 bump；
- CI 检查 gameplay DLL 发生变化但 visible/net version 都未变的提交。

这是发布策略加固，不应伪装成已发生的运行时 bug。

### D3. [P3][中高信心] 外部符文配置只按 `ModelId.Entry` 排序和保存

位置：

- `src/Selection/Sync/HextechChoiceCodec.cs:141-199,366-369`
- `src/Content/HextechCatalog.cs:150-155`
- `src/Config/HextechRunConfigurationSnapshot.cs`

外部模组可注册自己的符文，但配置 ordinal 只按 `id.Entry` 排序，禁用集合也只保存 Entry。两个不同 ModelId category 使用相同 Entry 时：

- 等值排序后的顺序可能受 HashSet 枚举顺序影响；
- 一个配置键无法区分两者；
- 同一个 bit 可能在两端映射到不同外部符文。

建议长期迁移到完整 ModelId；短期至少拒绝跨 category 的重复 Entry，并在启动时明确报错。

## 6. 已核验为安全或不建议立项的路径

- 敌方濒死狂宴已有串行门控和失败回滚，不应继续按旧 F75 原思路修改。
- `NatureIsHealingRune` 与敌方版本的 timer 只用于单机；联机使用可等待的回合钩子。
- RuneSelection payload 已绑定 `actIndex + choiceOrdinal`。
- RelicOption 已校验当前完整 options；其残余问题是 options 相同的两次 operation 缺少 nonce。
- combat tracking 的 Dictionary/HashSet 序列化会做 ordinal 排序。
- 玩家配置、run configuration 和已见符文保存路径已有稳定排序。
- 当前万用瞄准镜概率加算改动没有新增 ModelId、SavedProperty 或网络消息，使用既有同步遗物状态；本轮没有改动它。
- `HextechStableRandom` 中整数 `.ToString()` 可统一改为 invariant，但当前进入盐的动态值主要是非负整数和 ID，没有证据支持将其列为 P1/P2。
- 原始 `RunState.Rng.Niche` 的符文初选/普通 reroll 仅在单机分支；多人分支使用稳定随机并同步最终选择。
- 不建议恢复旧审查已否决的 F0/F4/F28/F37 修改方案；F11/F48/F53/F65 仍只应采用旧文档中的降级后动作。

### 与 2026-07-24 审查的关系

- S1 是 0.109 原版缓存状态机的新证据；旧 F83 只处理注册顺序造成的半注册状态。
- C1 覆盖 PlayerChoiceSynchronizer；旧 F14 只处理 DoubleVision 的 RewardSynchronizer。
- C2 覆盖通用选择取消；旧 F63 只处理敌方海克斯调整的固定 10 分钟等待。
- C4 是旧 F72 抽取 helper 后暴露出的语义问题，旧文档没有判断 `Task.Yield` 是否等于 process frame。
- C5 是消息相关性问题；旧 F66 只处理 malformed/越界降级，F68 只处理重复协调器。
- D2 与旧 F69 的 codec 版本假设有关，但这里审查的是入房门禁本身，因此列为策略残余而不是重复问题。

## 7. 建议实施批次

### 批次 1：序列化注册状态机

- 修 S1；
- 同步修 S2；
- 覆盖 0.107.1/0.109.0 分支测试；
- 使用 SponsorPack 做实际注册顺序测试。

这是最高优先级，因为它位于联机序列化基础层，并且 0.109 的当前注释与原版真实行为相反。

### 批次 2：选择事务与取消

- 修 C1、C2、C3、C4；
- 统一 transaction outcome：Committed / Canceled / Aborted；
- 增加 linked cancellation、exactly-once applied journal；
- 禁止同步失败后的本地继续提交。

### 批次 3：消息相关性与 codec 防御

- 修 C5、C6；
- 给 payload 增加协议版本和 operation id；
- 加边界/乱序/重复/迟到包测试；
- 再决定是否同步推进 D2 的 net protocol version。

### 批次 4：战斗异步状态

- 修 A1、A2、A3、A4；
- 优先复用 `HextechScopedDepthGuard`；
- 对先记账后 await 的逻辑统一采用串行门控和条件回滚。

### 批次 5：确定性与扩展兼容

- 修 D1；
- 决策 D2；
- 规划 D3 的配置迁移或冲突拒绝策略；
- 删除未使用的原始 Rng forge overload，防止未来误接到联机路径。

## 8. 验证矩阵

### 每批基础门禁

```bash
bash tools/run_tests.sh
```

要求：

- 0.107.1 与 0.109.0 两目标测试全部通过；
- 两目标 build 均为 0 error；
- 不新增 warning；
- loader 和多版本 bundle 校验继续通过。

本轮实际执行结果：

- 0.107.1：build `0 warning / 0 error`，`137/137` tests passed；
- 0.109.0：build `0 warning / 0 error`，`137/137` tests passed；
- loader：build `0 warning / 0 error`；
- bundle validator：`loader + 2 variants`，目标 `0.107.1, 0.109.0` 验证通过。

这些结果只证明当前工作树通过现有门禁；本文指出的故障注入测试尚未实现，因此不能据此判定问题已经修复。

### 必须新增的故障注入

1. 0.109 pre-Init 外部 SavedProperty 注册顺序置换。
2. PlayerChoice 连续两次发送失败。
3. 本地选择窗口关闭、换跑局、断线。
4. 迟到 Forge/RandomGrant/EnemyAdjustment 包与乱序 sequence。
5. 第二位玩家 `AfterObtained` 抛错后的重试和存档恢复。
6. 两条重叠最大生命命令。
7. 两条独立治疗与一条递归镜像治疗重叠。
8. NearDeath 的 Flash/Power Apply 单侧故障。
9. Goldrend 发送失败与重复投递。
10. codec 极值、边界和 round-trip。
11. ColorDiscovery 候选顺序置换。

### 双客户端实机验证

分别对 0.107.1 与 0.109.0 做：

- 新开双人局、进幕选择、关闭/重开地图；
- 选择期间断线，验证双方都结束 transaction，不出现单端继续；
- 重连/读档后只补未提交玩家，不重复发放；
- 双人同时获得最大生命、同时治疗；
- 战斗结束瞬间断线并触发 Goldrend；
- 保存、退出、重载后核对背包、金币、MaxHp/BaseMaxHp、SavedProperty 签名和 checksum；
- 主模组 + SponsorPack，改变非 gameplay 模组加载顺序，确认 SavedProperty net-id 与官方 Hash 仍一致。

静态测试、编译和 IL 反编译不能替代这组双客户端验证。

## 9. 本轮边界

- 本轮只新增本文档，没有修改模组源码、资源、版本号或发布产物。
- 没有提交、推送、部署、打包或上传创意工坊。
- 工作树中原有的 `UniversalScopeRune.cs` 与 `tests/HextechRunes.Tests/Program.cs` 改动应继续保持原样。
- 本文结论来自源码、调用链、双版本反编译与现有测试；没有声称已完成玩家侧复现。
