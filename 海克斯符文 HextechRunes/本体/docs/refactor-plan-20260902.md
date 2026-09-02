# HextechRunes 架构重构方案（2026-09-02）

> 取证基线：根仓库 `dev` 分支 751777d4，模组版本 0.9.1，编译目标 0.107.1 / 0.110.0 / 0.111.0。
> 参考对象：本机 Workshop 已安装的 STS2-RitsuLib 0.5.18（3747602295）、PengoTarot v1.4.16（3747679239）、figure_Saya 1.8.5（3747508952）、MultiEnchantmentMod v2.5.4（3747561525），全部反编译后逐项核对。
> 原版 API 以 `versioned-dll-backups/{0.107.1,0.109.0,0.111.0}/game-refs/sts2.dll` 为准，用 `tools/sts2-inspect` 核验。
>
> 本文只做诊断与方案，不改代码。所有"官方 Hook 可替代"结论都标注了核验方式；标"待核验"的项在动手前必须先反编译确认。§8 的三项原为待裁决，已于同日核对定案。

## 0. 结论

1. **入口与补丁体系是最大的病灶。** `ModEntry.Initialize` 顺序安装 48 个 Hook 组、199 个手工 `harmony.Patch`，没有任何一个补丁带 ID/关键性/描述元数据，安装顺序被写成"契约"，注释与现实已脱节（17 处 `Priority` 实际在决定执行序）。社区四个参考模组无一例外都是"单入口只做编排 + 补丁自描述 + 失败逐条可见"。
2. **约 4 成补丁打在原版本来就给了扩展点的地方。** 海克斯的核心状态载体 `HextechMayhemModifier` 本身就是 `ModifierModel`，符文本身就是 `RelicModel`，两者天然收到原版 `Hook.*` 全部回调。`CardModel.CanPlay` 内部就调 `Hook.ShouldPlay`，`RelicModel.Icon` 直接读虚属性 `PackedIconPath`，`RunManager.OnEnded` 对应的官方口子是 `ModManager.OnMetricsUpload`。这些地方仍在用 Harmony 硬切。
3. **五处补丁打在全游戏共享的中枢上，是与其他模组冲突的主要来源**：`Hook.BeforeCardPlayed/AfterCardPlayed/AfterCardChangedPiles/ModifyCardPlayCount/ModifyCardPlayResultLocation` 的 `Priority.First` 前缀（形态自动打出批处理）、`Log.Warn` 前缀（压一条假警告）、`NetHost/NetClientGameService.OnPacketReceived` 终结器、`ModManager.GetGameplayRelevantModNameList` 后缀（重写联机模组清单条目）、`OneTimeInitialization.ExecuteEssential` 后缀（重排 SavedProperty net-id 表）。
4. **假警告的根因在 loader，不在游戏。** `LoaderBootstrap.RegisterVariantAssembly` 无条件安装 `ReflectionHelper.ModTypes` 后缀，同时又调用 0.108+ 的 `AssociateAssemblyWithMod`，同一批类型被贡献两次，于是原版报"Two AbstractModels X and X share an ID"，海克斯再用一个全局 `Log.Warn` 补丁把它压掉。修 loader 的三级回退（只走一条路）即可同时删掉两个补丁。
5. **版本兼容层失控。** csproj 有 10 个编译目标、代码里 133 处 `#if`，但实际只发布 3 个变体。RitsuLib 在 49 万行反编译代码里没有一处 `#if`，差异全部收口在 `Compat/` 目录按分支编译的同形类里。
6. **联机校验相关改动大部分可以退役。** 0.109 起原版自己做 SavedProperty 确定性排序与哈希，海克斯的 net-id 规范化在 0.109+ 已是空转；模组清单条目重写可以用 manifest 版本号表达；包接收终结器只对 0.107.1 有意义。留一个"入房失败诊断"就够了，RitsuLib 就是这么做的。

规模上，这轮重构预计能把 `src/Hooks` 的 2 万行压到 1 万行以内、补丁数从 199 降到 120 左右、`#if` 从 133 降到 30 以内，而且**不改任何模型 ID、平衡数值、随机算法与 wire 协议**，联机 hash 与存档兼容不受影响。

## 1. 现状取证

| 指标 | 数值 | 说明 |
|---|---|---|
| 源码规模 | 807 文件 / 83,442 行（含 5,769 行测试主文件） | `src/Runes` 20,800 行、`src/Hooks` 20,536 行 |
| 手工 `harmony.Patch` | 199 | 属性式 `[HarmonyPatch]` 0，`PatchAll` 0 |
| 补丁形态 | prefix 88 / postfix 79 / prefix+postfix 25 / finalizer 5 | prefix 占比 57%，参考模组 Postfix 通常占 6~7 成 |
| 显式 `Priority` | 17 | 集中在 RelicCmd.Obtain、Heal、CanPlay、EndTurn、Hook.* |
| 同一原版方法被多处 patch | 12 组 | `NCombatRoom._Ready` ×6、`NCombatRoom.AddCreature` ×6、`NCreature._Ready` ×6、`RelicCmd.Obtain` ×3、`CardModel.FromSerializable` ×3 …… |
| 原始反射 `GetField/GetMethod/GetProperty` | 51 处 | `BindingFlags.NonPublic` 114 处，触及约 40 个原版类型的私有成员 |
| `#if` / `#elif` | 133 处 | 10 个 `STS2_*` 符号，只发布 3 个变体 |
| 非 `readonly` 静态字段 | ≈376 | 进程级可变状态，联机分叉的温床（见 0726 联机审查 A1/A2） |
| 入口安装组 | 48 | 依次调用，顺序即"契约" |

补丁分布最重的原版类型：`CardModel` 13、`NCombatRoom` 12、`NCreature` 10、`NCombatUi` 6、`Hook` 6、`CardPileCmd` 4、`Shiv` 4。

## 2. 社区标准做法对照

