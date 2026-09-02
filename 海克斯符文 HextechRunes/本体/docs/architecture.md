# HextechRunes 架构重构路线

本文件记录结构重构的目标边界。当前阶段先完成目录分层和首批服务抽取，不改变任何游戏行为、模型 ID、随机算法或联机协议语义。

## 依赖方向

长期目标是让业务逻辑沿单向依赖流动：

```text
Platform/Hooks/Config/Localization/Telemetry
  -> Mayhem
  -> Selection
  -> Core/Catalog/Runes/EnemyHexes
```

多人同步和随机数属于横切能力，所有会影响联机一致性的选择结果都必须通过明确 payload 或稳定随机输入表达，不允许依赖“各端本地池子刚好一样”。

## 补丁层（2026-09 重构后）

所有 Harmony 补丁都是自描述的嵌套静态类，入口 `ModEntry.Initialize` 只做编排，不再有安装顺序契约：

- 声明：`[HarmonyPatch(...)]` + `[HextechPatch(id, feature, Rune=/Runes=/Optional=)]`，由 `src/Patching/HextechPatcher.ApplyAll` 统一应用，逐条成败可见；`Optional=true` 的目标缺失只记 Info。目标需要运行时枚举时，类只带 `[HextechPatch]` 并声明 `static void Apply(Harmony)`。
- 同一目标的执行序只由 `[HarmonyPriority]` / `[HarmonyAfter]` 决定，禁止依赖安装顺序。
- 符文专属补丁内嵌在符文自己的文件里（如 `SurvivorUpgradeRune` 里的 `SurvivorPatch`）；`src/Hooks/**` 只放跨符文的横切补丁（战斗、UI、资源、商店、运行生命周期）与共享辅助。
- 版本差异写成整文件 `#if` 的分部文件（`HextechSavedPropertyBootstrap.Legacy.cs` / `.Official.cs`），共享代码里不写 `#if`；剩余的 `#if` 只允许出现在原版虚方法签名随版本变化的覆写处。
- 私有成员访问集中经 `HextechHookReflection`，缺失成员会在启动摘要里列出。

三道护栏（`tests/HextechRunes.Tests/`，三编译目标各一份，用 `HEXTECH_WRITE_PATCH_MANIFEST=1` 重生成）：`patch_manifest.<target>.txt` 冻结补丁目标与优先级；`static_state_manifest.<target>.txt` 冻结可变静态字段清单；`tests/vanilla_copy_guard.0.111.0.txt` 冻结所有可跳过原方法的 bool 前缀目标的 IL 哈希，游戏更新后 headless 日志出现 `[VanillaCopyGuard] DRIFT` 即需复核。

## 当前 Selection 分层

`src/Selection` 已按职责拆分为以下目录：

- `Coordinator/`：选择流程编排。负责何时弹界面、何时等待远端、何时落地奖励。这里可以调用其它 selection 服务，但不应继续堆具体池生成和同步编解码细节。
- `Pool/`：玩家海克斯池生成、过滤、标签权重、幕数限制、配置过滤边界。`HextechRunePoolBuilder` 是当前池构建入口，Coordinator 只保留兼容门面和流程侧调用点。
- `Reroll/`：玩家海克斯重随机制。所有重随逻辑必须保持本地 UI 随机不推进共享 run RNG，联机路径要保持可重放或同步最终选项。
- `Sync/`：多人选择 payload、远端等待、选择确认、ack 等同步边界。只负责“传什么、如何还原”，不负责具体 UI。
- `EnemyAdjust/`：选择界面里的敌方海克斯重随/移除同步。
- `AI/`：AI 队友或主机代选逻辑。
- `UI/`：`HextechRuneSelectionScreen` 的渲染、交互、hover、音效、布局。

## Source of truth 方向

后续重构应收敛到单一内容元数据源：

- 符文 ID、稀有度、角色池、标签、默认禁用、是否进入图鉴、是否进入抽选池都应从同一份 catalog metadata 派生。
- 本地化、配置界面、统计中文名、图鉴可见性不应各自维护重复名单。
- 在正式切换前，应保留旧 registry 与新 metadata 的双读对比，确认输出一致后再删除旧路径。

## 高风险规则

- 不在结构重构中顺手改平衡、文案或触发时机。
- 不改变模型注册顺序，除非明确重打版本并接受联机 hash 改变。
- 不改变 `PlayerChoiceResult` 的既有语义；如需扩展 payload，必须兼容旧 payload 或提供明确 fallback。
- 多人池过滤不能只同步 index；只要各客户端候选池可能不同，就必须同步最终选项 ID。
- 每次结构重构后至少执行内容校验、Release 编译、部署脚本和 headless 加载验证。