| 维度 | 海克斯现状 | 社区标准（出处） |
|---|---|---|
| 入口 | 48 组顺序 Install，注释宣称"顺序即契约" | `Initialize()` 只编排，10~30 行；工作拆到独立类（Pengo / MEM / Saya / RitsuLib 四者一致） |
| 补丁声明 | 手工 `harmony.Patch` + 字符串方法名 + `RequireMethod` | 属性式 `[HarmonyPatch]` + `PatchAll`（Pengo 138 / MEM 107 / Saya 96 个补丁类）；或 RitsuLib 的 `IPatchMethod`（`PatchId` / `IsCritical` / `GetTargets()`）+ `ModPatcher.PatchAll()` 逐条报告、critical 失败整体回滚 |
| 补丁失败 | `TryInstallOptionalHookGroup` 吞掉整组，Warn 一行 | 每个补丁独立成败，`ignoreIfTargetMissing` 降级为 Info（RitsuLib）；`ValidateReflectionTargets` 启动 fail-fast（MEM） |
| 优先级 | 17 处零散，注释与实况矛盾 | 聚合类补丁一律 `Priority.Low` 让别人先算（MEM 38 处）；`[HarmonyAfter("BaseLib")]` 显式声明与竞品的先后（RitsuLib 52 处） |
| 冲突可见性 | 无 | `LogHarmonyPatchConflicts()`：启动时列出与其他 owner 共享的补丁点（MEM） |
| prefix 覆盖原方法 | 30 个 Hooks 文件含 `return false`，无守卫 | RitsuLib 无条件覆盖仅 17/600；MEM 26 处全部登记进 `VanillaCopyGuard`，启动时比对原版方法 IL 的 SHA1，漂移即告警 |
| 私有成员 | `HextechHookReflection` 返回 null + 各文件自持 `FieldInfo` | `PrivateAccess`：找不到就抛 `MissingFieldException`，优先返回 `FieldRef`/委托而非 `Invoke`（RitsuLib）；`[UnsafeAccessor]` 零反射（Pengo）；`IgnoresAccessChecksTo("sts2")` publicizer（MEM） |
| 版本差异 | 133 处 `#if` 散落全仓库 | 0 处 `#if`，差异收口在 `Compat/` 同形类，一版本一 DLL（RitsuLib）；或单常量 `CompatVersion`（Pengo） |
| 内容注册 | 手工 `ModHelper.AddModelToPool` + 读私有 `_moddedContentForPools` 去重 + `ModelDb.Init` 后缀移动端 workaround | 只用公开 `ModHelper.AddModelToPool`，其余靠 ModelDb 自动发现；自定义池用 `ModelDb.get_AllCardPools` 后缀并入（Pengo / Saya） |
| 二段初始化 | 各 UI hook 自行 30 次重试 attach | `Callable.From(...).CallDeferred()` 轮询 `ModManager.State == Initialized`，上限 300（Pengo / MEM 逐行相同） |
| 跨模组 | 硬编码 `Natsuki.EndlessMode` Harmony id 做 `after`；专门 patch 无尽/集成战略/Artifact/Entomancer | 软依赖桥：`Lazy<Func<...>>` + `Delegate.CreateDelegate`，找不到就当没有（Pengo `MultiEnchantmentHelper` 85 行） |
| 联机校验 | 重写模组清单条目、重排 net-id 表、包接收终结器断连、`Log.Warn` 压警告 | 不改校验；把自有数据以 `[SavedProperty]` 载体混进原版通道（MEM）；net-id 排序后向原版 XxHash 追加字节保证双方 hash 一致（RitsuLib）；入房失败时**交换诊断 payload 并给出建议**而不是改判定（RitsuLib JoinDiagnostics） |
| 每卡持久数据 | `CardModel.ToSerializable/FromSerializable` 各 2~3 处 postfix 抢写 | 一个从不实例化的载体类声明 `[SavedProperty]`，数据塞进 `SerializableCard.Props`，天然过存档 + 网络 + 校验（MEM） |
| 资源 | 11 个 patch 拦 `RelicModel.Icon/IconOutline/BigIcon`、`PowerModel.Icon`、`CardModel.Portrait`… | PCK 内 `res://` + 覆写虚属性路径；原版 `RelicModel.Icon => ResourceLoader.Load(PackedIconPath)`，`PackedIconPath` 是 virtual（0.111 反编译第 153/165 行） |
| 配置 UI | 3,382 行手写 Godot 节点树，戳 `NMainMenu._lastHitButton` 等私有字段 | PCK 场景 + `[ScriptPath]` Node 类 + `[assembly: AssemblyHasScripts]`（Pengo `NConfigFloatingWindow`、MEM 主类本身是 Node） |
| 运行结束遥测 | patch `RunManager.OnEnded` | `ModManager.OnMetricsUpload(SerializableRun, bool isVictory, ulong localPlayerId)` 官方事件 |

## 3. 问题清单（按冲突面排序）

### P1 入口顺序契约与无元数据补丁
`src/ModEntry.cs` 48 行 Install；`src/Hooks/**` 199 个 `harmony.Patch`。没有补丁 ID，日志里出问题只能靠 label 字符串猜；`TryInstallOptionalHookGroup` 一个组里任意一个目标找不到就整组跳过，而组内其他补丁其实可以独立成立。

### P2 打在全游戏中枢上的补丁
- `Hook.*` 五个静态分发方法的 `Priority.First` prefix（`HextechFormAutoPlayHooks`）：所有模组的所有模型回调都经过这里，海克斯在"批处理窗口"内吞掉 `Before/AfterCardPlayed`，别的模组的出牌计数会被静默漏掉。
- `Log.Warn` prefix（`HextechModelIdSerializationWarningHooks`）：拦全游戏日志只为压一条假警告，任何写日志的模组都被多走一遍字符串比较。
- `NetHostGameService/NetClientGameService.OnPacketReceived` finalizer：包接收链上吞异常并主动断连。
- `ModManager.GetGameplayRelevantModNameList` postfix：把 `HextechRunes-0.9.1` 改写成 `HextechRunes-0.9.1-net1`，改变原版联机校验输入。
- `OneTimeInitialization.ExecuteEssential` postfix：重排 `SavedPropertiesTypeCache` 两张私有表并写回 `<NetIdBitSize>k__BackingField`（仅 <0.109 生效）。

### P3 同一目标多处补丁
六个视觉 Hook 组各自对 `NCombatRoom._Ready`、`NCombatRoom.AddCreature`、`NCreature._Ready` 打 postfix，等于 18 个补丁做一件事：给每个 creature 节点挂附件。`RelicCmd.Obtain` 三处靠 High/默认/Low 三档优先级排队。`CardModel.From/ToSerializable/DowngradeInternal` 三个功能各自抢写。

### P4 私有成员访问散落
51 处原始反射分布在 25 个文件；`HextechHookReflection.TryGetField` 失败返回 null 并"降级"，结果是功能悄悄失效而不是启动时报错。典型：`NInspectRelicScreen` 10 个私有字段、`NMultiplayerPlayerIntentHandler` 5 个、`AttackCommand` 4 个、`NOrbManager` 3 个。

### P5 版本兼容层
10 个编译目标（0.103.2 → 0.111.0）只发 3 个；`STS2_104_OR_NEWER` 等 7 个符号对应的分支全是死代码；133 处 `#if` 分布在 Runes/Hooks/Relics/Compat/Tests 各处。

### P6 资源 Hook 冗余
`RelicModel.Icon/IconOutline/BigIcon` 三个 prefix 直接 `return false`，而这三个 getter 只是 `ResourceLoader.Load(PackedIconPath)` / `PreloadManager.Cache.GetTexture2D(ResolvedBigIconPath)`，`HextechRelicBase` 已覆写了这三个虚路径。真正需要 hook 的只剩 `PowerModel.HoverTips`（record struct 值语义）和 `RestSiteOption.Icon`（base-game 命名空间路径）。

### P7 loader 双重贡献类型
`RegisterVariantAssembly` → `InstallReflectionBridge()` 无条件执行；`AssociateVariantAssemblyWithGame` → 0.108+ 走 `AssociateAssemblyWithMod`。RitsuLib 与 figure_Saya 的同款三级回退里，`ReflectionHelper.ModTypes` 补丁是**第三级兜底**，前两级成功就不装。

### P8 结构与状态
- god class：`HextechRuneConfigMenuHooks` 2,836+546 行、`HextechCombatVfx` 1,588、`DoubleVisionRune` 1,388（外加 10 个奖励管线补丁）、`HextechRuneConfiguration` 1,021、`HextechChoiceCodec` 954、测试 `Program.cs` 5,769。
- `src/Hooks` 是按"技术手段"而不是"功能"分目录，一个符文的行为散在 `Runes/X.cs`、`Hooks/Runes/HextechPlayerRuneHooks.X.cs`、`Hooks/UI/HextechXVisualHooks.cs` 三处。
- ≈376 个可变静态字段；`HextechScopedDepthGuard` 全局深度守卫已被 0726 审查判定会误伤无关玩家（A1）。

### P9 "兼容性补丁"塞在内容模组里
无尽模式、集成战略事件、Artifact、Entomancer、PersonalHive、GameOver 记分行、UI 安全、动画触发安全……共 9 个 `*Compatibility/*Safety` Hook 组，都是给别人的 bug 或原版 bug 打补丁。这些应当独立成"兼容包"或改为软依赖桥，不该跟内容一起发布、一起承担联机 hash。

## 4. 目标架构

```
loader/                 引导壳（保留），三级回退只走一条：Associate 成功 → 不装 ReflectionBridge
src/
  ModEntry.cs           ≤40 行：Compat.Validate() → Content.Register() → Patches.ApplyAll() → Services.Start()
  Compat/               唯一允许出现 #if 的目录；每个游戏版本差异一个同形静态类（Sts2Api.Cards / .Combat / .Net …）
  Patching/             HextechPatcher（属性式 PatchAll + 元数据 + 逐条报告 + LogPatchConflicts + VanillaCopyGuard）
                        GameInternals（所有私有成员句柄集中声明，Validate() 启动一次性列出缺失）
  Content/              Catalog 单一元数据源 → ModHelper.AddModelToPool；不再读 _moddedContentForPools
  Features/<Feature>/   垂直切片：模型 + 该功能的 [HarmonyPatch] 类 + 视觉附件 + 测试，一个目录一个功能
  Multiplayer/          ChoiceCodec、选择同步、入房诊断（不改校验）
  Services/             配置、遥测（OnMetricsUpload）、更新检查
  UI/                   PCK 场景 + [ScriptPath] Node 类；不戳 NMainMenu 私有字段
```

依赖方向沿用 `docs/architecture.md`：`Platform → Mayhem → Selection → Core`，本方案只是把 "Platform" 这层从 20,536 行的 Hooks 目录变成 Patching + Compat 两个小目录。

## 5. Harmony 补丁处置清单

处置代码：**删**=官方扩展点已覆盖，直接删除；**并**=多处合一；**改**=保留但改为属性式 + 守卫；**退**=联机校验类，随 0.107.1 分支退役；**重**=重做实现方式。

### 5.1 删（官方 Hook / 事件 / 虚属性已覆盖）

| 目标 | 现状 | 替代 | 核验 |
|---|---|---|---|
| `CardModel.CanPlay` ×2 + `CanPlay(out,out)`（3 个 postfix，First/Last 优先级） | BackToBasics / Kaka 禁玩 | 符文覆写 `RelicModel.ShouldPlay(combatState, card, ref preventer, autoPlayType)` | 0.111 `CardModel.CanPlay(out reason, out preventer)` 第 3350 行调 `Hook.ShouldPlay` ✔ |
| `RelicModel.Icon / IconOutline / BigIcon` 3 个 prefix、`NRelic.Reload` prefix | 自定义图标 | `HextechRelicBase` 已覆写 `PackedIconPath / PackedIconOutlinePath / BigIconPath`；把图标作为 Godot 导入资源进 PCK 后直接可用 | 0.111 `RelicModel` 第 153–169 行 ✔；需确认 PCK 内 `.tres/.png` 已被 Godot 导入（`tools/pack_mod.gd` 已有流程） |
| `PowerModel.Icon / BigIcon` postfix、`CardModel.Portrait`、`EnchantmentModel.Icon` postfix | 同上 | `PowerModel.PackedIconPath` 非 virtual：改为自定义 Power 覆写 `Id.Entry` 对应的 atlas 命名（把 png 放进 `atlases/power_atlas.sprites/<entry>.tres` 同名路径）或保留**单个** postfix | 0.111 `PowerModel` 第 124/134 行；Card/Enchantment 待核验 |
| `RunManager.OnEnded` prefix+postfix | 遥测上报 | `ModManager.OnMetricsUpload += (run, isVictory, localPlayerId) => …` | 0.111 `ModManager.add_OnMetricsUpload` ✔ |
| `NGame.StartRun` prefix+postfix、`NGame.LoadRun` postfix | 建立 MayhemModifier、幕守卫 | `RunManager.RunStarted` 事件 + `ModifierModel` 自身生命周期回调（`AfterActEntered` 已在用） | 0.111 `RunManager.add_RunStarted(Action<RunState>)` ✔；LoadRun 路径待核验是否触发 RunStarted |
| `NEventRoom.Proceed` prefix+postfix | 事件房离开 | `Hook.AfterRoomEntered / BeforeRoomEntered`（Modifier 覆写）或 `RunManager.RoomExited` 事件 | 0.111 ✔；语义等价性待核验 |
| `NCombatRoom._Ready` ×6、`AddCreature` ×6、`NCreature._Ready` ×6 | 6 组视觉附件 | `CombatManager.CombatSetUp / CreaturesChanged` 事件 + `NCombatRoom.Instance.GetCreatureNode(creature)` | 0.111 两者均为公开 API ✔ |
| `PlayerCmd.GainGold`、`PotionCmd.TryToProcure`、`CardReward/RelicReward/SpecialCardReward.OnSelect`、`CardPileCmd.Add`、`Reward.SelectUnsynchronized`（DoubleVision 事务 8 个补丁） | 复视复制奖励 | `Hook.AfterRewardTaken(runState, player, reward)` + `AfterPotionProcured` + `AfterGoldGained` + `ModifyCardBeingAddedToDeck`；以 Modifier 覆写实现，在同一处决定"是否复制" | Hook 签名 ✔；DoubleVision 的"同一事务只复制一次"语义要用 `reward` 实例做幂等键，待设计 |
| `CardPileCmd.AddGeneratedCardsToCombat` ×2、`Shiv.CreateInHand` ×4 | 大刀替换生成的飞刀 | `Hook.AfterCardGeneratedForCombat(combatState, card, creator)` 里做替换 | ✔（替换需能移除原卡，待核验 `CardPileCmd.Remove` 可用性） |
| `ModelDb.Init` postfix（移动端重复注册 workaround）+ 读 `ModHelper._moddedContentForPools` | 去重 | 自持 `HashSet<(Type pool, Type model)>` 去重后只调公开 `AddModelToPool` | ✔ |
| `Log.Warn` prefix | 压自比较假警告 | 修 loader（P7），警告自然消失 | 见 §6 阶段 1 |

### 5.2 并（合一）

| 目标 | 现状 | 处置 |
|---|---|---|
| `RelicCmd.Obtain` ×3（RewardSafety High / TezcatarasMercy / ForgeStacking Low） | 三档优先级排队 | 一个 `RelicObtainPatch`，内部按固定顺序调用三个纯函数；若 5.1 的 DoubleVision 迁移完成则只剩两个 |
| `CardModel.FromSerializable` ×3、`ToSerializable` ×2、`DowngradeInternal` ×2、`FinalizeUpgradeInternal` | 自升级存储 / 思维覆写关键词 / 起始牌升级 | 改用 MEM 模式：一个 `HextechCardCarrier` 载体类声明 `[SavedProperty]`，数据走 `SerializableCard.Props`；只剩 0 个 patch（若原版 Props 通道在 0.111 可写）或 1 组 |
| `NMainMenu._Ready` ×2 | 配置按钮 + 更新检查 | 一个 `MainMenuReadyPatch` 派发给两个订阅者 |
| `ArtifactPower.TryModifyPowerAmountReceived` ×2 | Artifact 兼容 + Neurosurge | 一个补丁类 |
| `NCreature.StartDeathAnim` ×2 | VFX + 飞踢尸体 | 一个补丁类 |

### 5.3 改（保留，改属性式 + 守卫）

以下目标原版没有扩展点，保留 Harmony，但全部改为 `[HarmonyPatch]` 属性类、放进对应 Feature 目录、显式 `[HarmonyPriority]`，凡 `return false` 复制原版逻辑的登记进 `VanillaCopyGuard`（IL SHA1 冻结表）：

- `CreatureCmd.Heal`（治疗倍率与封顶；改为 void 前缀 + 本地三阶段聚合器，见 §8.3）、`CreatureCmd.GainMaxHp/LoseMaxHp/SetMaxHp`（最大生命换算）、`CreatureCmd.Damage`、`Creature.DamageBlockInternal`、`AttackCommand.Execute`（双持；可先试 `Hook.ModifyAttackHitCount` + `Before/AfterAttack`）
- `CardModel.SpendResources / OnPlayWrapper / ResolveEnergyXValue / get_MaxUpgradeLevel / get_Tags`（`get_Tags` 无官方口子；`ResolveEnergyXValue` 可试 `Hook.ModifyXValue`）
- 原版能力：`StormPower`、`EntropyPower`、`Outbreak`、`SleightOfFleshPower`、`NeurosurgePower`、`SlipperyPower`、`JuggernautPower`、`CreativeAiPower`、`AutomationPower`、`CorrosiveWavePower`、`OblivionPower`、`PoisonPower`、`DieForYouPower`、`ArtifactPower`
- 原版卡 `OnPlay`：`BodySlam`、`Compact`、`CrashLanding`、`DecisionsDecisions`、`GrandFinale`、`HiddenGem`、`Jackpot`、`Survivor`、`Voltaic`、`WroughtInWar`、`SovereignBlade`、`BladeOfInk`、`PactsEnd`——**每一个先审一遍能否用符文自身的 `BeforeCardPlayed / AfterCardPlayed / ModifyDamage / ModifyBlock` 表达**，能表达的删；剩下的保留但禁止 `return false` 整段重写（参考 `hextech-vanilla-power-reimpl-pitfall` 的 Storm 教训）
- 原版遗物：`Kunai / Shuriken / OrnamentalFan / PenNib / Nunchaku`（幻影武器计数）、`DustyTome`
- `OrbCmd.AddSlots`、`NOrbManager.TweenLayout`、`CardSelectCmd.FromHand`、`ForgeCmd.Forge`、`PotionRewardOdds.Roll`、`TreasureRoomRelicSynchronizer.BeginRelicPicking`、`MerchantInventory` 相关
- UI：`NRelicInventory*`、`NCombatUi.*`（遗物栏隐藏开关 10 个补丁 → 一个类）、`NTopBarPortraitTip`、`NCardGrid.set_IsShowingUpgrades`、`NHealthBar` 燃烧预测、`NIntent/AbstractIntent` 双持意图
- `PowerModel.GetDumbHoverTip / HoverTips` 后缀（record struct 值语义，保留）、`RestSiteOption.Icon` 前缀（保留）

### 5.4 退（联机校验类，随 0.107.1 分支退役）

| 目标 | 处置 |
|---|---|
| `OneTimeInitialization.ExecuteEssential` 后缀 + `HextechSavedPropertyNetIdCanonicalizer` + `SetNetIdBitSize` | 0.109+ 已空转；0.107.1 变体退役时整体删除。若 0.107.1 还要维持一段时间，改为 RitsuLib 做法：排序后把追加的名字**同时喂进原版 XxHash**，而不是只重排表 |
| `NetHost/NetClientGameService.OnPacketReceived` 终结器 | 0.109+ 原版 idDatabaseHash 门禁已在入房前拦住 SavedProperty 失配；删除。想保留诊断价值，改为订阅原版 `NetError` 弹窗后附加一段"本地签名"日志，不接管包处理 |
| `ModManager.GetGameplayRelevantModNameList` 后缀 | 删除。wire 协议变化时抬 manifest `version`（这本来就是原版清单比对的输入）；协议号可放进 version 字符串的 build 元数据段 |
| `Log.Warn` 前缀 | 删除（P7 修完后无警告） |

### 5.5 重（重做实现）

| 功能 | 现状 | 重做方向 |
|---|---|---|
| 形态开局自动打出批处理（`HextechFormAutoPlayHooks`，9 个补丁含 5 个 `Hook.*` First 前缀 + `PlayerCmd.EndTurn` First 前缀 + 每个形态 `OnPlay` 前缀） | 把 N 张形态牌塞进出牌管线再压掉大部分回调 | 不走出牌管线：开局直接 `PowerCmd.Apply` 对应形态 Power + 卡牌移到消耗堆 + 一次 VFX。当前实现本来就跳过了 `Before/AfterCardPlayed`，等价性反而更好；`AsyncLocal` 抑制 EndTurn 的整套机制随之消失 |
| 配置菜单（3,382 行） | 代码构建节点树，戳 `NMainMenu` 私有字段模拟原版按钮焦点 | 主面板做成 PCK 场景 + `[ScriptPath]` Node；主菜单只留一个 postfix 加按钮；按钮用原版 `NMainMenuTextButton` 的公开 API 或克隆原版按钮节点，不读 `_locString/_lastHitButton` |
| 视觉附件（6 组 ≈ 3,000 行） | 每组自己找节点、自己管生命周期 | `ICreatureVisualAttachment` 接口 + 一个 `CreatureVisualHost`：订阅 `CombatManager.CreaturesChanged`，对每个 `GetCreatureNode` 挂载/卸载附件；各附件只写"画什么" |
| 兼容性/安全补丁 9 组 | 与内容同 DLL 发布 | 拆成 `HextechRunes.Compat` 独立模组（`affects_gameplay: false` 的部分）或软依赖桥；对其他模组的 patch 一律用 `AccessTools.TypeByName` 探测 + 独立 Harmony id |

## 6. 分阶段执行

每个阶段一个 PR，合并门槛统一：`bash tools/run_tests.sh` 三目标全绿、`validate_hextech_content.py` 通过、三个变体 Release 0 warning、headless 加载确认行、**双客户端联机冒烟（主机 0.111 + 客机 0.111，一幕内含符文选择 + 锻造 + 复视）**、`saved_property_manifest.txt` 无 diff（说明 net-id 布局未动）。

### 阶段 0：清死代码（1 天，零风险）
- csproj 只保留 `0.107.1 / 0.110.0 / 0.111.0` 三个目标；删除 `STS2_103_2 / 104 / 105 / 106 / 107_OR_NEWER / 108 / 109` 对应的 `#if` 分支（保留 `STS2_107_1`、`STS2_110_OR_NEWER`、`STS2_111_OR_NEWER` 三个符号，语义改为"当前分支"）。
- `ModInfo.TargetGameVersion` 的 10 段 `#elif` 缩到 3 段。
- 删除 `versioned-dll-backups` 里未发布版本的 refs（保留 0.109.0 供 `sts2-inspect` 对比即可）。
- 验证：IL 等价法（`ilspycmd` 反编译 diff 只差版本元数据）。

### 阶段 1：loader 与联机校验退役（1 天，需双客户端验证）
- `LoaderBootstrap.AssociateVariantAssemblyWithGame` 成功后**不再**调用 `InstallReflectionBridge`；只有两级都失败才装。
- 删除 `HextechModelIdSerializationWarningHooks`、`HextechMultiplayerCompatibilityHooks` 的清单重写与包终结器；保留 `BuildModNetworkSignature` 作为入房失败时的**诊断日志**。
- `HextechSavedPropertyNetIdHooks` 用 `#if STS2_107_1` 包住，0.110/0.111 变体不编译。
- 验证：0.111 双客户端入房、一幕联机；故意用不同版本 manifest 入房应得到原版 ModMismatch。

### 阶段 2：补丁基础设施（2~3 天，行为不变）
- 新建 `src/Patching/HextechPatcher.cs`：`[HarmonyPatch]` 属性类 + `PatchAll`；每个补丁类加 `[HextechPatchMeta(Id, Critical, Feature)]`；`ApplyAll()` 输出逐条报告；critical 失败禁用**该 Feature** 而不是整组；`LogPatchConflicts()` 列出与其他 owner 共享的目标。
- 新建 `src/Patching/GameInternals.cs`：现有 51 处反射句柄全部搬进来，`static readonly` + `Validate()` 一次性列出缺失；热路径用 `AccessTools.FieldRefAccess` / `[UnsafeAccessor]`。
- 新建 `VanillaCopyGuard`：登记所有 `return false` 复制原版逻辑的目标及其 IL SHA1（三目标各一张表）。
- 把 199 个 `harmony.Patch` 机械转换为属性类，**不改任何补丁体**；`ModEntry` 缩到编排。
- 新增测试：补丁目标清单快照（`tests/patch_manifest.txt`，同 `saved_property_manifest.txt` 机制）；`VanillaCopyGuard` 冻结表比对。
- 验证：补丁目标清单与阶段 1 结束时逐条相同；IL 等价法确认补丁体未变。

### 阶段 3：迁移到官方扩展点（1 周，逐项验证）
按 §5.1 逐项执行，每迁一项跑一次对应符文的行为测试 + 补丁清单快照更新。顺序建议：`CanPlay` → 资源图标 → `OnMetricsUpload` → 视觉附件事件化 → 运行生命周期事件 → 复视奖励事务 → 飞刀生成 → 治疗管线改 void 前缀 + 聚合器（§8.3，需双客户端验证）。
每项的"待核验"先用 `tools/sts2-inspect decompile` 确认三目标签名一致，再动手。

### 阶段 4：重做三块（1 周）
形态自动打出、配置菜单、视觉附件宿主，按 §5.5。形态自动打出必须双客户端验证（它是 0.8.x 时期联机分叉的老根据地）。

### 阶段 5：垂直切片与状态收敛（持续）
- 按 Feature 搬目录：`Runes/X.cs` + `Hooks/Runes/…X.cs` + `Hooks/UI/…XVisual.cs` 合到 `Features/X/`。纯移动，IL 等价法验证。
- 每个 Feature 的静态可变字段清零：run 级状态进 Modifier 的 `[SavedProperty]`（**注意**：新增 SavedProperty 会改 net-id 布局与联机 hash，必须与版本发布同步），combat 级用 `ConditionalWeakTable<CombatState, T>`，请求作用域用 `AsyncLocal`。
- 拆 god class：`DoubleVisionRune` 拆成"事务判定 / 复制执行 / 联机同步"三个类；测试 `Program.cs` 按 Feature 拆文件（harness 保留）。
- `#if` 全部收口到 `Compat/`：Feature 代码只调 `Sts2Api.*`，同名同形不同实现按目标编译。

## 7. 不要动的东西

- **loader 三级回退本身**（`AssociateAssemblyWithMod → Mod.assemblies → OnModDetected`）：与 RitsuLib、figure_Saya 完全同构，是社区标准。只修"第三级不该无条件装"。
- **同源多变体编译**：RitsuLib 与 Pengo 都是一版本一 DLL，方向正确。
- **`HextechRelicBase / HextechPowerBase / HextechModifierBase` 的 `sealed override → virtual Compat`** 适配层：这是 0.108 伤害签名变更后的正解，保留。
- **`AsyncLocal` 作用域、`saved_property_manifest.txt` 快照测试、v15 迁移链守护、双目标测试循环**：护栏都保留。
- **`HextechRunesApi` 公开面**：赞助包依赖 `RegisterPlayerRune / RegisterEventRelic / RegisterForge / SelectRelicOption / RegisterSavedPropertyCarrier / RegisterEnchantmentIcon / ObtainRandomForges / TrackPersistentInnate` 等 13 个符号（`HextechRunesSponsorPack/src` 39 个文件核对），签名不改，实现可换。
- **模型 ID、稀有度、随机盐、`PlayerChoiceResult` 语义、ChoiceCodec wire 格式**：不在本轮范围。

## 8. 三项已裁决事项

以下三条原本列为"待实验裁决"，经 0.111 反编译与参考模组源码核对后已有明确结论，只有第 3 条还需要一次窄范围的双客户端验证。

### 8.1 外部 SavedProperty 载体的注册时机：维持海克斯现状，禁止 Debug 注入

**裁决**：海克斯 0.109+ 的做法（载体必须是能被 ModelDb 发现的 `AbstractModel`，`RegisterSavedPropertyCarrier` 只校验不注入）是正确的；MultiEnchantment 在 `Initialize()` 里调 `CacheSavedPropertiesForTypeDebug` 只是"单独安装时碰巧能用"。

**依据**（0.111 `ModelIdSerializationCache` 反编译）：
- `Init()` 只从 `ModelDb.All` 取类型，经 `ContentSorter<ModelId>.Sort` 排序后逐个 `CachePropertiesForType(type, xxHash, buffer)`，属性名进 XxHash；`Init()` 开头**不清表**，只有 `ResetForTest()` 清。
- `CacheSavedPropertiesForTypeDebug(type)` 就是 `CachePropertiesForType(type, null, null)`：hasher 为 null，名字不进 hash。
- 因此 Init 前 Debug 注入的名字会占住最前面的 net-id，但既不参与官方排序也不进 hash。只装一个这样的模组时两端布局一致；两个模组都这么做、两端加载顺序不同，net-id 错位而 hash 门禁看不见——这正是 `hextech-savedproperty-bitsize-asymmetric-mods` 记录的机制。

**执行**：
- `HextechRunesApi.RegisterSavedPropertyCarrier` 文档注释与赞助包指南写明硬约定："载体必须是能被 ModelDb 发现的 AbstractModel；任何版本都禁止调用 `CacheSavedPropertiesForTypeDebug`"。
- 赞助包的四个载体（`Evolution` / `EntropyIncrease` / `EntropyDecrease` / `SponsorCompositeEnchantment`）纳入 `tests/saved_property_manifest.txt` 快照，载体清单变更即红。
- 0726 审查 S1 的结论成立，无需再实验。

### 8.2 `CardModel.get_Tags`：保留 postfix，加"只追加"约束

**裁决**：保留 postfix。RitsuLib 在同一 getter 上挂了 `CardModelCapabilityPatches.TagsPatch`，同样是纯追加 postfix，不 `return false`；三个内容模组都不碰它。海克斯 `CardTagsPostfix` 只做 `Append`，两者可叠加、顺序无关。

**执行**：从 `HextechPlayerRuneHooks` 拆成独立补丁类；加测试断言 postfix 只能追加、不能移除或替换 `__result` 中已有元素。

### 8.3 治疗管线：改 void 前缀 + 本地三阶段聚合器

**裁决**：这是三条里唯一需要动实现的。RitsuLib `CreatureCmdHealHookPatch` 是 `void Prefix(ref decimal amount)`，`[HarmonyPriority(0)]` + `[HarmonyAfter("BaseLib")]`，只改 `amount` 不接管方法。海克斯 `HealPrefix` 是 `bool` 前缀，四种情况 `return false` 跳过原方法（濒死狂宴禁回血、玻璃大炮封顶到 0、敌方回血转延迟格挡、修正后不足 0）。只要玩家同时装了 RitsuLib / BaseLib 系模组，海克斯一跳过原方法，对方的治疗修正整个丢失；反之对方也看不到海克斯的封顶。

**执行**：
1. `HealPrefix` 改为 `void` 前缀，"禁止回血"一律表达为把 `amount` 压到 0，不再跳过原方法；敌方回血转延迟格挡同样在前缀里归零 `amount` 并排队格挡。动手前用 `sts2-inspect` 读 0.111 `CreatureCmd.Heal` 的异步状态机体，确认 `amount <= 0` 时直接返回（本轮只看到外壳）。
2. 海克斯内部四处修正收进一个本地聚合器：加法项求和 → 乘法项连乘 → 最后封顶，一个入口写回 `amount`。签名照 RitsuLib `IHealHookListener` 的三阶段写，不引用 RitsuLib；日后要互通再加软依赖桥。
3. 聚合器落地后拆掉 `FinalizeGlassCannonHealCapPrefix` 的 `Priority.Last + after = Natsuki.EndlessMode` 排队。
4. 验证点很窄：装与不装 RitsuLib 两种环境、双客户端，濒死狂宴 / 玻璃大炮 / 敌方延迟格挡三个符文的回血数值一致。

## 9. 执行进度（2026-09-02，commit 24f29adc → 3f294ae3）

每步都通过了三目标 Release 零警告、三目标测试全绿、部署 + headless 加载、以及"补丁表导出比对"（`HEXTECH_DUMP_PATCHES` 导出目标/种类/优先级/同目标执行序，改动前后 normalize 后 diff）。

| 阶段 | 状态 | 结果 |
|---|---|---|
| 0 清死代码 | 完成 | 编译目标 10 → 3（csproj 加 `HextechValidateTarget` 拦截其它值）；`#if` 133 → 56；ModInfo 版本链 3 段 |
| 1 loader / 联机校验 | 完成 | loader 只在 Associate 两级都失败时才装 `ReflectionHelper.ModTypes` 后缀，headless 确认走 `AssociateAssemblyWithMod` 且自比较假警告消失；删除 `Log.Warn` 拦截、模组清单条目重写、两个包接收终结器；net-id 规范化整文件 `#if STS2_107_1`；0.109+ 载体自检改到首次 StartRun/LoadRun 一次性执行 |
| 2 补丁基础设施 | 完成 | 199 个手工 `harmony.Patch` 全部改为 `[HarmonyPatch]`+`[HextechPatch]` 嵌套补丁类（160 个类），`ModEntry` 只剩编排；`HextechPatcher`（逐类应用、失败按符文/功能归因、共享补丁点日志、补丁表导出）；补丁清单快照测试 `patch_manifest.<target>.txt`；原版拷贝守卫 `vanilla_copy_guard.0.111.0.txt`（95 个可跳过原方法的目标，嵌入 DLL 启动比对 IL SHA1）；反射缺失一次性汇总 |
| 3 迁到官方扩展点 | 部分 | `CardModel.CanPlay` 三处 postfix 全部删除：回归基本功/卡卡走 `ShouldPlay`，敌方回归基本功走 Modifier→敌方海克斯效果的 `ShouldPlay`，蓝蜡烛走 `TryModifyKeywordsInCombat` 摘掉 Unplayable，升级压轴只补 `GrandFinale.IsPlayable`；六组视觉附件 18 个补丁合并为 `HextechCreatureVisualHost` 3 个 |
| 3 资源图标（第二轮） | 完成 | 用游戏自带引擎跑 GDScript 探针（`--headless -s probe.gd` 挂 mod PCK）证实 PCK 内图片可被 `ResourceLoader` 直接加载为 `CompressedTexture2D`；删除 `RelicModel.Icon/IconOutline/BigIcon` 三个前缀、`NRelic.Reload` 前缀与 `CardModel.Portrait` 后缀（模型的虚路径属性已指向 PCK 资源；描边改指一张全透明图 `relicOutlineEmpty.png`）。手动解码纹理的工具从补丁类拆成 `HextechTextures`，只服务模组自建 UI/特效。资源补丁 11 → 6 |
| 3 治疗管线（§8.3 修正） | 完成 | 反编译 `CreatureCmd.Heal` 状态机：amount 为 0 也会播治疗音效/特效/动画，因此"禁止回血"必须继续跳过原方法，§8.3 的"改 void 前缀"不成立；只给封顶前缀补 `[HarmonyAfter]` RitsuLib core 与 BaseLib，保证第三方加减乘算完后再封顶 |
| 4 重做三块 | 未做 | 形态自动打出的批处理刻意走出牌管线以保留附魔/流电/克隆语义，`Hook.*` 前缀在批处理窗口外一律直接放行，直接施加 Power 的重做会丢这些语义，不做；配置菜单场景化、视觉附件事件化需真机验证 |
| 5 垂直切片（第一轮） | 完成 | 36 个符文专属补丁类搬进各自符文文件（如 `SurvivorUpgradeRune` 内嵌 `SurvivorPatch`），删除 `HextechRuneMechanicHooks`、`CrashLanding`、`CardUpgradeSelection` 三个文件；`src/Hooks/Runes` 只剩共享辅助（幻影武器、充能球布局、卡牌标签、亮剑枚举） |
| 5 垂直切片（第二轮） | 完成 | 幻影武器 7 个补丁类内嵌进 `IllusoryWeaponRune`；版本兼容基类文件（`HextechGameApiCompat`、`HextechModelBaseCompat`）移入 `src/Compat/` |
| 5 god class 拆分 | 完成（分部文件） | `DoubleVisionRune` 1,388 行 → 主体 118 行 + Transactions / Duplication / Scopes / Types 四个分部；`HextechCombatVfx` 1,562 → 主体 446 + Sequences / Primitives；`HextechRuneConfigMenuHooks` 2,836 → 主体 219 + Overlay / Pages / BottomBar / RuneGrid / Entries / Types。纯移动，按 IL 等价法思路只影响编译顺序 |
| 5 `#if` 收口示范 | 完成 | `HextechSavedPropertyBootstrap` 拆成共享流程 + `.Legacy.cs`（整文件 `#if STS2_107_1`）+ `.Official.cs`（整文件 `#if STS2_109_OR_NEWER`），共享代码里不再有任何 `#if`；同一套路可继续用于 `HextechRelicBase`、形态自动打出等剩余 `#if` 密集文件 |
| 4 配置菜单（主菜单按钮） | 完成 | 按钮改在 `NMainMenu._Ready` 的前缀里插入：原版随后自己的 `ConnectMainMenuTextButtonFocusLogic` 会把焦点光标动画连到它身上；文案走公开的 `SetLocalization`，`HEXTECH_CONFIG_BUTTON*` 两个键迁到 `main_menu_ui.json`（9 语言，原版 `LocManager` 逐表合并模组 loc 文件，headless 日志已见 `Merging with base loc table`）；覆盖层关闭时把焦点还给打开它的按钮。`_locString` / `_lastHitButton` / `MainMenuButtonFocused` / `MainMenuButtonUnfocused` 四个私有成员反射全部删除，配置菜单不再触碰任何原版私有成员。主面板场景化不做：程序化节点树没有冲突面，场景化只换可维护性，且需要 Godot 编辑器工作流与真机逐页核对 |
| 5 测试文件拆分 | 完成 | `tests/Program.cs` 5,948 行按主题拆成 Selection / Config / Metadata / EnemyHexes / DoubleVision / Runes 六个分部（221 个用例按名称前缀归组、无遗漏），主文件只剩 harness、注册表与共享辅助（782 行） |
| 5 `#if` 现状 | 133 → 50 | 剩余 50 处里 `HextechGameApiCompat` 占 9、其余全是原版虚方法/`Hook.*` 签名随版本变化的覆写或补丁目标声明（`ModifyDamage*`、`ModifyCardPlayResultLocation`、`Shiv.CreateInHand`、`GetResultLocationForCardPlay`），属于 §5.3 允许保留的那一类，不再往下压 |
| 5 静态字段审计 | 完成 | 92 个可变静态字段逐个核对：约 65 个是反射句柄 / 纹理缓存 / 只记一次的日志旗标（进程级只写一次）；约 15 个是 UI 单例的节点引用（随界面重建重绑）；约 10 个是运行期状态，全部带显式重置（`HextechGoldrendSync` 按 RunState 引用切换、`HextechRunLifecycleHooks` 按 RunManager 引用重订阅、`HextechGoldenRerollSession` 用弱引用 + 一次性旗标、`CompensationEnemyHex` 在命令结束时清空）。没有需要改作用域的项 |
| 5 状态收敛 | 护栏先行 | 加入可变静态字段清单快照测试 `static_state_manifest.<target>.txt`（非 readonly、非 const 的静态字段全部列出，实际 92 个，§1 的 ≈376 是把 readonly 缓存也算进去的粗估），新增一个就必须在清单里显形；逐个改作用域仍是后续语义改动 |

评估后**决定保留**的补丁（理由已核实，不要再翻案）：
- `CardPileCmd.Draw` 前缀：卡牌检视是"用选牌界面替换抽牌返回值"，`ShouldDraw/ModifyHandDraw/BeforeHandDraw` 都表达不了，且改成 Hook 会把 PlayerChoice 挪到不同的同步点。
- `RunManager.OnEnded` 前缀+后缀：前缀要在 `ToSave` 之前补战斗历史（原版败北存档缺房间记录），`OnMetricsUpload` 只在 `ShouldSave` 且首次上报时触发，替不了。
- `NGame.StartRun / LoadRun`：需要包住原版 UI 任务链再做延续，`RunManager.RunStarted` 只在模型层触发。
- 资源图标 11 个补丁：原版 `RelicModel.Icon` 确实只读虚属性 `PackedIconPath`，理论上可以整组删除，但纹理加载曾多轮返工（见 `sts2-resourcepath-assetcache-dispose`），headless 无法验证视觉，必须真机看过遗物栏/图鉴/检视/悬浮四处再删。
- 复视奖励事务 8 个补丁：改用 `AfterRewardTaken` 等需要重新设计"同一事务只复制一次"的幂等键，属于重做而非迁移。

后续动手顺序建议：先做资源图标组（真机验证四处 UI 即可删 11 个补丁），再做治疗管线 void 前缀 + 聚合器（§8.3，需双客户端），最后才是形态自动打出重做。
