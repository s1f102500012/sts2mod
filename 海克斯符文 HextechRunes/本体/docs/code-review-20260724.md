# HextechRunes 本体代码审查执行文档(2026-07-24)

> 生成自一轮多智能体全量审查(759 文件 / 6.67 万行 / 173 个 Harmony patch / 214 个 catch 全量走查,
> 关键结论均经 `tools/sts2-inspect` 对 0.107.1 与 0.109.0 双版本反编译核验,并经对抗验证轮修正)。
> 审查目标:①优化与其他模组的兼容性 ②代码规范化 ③减少过度防御性编程。

## 0. 执行者必读:背景与硬约束

### 仓库与构建
- 仓库 `/Users/iniad/sts2-mods`,分支 `dev`,模组根 `HextechRunes/`。
- 双版本支持:同源双编译(csproj 按 `HextechSts2Target` 叠加 `STS2_xxx_OR_NEWER` 常量)。
  每批改完必须验证:
  ```bash
  cd /Users/iniad/sts2-mods/HextechRunes
  bash tools/run_tests.sh          # 注意用 bash,zsh 下会因 BASH_SOURCE 失败;应两目标各 135/135
  dotnet build src/HextechRunes.csproj -c Release -p:HextechSts2Target=0.107.1   # 0 error
  dotnet build src/HextechRunes.csproj -c Release -p:HextechSts2Target=0.109.0   # 0 error
  ```
- 除既有 2 条 nullable 警告(本文档 F94 会修掉)外,仓库是零警告基线;不得引入新警告。
- 不要部署到游戏 mods 目录、不要打包发布、不要推送远端。
- 工作树上已有一轮未提交的"纹理加载器修复"(AssetHooks.cs、HextechRuneSelectionScreen.Style.cs、
  AssetResourceResolver.cs、AssetUiSafetyTests.cs、Program.cs)。它已经过审查与双引擎运行时验证,
  **不要回退或修改其语义**;先把它作为独立 commit 提交,再开始本文档的批次。

### 联机安全判定框架(改 catch/防御前必读)
本 mod 是联机向模组,有多条既有教训:表现层异常发生在同步写入前会致 checksum 分叉/踢人;
异步任务链中的异常只被 OCE-only catch 吞一半;两端 catch 触发时机不同本身会制造分叉。因此:
- **必须保留**的防御:Harmony patch 顶层 catch(同步敏感路径)、网络 payload 解码、第三方注册/回调入口、
  Godot 节点生命周期(QueueFree 竞态)、反射查找失败的降级。
- 本文档所有 defense 类条目的修改方向都是"让失败可见"(加日志/收窄类型/修正 typed catch),
  **除批次 6a 明确列出的死防御外,不要删除任何 catch**。
- 修改任何 prefix 的 return false 条件时,新增早退必须保证两端判定确定一致
  (只依赖同步的 RunState/模型状态,不依赖本地 UI/配置)。

### 提交约定
- 作者署名 `Natsuki`;commit message 中文,风格参照 `git log`(如"海克斯大乱斗:清理已退役海克斯残余");
  **不带任何 AI 署名**(无 Co-Authored-By、无 Generated with)。
- 每个批次一个 commit(批次 6a/6b 可各一个);commit 前跑完上面的验证命令。

### 与文档冲突时
文档行号基于 2026-07-24 的工作树,若与代码现状对不上,以代码为准:先重新定位,定位不到或语义已变则
**保守跳过该条**,在最终报告中列出跳过原因,不要强行套改。

### 严重度与置信度
P1=高价值且已双重验证;P2=确认有效;P3=低优先级。每条含 tag(compat/convention/defense)。
标注"降级说明"的条目,其原始 detail 的部分论证已被对抗验证驳倒,**只执行 suggestion 里修正后的动作**。

---

## 批次 1:速修(五分钟级,先做)

全部是低风险小改:typed catch 修正恢复防御语义、消除 nullable 警告、静默 catch 加日志、失真注释修正。

#### F3. [P2][convention] ModEntry 的安装序契约注释已失真:'全仓库不用 HarmonyPriority'与现状矛盾

- 位置:`HextechRunes/src/ModEntry.cs` :: 25-28
- 来源:harmony-compat,置信度 high

**问题**:注释宣称'② 全仓库不用 HarmonyPriority,同一目标方法多处 patch 的执行序 = 此处 Install 调用序',但当前至少 6 处在用 priority:RewardSafetyHooks.cs:53(RelicCmd.Obtain prefix High)、ForgeStackingHooks.cs:14(同目标 Low)、FormAutoPlayHooks.cs:19(EndTurn First)、CombatHooks.Install.cs:54/58/202(CanPlay postfix Last、AttackCommand postfix Last)、EnemyPowerScalingHooks.cs:21/26/43(First)。对 RelicCmd.Obtain 这种本 mod 三处同目标 patch(RewardSafety High → TezcatarasMercy 默认 → ForgeStacking Low)的真实执行序完全由 priority 决定,与注释描述的'Install 调用序'相反。后续维护者按此注释推理执行序会得出错误结论,这在'安装顺序即契约'的文件头注释里是实打实的维护陷阱。

**改法**:改写该条注释为:'② 同一目标方法多处 patch 默认按此处 Install 调用序;例外(显式 priority)集中在:RelicCmd.Obtain(RewardSafety=High/ForgeStacking=Low,保证 DoubleVision 事务包住锻炉叠层)、PlayerCmd.EndTurn(First)、CardModel.CanPlay(Last,禁玩判定终裁)、AttackCommand/敌方 power 缩放(Last/First)',并要求新增 priority 时同步维护此清单。

#### F10. [P1][defense] catch(InvalidOperationException) 防不住 CanonicalModelException:potion.Owner 守卫是死甲

- 位置:`Relics/Base/HextechRelicBase.CombatHelpers.cs` :: 91-98
- 来源:defense-audit,置信度 high

**问题**:PotionModel.Owner getter 唯一会抛的是 AssertMutable() 的 CanonicalModelException,而它直接继承 System.Exception 而非 InvalidOperationException(0.107.1/0.109.0 双版本反编译实证)。此处 catch(InvalidOperationException) 永远捕不到目标异常:若 canonical potion 真出现,异常将从遗物的药水结算判定(数值路径)单端逃逸——单端中断即联机分叉;而写这个 catch 想防的正是这件事。现实触发概率低(调用方传结算中的 mutable potion),但防御设计意图完全落空。

**改法**:把 catch (InvalidOperationException) 改为 catch (CanonicalModelException),与 HextechPlayerRuneHooks.cs:355 / IllusoryWeaponRune.cs 的既有正确写法一致。一行改动恢复防御语义。

#### F12. [P2][defense] HextechStableRandom 三个 GetSafe* 包装是死防御,真正会抛的 card.Owner 反而裸奔

- 位置:`HextechStableRandom.cs` :: 172, 229-281
- 来源:defense-audit,置信度 high

**问题**:GetSafeInt/GetSafeCreatureKey/GetSafePileKey 包装的 CurrentPlayIndex/CurrentTarget/Pile 三个 getter 在 0.107.1 与 0.109.0 均为纯字段读取、不可能抛出(反编译实证),catch(InvalidOperationException) 是 (a)+(d) 类死代码;其 -1/"none" 哨兵值若真生效反而是分叉源(两端 catch 时机不同→哈希盐不同→稳定随机选出不同结果)。而 CardActionKey:172 直接调用的 card.Owner 才是唯一会抛的(CanonicalModelException),却没有任何防护——防御写在了不会抛的地方,会抛的地方裸奔。现有调用方(VakuuTurnController/CardTransformUpgradeHelper/HextechEnemyTriggerGuard)均传战斗 mutable 卡,实际风险低。

**改法**:删除三个 GetSafe* 包装直接取值(死代码);若要为 CardActionKey 保留 canonical 卡防护,在 172 行处对 card.Owner 单独 catch (CanonicalModelException) 并返回固定串 "owner:canonical"——注意必须是确定性哨兵而非吞掉,保证两端一致。

#### F15. [P1][defense] 系数助手双处静默裸 catch 已实证掩盖过 shipped bug

- 位置:`Combat/HextechPlayerCoefficientHelper.cs` :: 94-101, 148-155
- 来源:defense-audit,置信度 high

**问题**:两个 multiplier 聚合循环里的裸 catch 无任何日志。HeavyHitterRune.cs:13 的注释自证其危害:「Owner.Creature 为 null 会抛 NPE 并被系数助手的 catch 静默吞掉,导致加成不显示」——即一个真实 provider bug 曾被此 catch 掩盖到需要考古才定位。路径本身是纯 hover 显示,吞异常保顶栏不炸是对的((g) 语义),但静默是错的:继续运行安全、可见失败更有价值,二者可兼得。

**改法**:catch 体内用 HashSet<Type> 按 relic/provider 类型每类型 Warn 一次(仿 Content/HextechCatalog.Series.cs:85 的 MissingVisibleCustomRelicLogs 模式),不影响限流也不刷屏。

#### F21. [P3][convention] card.Owner 的 canonical 防护写法不一致:两处裸 catch 应统一为 typed

- 位置:`Helpers/HextechKnifeHelper.cs` :: 30-38, 50-58
- 来源:defense-audit,置信度 high

**问题**:card.Owner getter 唯一会抛 CanonicalModelException(反编译实证)。仓库同场景已有三处正确 typed 写法(Hooks/Runes/HextechPlayerRuneHooks.cs:355、Runes/FlyingKickRune.cs:56、Runes/IllusoryWeaponRune.cs:75),而 KnifeHelper 两处用裸 catch——会把 provider 真 bug(如未来重构引入的 NRE)一并吞成 return false,且与既有惯例分裂。

**改法**:两处改为 catch (CanonicalModelException),与仓库既有写法对齐。

#### F34. [P2][defense] 治疗系数裸 catch 静默吞异常后继续参与 Heal 数值计算

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Combat/HextechPlayerCoefficientHelper.cs` :: 84-103, 140-152
- 来源:hooks-combat-assets,置信度 high

**问题**:GetHealingMultiplier 对每个 IHextechHealingMultiplierProvider(对外 API 面,第三方 mod 可实现)调用 ModifyHealingMultiplicative,裸 catch {} 吞掉异常后按 1m 继续;注释写着 'Hover text should never break the top bar',但该函数同时被 Hooks/Combat/HextechCombatHooks.Healing.cs 的 HealPrefix 用于 gameplay 路径(amount *= GetHealingMultiplier(player)),直接决定同步的治疗量。若第三方 provider 因本地状态(UI/节点未就绪)在一端抛异常另一端不抛,两端治疗量分叉→checksum 分叉,且 catch 完全静默、事后无法归因。这正是'catch 返回默认值进入数值计算'的带病运行模式,比可见失败更危险。MultiplyRelicModifiers(damage/block 系数)同款裸 catch 如仅用于顶栏显示则可接受,但同样零日志。

**改法**:保留 catch(它包的是 IHextechHealingMultiplierProvider 第三方 API 面;对抗验证证明不 catch 会把异常同步打进 CreatureCmd.Heal 任务链,复刻 R1 分叉教训)。只修静默:catch 体内按 provider 类型每类型限流 Log.Warn 一次(仿 HextechCatalog.Series.cs:85 的 HashSet 模式);并在 IHextechHealingMultiplierProvider 的 xml-doc 注明实现必须是纯函数、不得依赖本地表现层状态。不要按原 detail 里"HealPrefix 走不 catch 路径"的思路改。

#### F94. [P1][convention] 仅存的 2 条 nullable 警告污染零警告基线,且都在战斗补丁路径上

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/EnemyHexes/AeonglassEnemyHex.cs` :: 39 (另一处: Hooks/Runes/HextechInkshadowHooks.cs:35)
- 来源:conventions-global,置信度 high

**问题**:实测 dotnet build (0.107.1 变体) 全仓库只有这 2 条警告:AeonglassEnemyHex.cs:39 CS8602 对 shuffler.PlayerCombatState 可能为 null 的解引用;HextechInkshadowHooks.cs:35 CS8604 把可能为 null 的 card.Owner/card.CombatState 传给 Shiv.CreateInHand。仓库其余部分是零警告,这 2 条会让 IDE0005 等新增警告失去'出现即异常'的信号价值。且两处都在 Harmony 接管的战斗执行路径:Inkshadow 是 prefix 接管后 await 进任务链,若 Owner 真为 null 会在异步链里抛 NRE(该类异常按既有教训只被部分 catch,联机下有分叉面)。

**改法**:Aeonglass: 在 39 行前的早退条件里补 `|| shuffler.PlayerCombatState is not { } playerCombatState` 并改用局部变量。Inkshadow: 在 prefix 接管前(返回 true 走原版)增加 `if (card.Owner is null || card.CombatState is null) return true;`,让异常场景退回原版行为而不是在自家任务链里抛。修完后基线归零。

## 批次 2:联机安全(当前最现实的分叉触发器)

敌方海克斯同步等待策略统一。改动集中在一处,改完必须双版本编译+全测试。

#### F63. [P1][defense] 敌方海克斯调整远端接收 10 分钟硬超时后永久停听,制造两端敌方海克斯分叉

- 位置:`src/Selection/EnemyAdjust/HextechRuneSelectionCoordinator.EnemyHexSync.cs` :: 132-170 (配合 Coordinator/HextechRuneSelectionCoordinator.Types.cs:52 EnemyHexAdjustmentTimeoutFrames=36000)
- 来源:selection-sync,置信度 medium

**问题**:ReceiveEnemyHexAdjustments 每轮以 36000 帧(600 秒墙钟)超时调用 TryWaitForRemoteHextechChoice 且未传 shouldContinueAfterTimeout,超时后 Log.Warn 并 return——从此永久停听,但双方选择屏都还开着:权威端(host)之后再 reroll/移除或发送 isFinal 包时,远端的 syncContext.CurrentMonsterHexes 停留在最后一次收到的状态,SelectRunesForAllPlayersMultiplayer 返回值两端不同,SetMonsterHexesForAct 写入不同的敌方海克斯集合,进战斗时被 NetFullCombatState checksum 判分叉踢人。对比同文件族的玩家符文等待(RemoteRuneChoicePollFrames=1800 + ShouldKeepWaitingForRemoteRuneChoice 持续续等)可见这是两套不一致的等待策略,而非刻意设计。触发条件是双方在选择屏上停留超过 10 分钟后权威端仍有操作(挂机等人场景),罕见但后果是硬分叉。

**改法**:给 ReceiveEnemyHexAdjustments 的 TryWaitForRemoteHextechChoice 调用补上 shouldContinueAfterTimeout: () => screen.IsInsideTree() && IsMultiplayerConnected(),把 36000 帧从『放弃阈值』变成『轮询周期』(可顺势降到 1800 帧与玩家符文等待一致);真正退出只保留 screen 关闭、run 变更、断线三种情形。这样权威端晚到的调整与 final 包仍会被应用,两端确定一致。

## 批次 3:兼容性收口(无条件接管改为内容门控)

三个主项是同型改法:海克斯内容不在场时交回原版(prefix 返回 true),内容在场才接管。这是"优化与其他模组兼容性"目标的核心批次。

#### F1. [P1][compat] CreatureCmd.Damage prefix 无条件吞掉一切'战斗未进行时'的对敌伤害,不限本 mod 内容在场

- 位置:`HextechRunes/src/Hooks/Combat/HextechCombatHooks.Pacifist.cs` :: 17-50
- 来源:harmony-compat,置信度 medium

**问题**:ActualDamageCommandPrefix 里 ShouldSuppressPreCombatEnemyDamage 的条件只有两个:CombatManager.IsInProgress!=true 且目标含 Side==Enemy 且 CombatState!=null 的 creature——完全没有检查本局是否有 Mayhem modifier 或任何海克斯内容。命中即 return false 并把 __result 替换为空 DamageResult 序列,且无任何日志。其他 mod 在战斗 setup 阶段(IsInProgress 置位前)或战斗结算尾声(置位清除后)发起的对敌伤害命令会被静默丢弃,其等待的 DamageResult 集合变成空,可能连带破坏它们的后续逻辑判断。这属于'装了本 mod 后原版/他 mod 行为变样'类 bug 的温床,而且因为静默无日志,极难归因到本 mod。

**改法**:两步收紧:(1)在 ShouldSuppressPreCombatEnemyDamage 开头加内容门控——creature.CombatState?.RunState is RunState rs && GetMayhemModifier(rs)!=null(或至少任一玩家持有海克斯符文)才继续判定,否则直接 false;(2)命中抑制时打一条限流 Warn(带 target id 与调用栈首帧),让被吞的第三方伤害至少可诊断。若该守卫是为修某个具体海克斯敌方效果的时序问题,应进一步把条件缩到该效果的来源标记上。

#### F2. [P1][compat] EntropyPower.AfterPlayerTurnStart 被无条件替换:改变原版可选卡集合与 RNG 消耗流,且未按 Storm 先例门控

- 位置:`HextechRunes/src/Hooks/Combat/HextechCombatHooks.PowerCompat.cs` :: 29-86
- 来源:harmony-compat,置信度 medium

**问题**:EntropyAfterPlayerTurnStartPrefix 无条件 return false,连没有任何海克斯内容的普通局也走重实现。对比反编译的 0.109.0 原版:原版 CardSelectCmd.FromHand 传 filter:null、变形用 CardCmd.TransformToRandom(card, RunState.Rng.CombatCardSelection);重实现加了 CanTransformToRandomCard 过滤(改变可选卡集合)、改用 HextechStableRandom 盐值路径(不再推进 Rng.CombatCardSelection 流,装 mod 后同种子局的后续随机序列与原版分叉)。同文件里 Storm 的两个 prefix 都用 ShouldUseHextechStormHandling(Mayhem 在场)门控——这正是记忆中'原版能力重实现丢守卫'教训后的修法,Entropy 漏了同样的门控。同时该 prefix 也压制其他 mod 对该方法的同/低优先级 prefix。

**改法**:与 Storm 对齐:开头加 if (__instance.Owner?.CombatState?.RunState is not RunState rs || GetMayhemModifier(rs)==null) return true;,只在海克斯局启用稳定随机版。若稳定变形在非海克斯联机局也确有需求(需实证某个 desync case),则至少把过滤条件与原版对齐(filter:null + 变形前逐卡判 IsTransformable)并在注释里写明'冻结原版 X 版本实现,升级需 diff'。

#### F23. [P1][compat] RefreshForeground 前缀在无灼烧时也整段替换原版血条渲染

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechBurnHealthBarHooks.cs` :: 71-82, 152-260
- 来源:hooks-ui,置信度 high

**问题**:RefreshForegroundPrefix 只要 TryRenderForeground 成功就返回 false 跳过原方法,而 TryRenderForeground 在生物完全没有 HextechBurnPower(甚至没有任何 DOT)时也走完整重实现并返回 true。结果是 mod 加载后所有生物的每次血条前景刷新都由重实现接管:其他 mod 对 NHealthBar.RefreshForeground 的 prefix 永远不执行(postfix 仍执行但看到的是重实现结果);且原版内部走 IsPoisonLethal/IsDoomLethal 私有判定,重实现是手写比较,游戏小版本改动这两个判定后 mod 会静默偏离原版(正符合 memory 中 vanilla-reimpl 丢内部守卫的既往教训)。这是热门 patch 目标(血条),接管面应最小化。

**改法**:在 TryRenderForeground 开头加最小化早退:int burn = PredictBurnDamage(creature, currentHp); if (burn <= 0) { if (BurnForegrounds.TryGetValue(instance, out var bf) && GodotObject.IsInstanceValid(bf)) bf.Visible = false; return false; } —— 即只有灼烧实际存在时才整段接管,其余帧交回原版(prefix 返回 true)。这样无灼烧场景下其他 mod 的 prefix 与未来版本的原版逻辑均不受影响,行为与现状一致(burn 前景已隐藏)。

#### F24. [P2][compat] 三个光环视觉每帧争抢父节点 index 0,叠加时互相打架

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechBaronAuraHooks.cs` :: 208-214 (另见 HextechSlowCookAuraHooks.cs:267-273, HextechNearDeathFeastVisualHooks.cs:307-313)
- 来源:hooks-ui,置信度 medium

**问题**:HandOfBaronAuraVisual、SlowCookAuraVisual、HextechNearDeathFeastVisual 都把 _root 挂到 creature.GetParent()(战斗房间),并在各自的每帧循环里 EnsureRenderOrder:GetIndex() != 0 就 MoveChildSafely(_root, 0)。玩家同时持有巴龙之手+慢炖(两者均为可共存的玩家符文)或濒死狂宴激活时,两/三个可视根都想占 index 0:每帧 A 移到 0 → B 发现自己 != 0 → 下帧移到 0 → A 再移……形成每帧子节点重排,层叠顺序在两个光环之间逐帧翻转(视觉闪变),同时对同父节点做子序假设的其他 mod(或原版房间逻辑)会看到持续变动的子节点顺序。

**改法**:在房间父节点下建一个共享的 'HextechRunes_BehindCreatures' Node2D 容器(首个视觉创建、挂到 index 0 一次),三个视觉的 _root 都挂进该容器内部,EnsureRenderOrder 只需保证容器本身在 index 0(单一节点无争抢);或退一步把判定放宽为 GetIndex() > 已知本 mod 视觉数量上限时才 Move,消除相互触发。

#### F38. [P2][defense] DualWield 的 RequireField 延迟到攻击执行 prefix 内解析,失败即打断敌方攻击

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechCombatHooks.DualWield.cs` :: 34-36
- 来源:hooks-combat-assets,置信度 high

**问题**:DualWieldAttackCommandExecutePrefix 在首次命中时才 RequireField(typeof(AttackCommand), "_damagePerHit"/"_hitCount");若未来版本重命名私有字段,异常会在 AttackCommand.Execute 的 prefix 里抛出——这是同步敏感路径,战斗中途炸掉敌方攻击比双刀流失效危险得多(联机下两端同炸尚一致,但单端 mod 集导致的 IL 差异可能不一致)。项目其他可选 hook(TryInstallRuneHook、TryPatchAfterPowerAmountChanged)已有'安装期失败→降级禁用'的成熟模式,此处未沿用。

**改法**:把两个 RequireField 移到 InstallDamageCommandHooks(或专用 TryInstall)里安装期解析:解析失败则 Log.Warn 并不安装该 prefix(双刀流退化为普通攻击),prefix 体内只使用已解析的非空 FieldInfo。

#### F49. [P2][compat] BlankCheck/MindOverMatter/ColorDiscovery 手写生成池过滤漏掉 Event 稀有度排除(SingularityAI 前科同类)

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Runes/BlankCheckRune.cs` :: 28-32(另 MindOverMatterRune.cs:22-25、ColorDiscoveryRune.cs:114-118)
- 来源:runes-content,置信度 medium

**问题**:反编译确认 CardFactory.FilterForCombat = CanBeGeneratedInCombat && Rarity 非 Basic/Ancient/Event(+Distinct);而这三处手写过滤只排 Basic/Ancient,漏了 Event,BlankCheck/MindOverMatter 还漏了库内惯例的 CanBeGeneratedByModifiers。CardPoolModel.AllCards 经 ModHelper.ConcatModelsFromMods 接受其他 mod 注入,GetUnlockedCards 不滤稀有度——一旦有第三方 mod 往职业池/无色池注册 Event 稀有度卡,这三个符文就会把它抽进战斗,复刻 SingularityAI 生成禁忌魔典→打出时两端 power 应用分叉→StateDivergence 掉线的已修 bug。同一目录内 SingularityAI/Deadwood/CorruptedBranch 均已改用 FilterForCombat,属同一操作两套写法。

**改法**:三处统一改为 CardFactory.FilterForCombat(pool.GetUnlockedCards(...)).Where(static c => c.CanBeGeneratedByModifiers)(ColorDiscovery 保留其额外条件),与 SingularityAIRune.cs:20-24 完全同款;并在 tests/Program.cs 增加一条守卫测试:反射枚举所有调用 GetUnlockedCards 的符文,断言生成池不含 Event/Ancient/Basic 稀有度。

#### F67. [P2][compat] 选择屏阻塞判定只用类型名含 "Reward" 的字符串启发式,漏掉其他 mod 的奖励/选择 overlay

- 位置:`src/Selection/Coordinator/HextechRuneSelectionCoordinator.Core.cs` :: 242-275 WaitForSelectionBlockingOverlaysToClear / IsSelectionBlockingOverlay
- 来源:selection-sync,置信度 medium

**问题**:进幕选择前等待顶层 overlay 清空的判定是 overlayType.FullName.Contains("Reward"),这对原版奖励屏有效,但其他 mod 的战利品/选择类 overlay(命名不含 Reward,如 Spoils/Loot/中文拼音命名)不会被视为阻塞,海克斯选择屏会直接 Push 压在其交互之上;OnHolderSelected 里 SetInputAsHandled + FullRect Stop MouseFilter 会吞掉下层输入。反编译核验 0.107.1/0.109.0 的 IOverlayScreen 均有 NetScreenType ScreenType 成员,本 mod 自己的屏也自报 NetScreenType.Rewards,说明有类型化信号可用。

**改法**:IsSelectionBlockingOverlay 增加类型化分支:overlay is IOverlayScreen { ScreenType: NetScreenType.Rewards } 时视为阻塞(排除自身 HextechRuneSelectionScreen 的现有判断保留),字符串启发式降为兜底;可再文档化『不阻塞未知 overlay』属有意行为。

#### F85. [P2][compat] 更新提示按界面显示文本定位原版 Label,非中英文语言下静默失效

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Services/HextechUpdateChecker.cs` :: 253-290 (FindVanillaModStatusLabel / IsVanillaModStatusText)
- 来源:platform,置信度 medium

**问题**:挂载点通过遍历场景树找文本含「模组+已加载」或「mod+loaded」的 Label。游戏支持 9 种语言,玩家用日语/韩语等 locale 时永远匹配不到:每次进主菜单空转 30 帧后 Warn 一条,更新提示功能整体消失且玩家无从归因。这也是对原版 UI 文案的隐式耦合——官方改一字提示就断。

**改法**:改为按本地化键匹配:取原版 mod 状态字符串的 loc key(经 LocManager/Tr 得到当前语言文案再比对),或按节点路径/父容器类型定位(NMainMenu 下 ModLoader 状态 Label 的固定挂载点),文本匹配仅留作最后回退。

## 批次 4:公共 API 契约

第三方注册接口的 fail-fast 与冲突可见化。改动涉及对外行为,注意保持既有成功路径完全不变。

#### F83. [P1][compat] 公共 API 晚注册在池冻结后抛异常且留下半注册状态

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Api/HextechRunesApi.cs` :: 19-38 (RegisterPlayerRune Type 重载; RegisterEventRelic 46-57、RegisterForge 64-75 同构)
- 来源:platform,置信度 high

**问题**:三步注册顺序是 ExternalContentRegistry 落表 → InjectModelType(net-id 表) → RegisterPlayerRuneModels(ModHelper.AddModelToPool)。反编译核验:游戏 ConcatModelsFromMods 首次取池即置 isFrozen=true,此后 AddModelToPool 直接 throw InvalidOperationException("too late")。第三方拓展包在游戏初始化后才调用本 API 时,异常从第三步抛出,但前两步已生效:该类型进了 catalog 查找与 SavedProperty net-id 表(还会在 0.109 分支追加尾部 net-id),却永远不进遗物池。若调用方 catch 后继续,得到一个'存在但抽不到'的幽灵符文,且 net-id 表被单方面污染——两端拓展包时序不同时正是 1014 的温床。

**改法**:把 RegisterModelsInPool(会抛的操作)挪到三步之首做 fail-fast;或在 RegisterPlayerRune 内 catch 冻结异常后回滚 ExternalContentRegistry 条目并 rethrow 带明确指引的异常("必须在游戏初始化前注册,参见 InjectModelType 的时序告警")。同时在 API 的 xml-doc 里写明注册窗口契约。

#### F86. [P2][compat] 外部内容重复注册时冲突参数被静默丢弃

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Content/HextechExternalContentRegistry.cs` :: 23-72 (RegisterPlayerRune/RegisterEventRelic/RegisterForge)
- 来源:platform,置信度 high

**问题**:同一类型重复注册时只更新 assetModId,首次注册的 rarity/flags/characterPool/tagKey 静默保留,无任何日志。第三方误用场景(两个拓展包注册同名类型、或热重载式重复调用不同参数)得到的是'看似成功、参数被忽略'——对外 API 三大误用面(重复/晚/坏类型)中,坏类型抛 ArgumentException、晚注册有告警,唯独重复注册完全无声。

**改法**:重复注册且 registration 参数与已存条目不等时 Log.Warn 一条(带两组参数与调用方程序集名);参数完全相同的幂等重复保持静默。

## 批次 5:防御可见化(方向是"让失败可见",不是删防御)

静默 catch 补日志、限流策略改进、状态清理。凡标注"降级说明"的条目按修正后的 suggestion 执行,不要按 detail 里的原始思路改。

#### F7. [P3][defense] CardModel.SpendResources 写入的 Pending 静态字典缺兜底清理,取消/异常路径会永久滞留条目

- 位置:`HextechRunes/src/Hooks/Combat/HextechCombatHooks.PlayCost.cs` :: 12-16,52-66,119-135
- 来源:harmony-compat,置信度 medium

**问题**:CardSpendResourcesPrefix 对每次出牌无条件写 PendingManualPlayEnergyValues/PendingManualPlayResourceSpends(static Dictionary<CardModel,...>),仅在随后的 CardOnPlayWrapperPrefix 里 Remove。若 SpendResources 之后 OnPlayWrapper 未发生(出牌被取消、上游异常、其他 mod 短路了 OnPlayWrapper),条目滞留:强引用 CardModel 跨战斗/跨局泄漏,且同一卡实例下次出牌会消费到上一次的过期快照(能量/星费错值进入 GetResourceSpendForCurrentCardPlay 的数值计算)——两端触发时机不同时是潜在分叉源。Active* 两个字典有 finally 兜底,Pending* 没有对称保障。

**改法**:在已有的 RunLifecycle 钩子(StartRunPrefix/RunEndedPostfix)以及战斗结束路径统一 Clear() 四个字典;另可把 Pending* 改为 ConditionalWeakTable 或在 CardOnPlayWrapperPrefix 之外加一个 CombatManager 回合边界清理,保证'取消的出牌'不留过期快照。

#### F11. [P3][defense] 远端选项重建失败静默回退本地选项+同步索引=分叉制造机

- 位置:`Selection/Coordinator/HextechForgeSelectionCoordinator.cs` :: 166-176(同型:Selection/Coordinator/HextechRelicOptionSelectionCoordinator.cs:156-165、Selection/Coordinator/HextechRuneSelectionCoordinator.Selection.cs:315)
- 来源:defense-audit,置信度 medium

**问题**:重建远端玩家选择时,若同步过来的 optionIds 在本端 ModelDb 加载失败(两端内容集不一致才会发生),catch 后仅 Warn 并回退到本端本地生成的 fallbackOptions,再用同步来的 selectedIndex 去索引——两份列表内容不同时,同一 index 映射到不同遗物,观察端与选择端各自授予不同遗物,checksum 分叉被推迟到下一场战斗才由游戏 StateDivergence 爆出,日志里只剩一条 Warn,极难归因。这正是「catch 返回默认值继续参与游戏状态、两端不一致」的典型:带病运行比当场可见失败更危险。触发前提已被 HextechMultiplayerCompatibilityHooks 的网络签名护栏拦掉大半,故不评 P0。

**改法**:降级说明:对抗验证证明触发条件实为两端内容集真不匹配(malformed 已在 codec 层被单独分支拦截),该状态下任何恢复策略都必然分叉,catch 只是推迟检测而非制造分叉。仍可做:回退时把 Warn 升级为 Error 并带上无法解析的 ModelId 列表,便于定位是哪个 mod 的内容缺失;逻辑不动。

#### F13. [P2][defense] 配置读失败一律用默认值覆写用户配置文件:瞬时 IO 错误=全配置丢失

- 位置:`Config/HextechRuneConfiguration.cs` :: 356-382(同型:Hooks/UI/HextechRelicVisibilityHooks.Config.cs:101-129)
- 来源:defense-audit,置信度 high

**问题**:LoadConfig 的 catch(Exception) 把「JSON 真损坏」和「文件被占用/权限/杀软锁等瞬时 IOException」混为一谈,catch 路径立即 CreateDefaultConfig()+SaveConfig() 落盘——一次瞬时读失败就把用户手工调好的整份禁用清单/权重配置永久清掉,且只有一条 Warn。这是「catch 后带病继续」变体:数据丢失被防御本身制造。发生概率低,但代价是不可逆的用户数据损失。

**改法**:分型处理:catch (JsonException) 时先把原文件复制为 .corrupt.bak 再重建落盘;catch (IOException) 时本次会话用内存默认值但不 SaveConfig,留待下次启动重读。

#### F14. [P2][defense] DoubleVision 四处 TrySync* 吞掉同步广播失败,分叉被降级成一条 Warn

- 位置:`Runes/DoubleVisionRune.cs` :: 1026-1107
- 来源:defense-audit,置信度 medium

**问题**:TrySyncObtainedCard/Gold/Potion/Relic 包住 RewardSynchronizer.SyncLocalObtained* 的 catch(Exception) 只 Warn:一旦广播真失败,本端已把复制奖励落进状态而远端不知情,后续 checksum 必然分叉——但日志层面这只是一条与无害 UI 告警同级的 Warn,分诊时几乎必被当噪声跳过(与既往「联机分叉难归因」的教训同构)。此处不能改成 rethrow(奖励已落地,抛出只会把静默分叉换成任务链 fault 的另一种分叉),但可见度必须提。

**改法**:catch 保留,日志升为 Log.Error 并统一加可检索标记(如 [DoubleVision][DESYNC-RISK]),写明「本端已获得复制奖励但同步广播失败,联机可能分叉」;有遥测通道的话计一个 desyncRisk 计数。

#### F17. [P2][defense] 存档恢复 catch 完全静默:解析失败即清空玩家状态且无任何日志

- 位置:`Mayhem/HextechMayhemActState.cs` :: 379-382(同型:Runes/SolidTimeRune.StoredCards.cs:31-46, 93-100)
- 来源:defense-audit,置信度 high

**问题**:RestoreMonsterHexesByAct 的裸 catch 把敌方海克斯按幕列表静默重置为空,SolidTimeRune 的 GetRemovedCards 把存储的 power 卡静默还原为空表——玩家跨存档状态丢失后毫无日志线索,bug report 分诊时只能看到「海克斯没了/卡没了」的结果。两端解析的是同一份同步/存档串,失败对称,分叉风险低;主要危害是 (a) 类静默吞掉数据损坏证据。

**改法**:两处 catch 各加一条 Log.Warn,带异常类型与 json 前 80 字符截断,便于从玩家日志直接定位损坏来源;行为(重置为空)保持不变。

#### F18. [P3][defense] 计数限流 10 处「前 N 条后永久静默」且静态计数器跨局不复位

- 位置:`Hooks/UI/HextechUiSafetyHooks.cs` :: 279, 287, 295(同型:HextechAnimTriggerSafetyHooks.cs:32、Hooks/Combat/HextechCombatHooks.DualWieldIntent.cs:100、Hooks/Assets/AssetHooks.cs:425、Hooks/Combat/HextechCombatVfx.cs:117、Hooks/UI/HextechPlayerStatsHoverHooks.cs:61、Hooks/Compat/HextechGameOverCompatibilityHooks.cs:51、Hooks/Combat/HextechCombatHooks.JeweledGauntlet.cs:317)
- 来源:defense-audit,置信度 high

**问题**:全部 10 处限流均为静态 _xxxLogs++ < N,超限后该路径在整个进程生命周期完全静默:第一局刷满 10 条无害告警后,第三局出现的同 hook 新问题(可能换了触发根因)不留任何痕迹——对以玩家日志为主要取证手段的联机 mod 是实打实的诊断盲区。仓库已有更优模式(AssetHooks.WarnedTextureMissPaths / Style.cs WarnDisplayTextureFallbackOnce 的每路径一次)。

**改法**:择一统一:StartRunPrefix(Hooks/RunLifecycle/HextechRunLifecycleHooks.StartRun.cs)里集中复位这些计数器(每局各给 N 条预算);或改为按「hook+异常类型」每组合一次的 HashSet 模式。

#### F42. [P3][defense] HextechCreatureNodeRegistry 无战斗生命周期清理,靠 >24 阈值惰性回收

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechCombatVfx.cs` :: 88-152
- 来源:hooks-combat-assets,置信度 high

**问题**:entity→node 字典只在 Register 时 Count>24 才 Prune 失效项,战斗结束后最多 24 条 Creature→已释放 NCreature 的映射滞留,强引用把上一场战斗的 Creature 及其状态图钉在内存里直到下一场注册满 24。TryGet 有 IsInstanceValid 校验所以不产生错误行为,纯内存滞留;但'惰性阈值'纪律与本 mod 其他静态状态(SlipperyReductionsByCommand 按命令清理)不一致。

**改法**:在 CombatRoomReadyPostfix 开头(新战斗房间就绪时)先 Nodes.Clear() 再注册当前 CreatureNodes,即可移除 24 阈值与 Prune 逻辑。

#### F43. [P3][defense] PlayCost 的 Pending 字典与 PendingInstantDeathDoomKills 缺战斗级重置

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechCombatHooks.PlayCost.cs` :: PlayCost.cs:13-16; Outbreak.cs:12-14
- 来源:hooks-combat-assets,置信度 medium

**问题**:PendingManualPlayEnergyValues/PendingManualPlayResourceSpends 在 SpendResources prefix 写入、OnPlayWrapper prefix Remove 消费;若打出流程在两者之间被中断(异常/联机 rejoin/取消),条目跨战斗滞留,同一 CardModel 下次打出会消费上次的陈旧能量值(X 费牌尤其危险,影响返还/回基本功判定)。PendingInstantDeathDoomKills 静态列表同理:响应链异常中止后 Creature 滞留,虽然 flush 时的 IsAlive+Doom 条件大概率兜住,但引用跨战斗存活。两者都没有战斗开始/结束的清理点。

**改法**:在战斗开始 hook(如 CombatRoomReadyPostfix 或既有 run 生命周期钩子)统一 Clear 这四个字典/列表;成本一行,消除整类陈旧状态。

#### F48. [P3][defense] Forge 权重/禁用集/价格的 catch{} 回落到本机配置,联机两端可取到不同数值

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Forges/HextechForgeGrantHelper.cs` :: 439-455, 463-479(另 HextechForgeShopPriceHelper.cs:5-21)
- 来源:runes-content,置信度 medium

**问题**:GetBaseForgeRarityWeights 与 GetEffectiveDisabledForgeIds 用无类型 catch{} 静默回落到 HextechRuneConfiguration.GetSnapshot()/GetDisabledForgeIds()——这是每台机器各自的本地配置。若 RunState/Modifiers 访问在某一端异常(rejoin 窗口、初始化竞态)而另一端正常,两端会以不同的稀有度权重/禁用集喂给 HextechStableRandom,同一 roll 落到不同档位→选森池不同→发放分叉;这是典型的"catch 返回默认值继续参与数值计算"带病运行,比可见失败更危险。文件自己的注释(129-135 行)已为禁用集设了 ObtainSelectedForge 落地复核安全网,但权重与商店价格没有对应兜底。

**改法**:降级说明:对抗验证反编译证明所有声称的抛点均不存在(Player.RunState 返回 NullRunState.Instance 永不抛/不为 null 等),分叉论证不成立。仍可做的低价值清理:三处 catch{} 收窄为具体异常类型并加限流 Log.Warn,让未来真实异常可见;不需要改回退值来源。

#### F51. [P2][defense] DoubleVision 事件遗物复制逐条 catch 吞异常,联机可产生"一端有复制份另一端没有"

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Runes/DoubleVisionRune.cs` :: 371-396
- 来源:runes-content,置信度 medium

**问题**:CommitEventRelicIntent 对每个 DuplicateObtainedRelic 调用 catch(Exception) 后仅 Warn 继续。事件事务按注释"在所有端顺序提交"且此路径 syncReward:false(不走广播兜底),两端各自独立执行复制;若某端复制中途抛(如某原版遗物 AfterObtained 在客机因表现层状态差异失败)而另一端成功,则遗物列表从此分叉且无 reconciliation——正是"两端 catch 触发时机不同反而制造分叉"的形态。原始奖励已落地,此 catch 保护的只是附赠复制份,吞掉后表面无恙实则埋下 checksum 雷。

**改法**:catch 后不要仅记日志:将失败升级为确定性决策——复制前先做两端一致的可行性预检(模型已注册、非交互式 AfterObtained 白名单),预检不过两端都跳过;真异常时改走与 TryRecoverExternalEventRelicObtain 同款的"保遗物不保效果"路径,保证两端最终遗物集合一致,或至少把该遗物 id 广播为"本次放弃复制"让远端同步跳过。

#### F52. [P2][defense] NearDeathFeast 在同步路径 fire-and-forget 力量补发,异常不可见且时序脱锚

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Runes/NearDeathFeastRune.cs` :: 134, 173
- 来源:runes-content,置信度 medium

**问题**:LoseHpAllowingDying/PreserveNegativeHpAsDyingState 是被 Harmony hook 调用的同步方法,内部用 `_ = rune.SyncNearDeathStrength();` 丢弃 Task——PowerCmd.Apply<StrengthPower> 的异常完全无人观察(连 Warn 都没有),且力量应用相对后续伤害结算的时序交给调度器,与 226 行同一方法被 await 的用法形成两套语义。异常真发生时静默丢力量层数,两端各自静默,属"带病运行"候选;可见失败(至少日志)明显更利于排查。

**改法**:改用工程内既有的 TaskHelper.RunSafely(rune.SyncNearDeathStrength())(FlyingKickCorpseLaunchDriver.cs:42 同款),保证异常被记录;并加中文注释说明为何此处不能 await(同步 hook 上下文)。

#### F55. [P3][defense] TryGetField 反射失败零日志,SweepingBlade 类效果跨版本静默失效

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Helpers/HextechHookReflection.cs` :: 41-44
- 来源:runes-content,置信度 high

**问题**:TryGetField/TryGetMethod 返回 null 时无任何告警,SweepingBladeRune 的 AttackCommand._singleTarget/_combatState(7-8 行)一旦被游戏更新改名,符文核心效果(改为全体攻击)整段无声消失,玩家只会报"符文没效果"而日志无线索;NearDeathFeastRune.SetDamageResultValue(296-311 行)的 field?.SetValue 同样静默,一旦 DamageResult 成员改名,WasTargetKilled 恒为 false,击杀触发链静默哑火。降级本身该保留(两端同版本、确定一致),缺的是可见性。

**改法**:参照 HextechRelicBase.GetResolvedIconPath 的 WarnedMissingIconPaths 模式:TryGetField/TryGetMethod 增加带 static HashSet 去重的一次性 Log.Warn("[HextechRunes][Reflection] missing member X.Y, feature degraded");NearDeathFeast.SetDamageResultValue 在 property 与三种 field 全 miss 时同样 Warn 一次。

#### F56. [P3][defense] SolidTime 存档 JSON 解码失败静默清空玩家存牌,应升为可见失败

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Runes/SolidTimeRune.StoredCards.cs` :: 25-34
- 来源:runes-content,置信度 high

**问题**:DecodeStoredCards 的 catch { return []; } 面向持久化 SavedProperty 字符串,防解码本身正当(类别 h,且两端字符串一致故确定一致),但损坏存档会让玩家静默丢失全部存入的能力牌且无日志可查——继续运行没有比可见失败更危险,但完全静默让"存的牌不见了"类玩家反馈无法归因。

**改法**:catch(Exception ex) 时增加 Log.Warn($"[HextechRunes][SolidTime] stored cards json corrupted, dropping {_removedCardsJson.Length} chars: {ex.GetType().Name}") 再返回 [],一行改动。

#### F64. [P2][defense] ShouldDirectlyGrantRandomForge 的空 catch 回退到本地配置,破坏注释声称的『双端要么都短路要么都不短路』不变量

- 位置:`src/Selection/Coordinator/HextechForgeSelectionCoordinator.cs` :: 201-217 (同类:src/Selection/Pool/HextechRunePoolBuilder.cs:268-288 GetEffectiveDisabledPlayerRuneIds)
- 来源:selection-sync,置信度 medium

**问题**:被 catch 包住的只是 player.RunState 模式匹配 + Modifiers.OfType().LastOrDefault(),纯内部逻辑,异常必为 bug;catch 后回退到 HextechRuneConfiguration.GetSnapshot().RandomForgeDirectGrant——这是各端各自的本地配置。SelectForge 顶部注释明确以『配置经 RunConfigurationSnapshot 跟随主机,双端要么都短路要么都不短路』作为免同步直发的安全前提,而这个 catch 恰好在异常单端触发时让一端走本地配置:一端直发、另一端弹三选一,奖励流两端行为分叉。带病运行比可见失败更危险——若真发生宁可抛出让外层 reward 流可见失败。GetEffectiveDisabledPlayerRuneIds 的 catch 同理:client 正常路径刻意返回空集,catch 路径却返回本地禁用配置,方向相反。

**改法**:删除这两处 try/catch(异常必为 bug,应冒泡可见);若要保底,catch 后回退到与网络类型无关的确定常量(RandomForgeDirectGrant→false、disabledIds→空集)并 Log.Error,绝不回退到本地配置。

#### F65. [P3][defense] 远端符文选择超时回退 options[0](COMPENSATION_RUNE 日志的出处),瞬时 IsConnected 抖动窗口内两端获得不同遗物

- 位置:`src/Selection/Coordinator/HextechRuneSelectionCoordinator.Selection.cs` :: 231-244 CreateRemoteRuneChoiceFallback(触发链 71-83、131-143)
- 来源:selection-sync,置信度 medium

**问题**:该回退仅在 ShouldKeepWaitingForRemoteRuneChoice 返回 false(run 变更或 IsMultiplayerConnected()==false)时可达:真断线场景下靠 rejoin 从 host 快照重同步兜底、run 变更场景无害;危险窗口是 IsConnected 短暂抖动但双方都未离开对局——本端为远端玩家 fabricate options[0] 并 RelicCmd.Obtain,选择端稍后应用自己的真实选择,双方继续同局,下一场战斗 checksum 分叉。BR 日志中 timeout fallback: selected=COMPENSATION_RUNE 即此路径(记忆中该案例真点火器是 rejoin 超时,但 fabricate 本身仍是隐患)。回退还把 RerollCount=0 记入遥测,污染 RecordRuneChoice 数据。

**改法**:降级说明:传输层反编译证明 _isConnected 无瞬时抖动语义,"抖动窗口两端不同遗物"被驳倒。仍可做:回退发生时的日志已存在,补充记录 options[0] 的 relic id 与当时的连接状态快照,便于事后归因;逻辑不动。

#### F66. [P2][defense] 远端 Forge/RelicOption 选择 malformed/越界时静默取第一项继续,属『带病运行』

- 位置:`src/Selection/Coordinator/HextechForgeSelectionCoordinator.cs` :: 151-179 ResolveRemoteForgeChoice(同构:HextechRelicOptionSelectionCoordinator.cs:141-169)
- 来源:selection-sync,置信度 medium

**问题**:TryDecode 失败或 selectedIndex 越界时,接收端 Log.Warn 后返回 fallbackOptions[0] 继续发放;发送端发放的是自己的真实选择。这两种失败只会在版本偏斜或 bug 时出现,此时静默给第一项让双方各拿不同遗物继续跑,分叉被推迟到 checksum 才爆且难以归因;可见失败(抛出让外层流程中止、该次奖励保持未解析可重试)反而两端对称且可自愈——与 HandleActSelection R2 注释里的自愈哲学一致。注意区别:同文件对『synced option 模型加载失败回退本地 options』的降级是合理的(有 Warn、且 selectedIndex 语义仍对齐),问题仅在 malformed/越界两个分支。

**改法**:malformed payload 与 index 越界分支改为抛 InvalidOperationException(或返回 null 让调用方按『选择未完成』处理),Log.Error 带上完整 payload dump;保留 optionIds 加载失败回退本地 options 的现有降级。

#### F73. [P3][defense] codec 旧版 ordinal 解码失败时三处契约不一致:两处 true+空列表、一处 false

- 位置:`src/Selection/Sync/HextechChoiceCodec.cs` :: 447-453 (TryDecodeRuneSelectionFinalOptions), 570-577 (TryDecodeForgeSelection), 508-514 (TryDecodeRandomRuneGrant)
- 来源:selection-sync,置信度 medium

**问题**:同样是『legacy ordinal 超界』失败,RuneSelection 与 ForgeSelection 清空列表后 return true(调用方转入确定性重放/本地回退),RandomRuneGrant 却 return false(调用方视为非本协议消息)。前两者的 true+空列表把『解码失败』伪装成『无附带选项』,调用方无法区分旧 payload 与坏 payload;三处行为差异无注释说明是否刻意。

**改法**:统一契约:legacy ordinal 失败一律 return false(消息不可信),或至少在三处各加一行注释说明为何取不同策略;RuneSelection 路径失败时调用方已有确定性重放回退,改 false 不损失功能。

#### F75. [P2][defense] NearDeathFeast 力量补发:先记账后施加,catch 吞异常不回滚,失败即账实分离

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/EnemyHexes/HextechEnemyNearDeath.cs` :: 203-227
- 来源:enemyhex-mayhem,置信度 medium

**问题**:SyncStrength 在 await PowerCmd.Apply<StrengthPower> 之前就把 NearDeathFeastEnemyStrength[combatId] 写成目标值 debt(220 行),随后 catch(Exception) 只 Log.Warn 就返回。若 Apply 抛异常,账面 granted 已等于 debt,后续调用因 delta<=0 永不重试,敌人力量永久少补;且该方法是 _ = SyncStrength(...) 火后不理地从伤害结算路径(LoseHpAllowingDying:87)发起,若异常只在一端发生(如表现层诱因),两端 StrengthPower 层数直接分叉进 checksum——这正是'catch 后带病运行比可见失败更危险'的典型:继续运行制造静默分叉,可见失败反而两端一致可诊断。

**改法**:catch 块内把账本回滚:记录 int previousGranted = granted,catch 里 tracking.NearDeathFeastEnemyStrength[combatId] = previousGranted(保留 Log.Warn),让下一次 SyncStrength 自动重试补差额。不建议改成先 Apply 后记账——先记账可能是防 Apply 触发的钩子链重入导致双重补发,回滚式修改能同时保住这层防重入语义。

#### F76. [P2][defense] ActState JSON 恢复是全模组唯一无日志的裸 catch,静默清空敌方海克斯状态

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Mayhem/HextechMayhemActState.cs` :: 356-383
- 来源:enemyhex-mayhem,置信度 high

**问题**:RestoreMonsterHexesByAct 的 catch 不带异常变量、不打任何日志,直接把 _monsterHexesByAct 重置为空——玩家读档后所有敌方海克斯凭空消失且日志零线索。同类兄弟路径全都 fail-visible:RestoreRunConfigurationSnapshot(RunConfiguration.cs:187)、HextechMayhemCombatTrackingState.Restore(Serialization.cs:22)、HextechPlayerRuneConfigSnapshotState.TryRestore(63 行)都 catch(Exception ex)+Log.Warn。该 SavedProperty 字符串两端加载内容一致、System.Text.Json 解析确定,不构成联机分叉,但排查'海克斯消失'类玩家反馈时这里是唯一黑洞。

**改法**:改为 catch (Exception ex) { _monsterHexesByAct = NewMonsterHexLists(); Log.Warn($"[{ModInfo.Id}][Mayhem] Monster hexes by act restore failed; state cleared: {ex.Message}", 2); },与兄弟路径的日志格式对齐。

#### F79. [P3][compat] 单例效果实例持有跑局态:NatureIsHealing 的 _modifier/_timer 与 Compensation 的 _pendingCompensations 缺少 run 级重置

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/EnemyHexes/NatureIsHealingEnemyHex.cs` :: 10-12, 46-88(另见 CompensationEnemyHex.cs:5-7)
- 来源:enemyhex-mayhem,置信度 medium

**问题**:HextechEnemyHexEffects.OrderedEffects 里的效果实例是进程级单例,但 NatureIsHealingEnemyHex 在实例字段存 _timer/_modifier(整个 RunState 强引用),CompensationEnemyHex 在实例字段存含 Creature 引用的 _pendingCompensations——这与'状态全进 CombatTracking'的项目纪律相悖。清理依赖 AfterCombatEnd/下次激活时的钩子:战斗中途弃局回主菜单后,若后续跑局不再抽到该海克斯,旧 RunState/Creature 引用被无限期持有(内存滞留)。行为正确性有防护(NatureIsHealing 定时器下次 tick 经 TryGetAliveEnemies 检查 CombatManager.IsInProgress 后自停;Compensation 的 TryTake 按 Creature 引用相等匹配不会串局),所以只是滞留不是错误。附带:CompensationEnemyHex 的 static HashSet<CompensationEnemyHex> EffectsWithPendingCompensation 永远最多 1 个元素(单例),是不必要的集合抽象。

**改法**:在 HextechMayhemModifier.ResetForNewRun/ResetCombatTracking 链上加一个广播(如 HextechEnemyHexEffects 提供 static void ResetAllRunScopedState() 逐效果调用 virtual ResetRunScopedState()),NatureIsHealing 在其中 StopTimer()、Compensation 清 _pendingCompensations;EffectsWithPendingCompensation 简化为 private static CompensationEnemyHex? _instanceWithPending 或干脆直接判 _pendingCompensations.Count。

#### F84. [P2][defense] SaveConfig 磁盘写入无保护,且位于损坏回退的 catch 路径内

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Config/HextechRuneConfiguration.cs` :: 751-756 (SaveConfig), 344-365 (LoadOrCreateConfig)
- 来源:platform,置信度 high

**问题**:SaveConfig 的 Directory.CreateDirectory + File.WriteAllText 完全未捕获。LoadOrCreateConfig 里'配置读损坏→回默认'的 catch 分支自身又调用 SaveConfig:当故障根因是磁盘(只读目录、磁盘满、杀软锁文件)而非 JSON 损坏时,写异常从 catch 内逃逸,沿 EnsureLoaded 传进首个触发懒加载的 getter——包括 GetSnapshot 这类 run 启动/联机快照路径。此处可见失败比继续运行更危险的判断不成立:内存内默认配置完全可用、两端行为不受落盘影响,写失败只该是日志。

**改法**:SaveConfig 内套 try/catch,失败 Log.Warn($"[RuneConfig] Config write failed: {ex.Message}") 后返回;内存 _config 照常生效。Telemetry 的 EnsureConfigFile(HextechTelemetry.Config.cs:26-38 写文件同样裸奔,但外层 Initialize 已兜)可顺手统一。

## 批次 6a:死防御删除(真正可以删的,约 14 处,零风险)

#### F19. [P3][defense] 双层死 catch:GetCollapseEnemyHexesConfig 捕不到现实异常、兜底也兜不住

- 位置:`Hooks/UI/HextechEnemyUi.cs` :: 127-135
- 来源:defense-audit,置信度 medium

**问题**:GetCollapseEnemyHexes() 只读内存字段,其配置加载链(HextechRelicVisibilityHooks.Config.cs LoadConfig)自身已全量 catch 不外抛;唯一可能的异常是该静态类初始化失败的 TypeInitializationException,而那种情况下 catch 里调的 GetDefaultCollapseEnemyHexes() 在同一个类上、同样会抛——即该 catch 既捕不到现实异常,兜底路径在唯一能触发的场景下也必然失败。(c) 类纯噪声层。

**改法**:删除 try/catch 直接 return HextechRelicVisibilityHooks.GetCollapseEnemyHexes()。删除后若异常真发生(类型初始化失败),外层 EnemyUi.Refresh 的同步保护 catch(51 行,必要保留)会兜住并 Warn,行为不变但少一层假安全。

#### F20. [P3][defense] OperatingSystem.IsAndroid() 包 try/catch:BCL 纯查询不抛异常

- 位置:`Compat/HextechRuntimeRuneCompatibility.cs` :: 11-18(同型:Bootstrap/HextechModelPoolRegistrar.cs:113-118)
- 来源:defense-audit,置信度 high

**问题**:OperatingSystem.IsAndroid() 是无参平台查询,任何目标框架上都不抛异常,catch→false 是 (a) 类死防御;两处重复。删除后若真有异常(理论上不可能)会在启动期可见爆出,比静默 false 把 Android workaround 关掉更利于发现问题。

**改法**:两处删 try/catch 直接 return OperatingSystem.IsAndroid();顺带二者可合并为一个共享属性。

#### F29. [P3][defense] 对纯字段读取的 try/catch 过度防御

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechEnemyUi.cs` :: 123-133
- 来源:hooks-ui,置信度 high

**问题**:GetCollapseEnemyHexesConfig 用 try/catch 包裹 HextechRelicVisibilityHooks.GetCollapseEnemyHexes(),而后者只是 return _config.CollapseEnemyHexes——_config 在字段初始化器给了 new(),Install 再赋 LoadOrCreateConfig() 结果,任何时刻都非 null,读取不可能抛。按判定框架属 (a) 纯内部逻辑 catch + (d) 对不可能失败的东西设防;吞掉的异常若真发生只能是本 mod 自身 bug,静默回退默认值反而掩盖它(两端 UI 折叠状态还可能不一致,但纯本地 UI 无同步影响)。调用方 RefreshInternal 外层已有 Refresh 的整体 catch 兜底,这层是多余的第三层。

**改法**:删除 try/catch,直接 return HextechRelicVisibilityHooks.GetCollapseEnemyHexes();(方法可整体内联进 RefreshInternal 的调用点)。

#### F45. [P3][convention] CombatVfx 结构总体健康;唯一冗余是 RunSafely 与内部 try/catch 双层兜底

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechCombatVfx.cs` :: 359-403, 406-551
- 来源:hooks-combat-assets,置信度 high

**问题**:1343 行实为三个内聚类(hook 安装/节点注册表/特效实现),纯视觉路径全部走 CallDeferred+位置快照、不读写同步状态,防御密度与场景匹配,不建议大拆。唯一冗余:BoomerangSweep/OmegaJudgment/FlyingKickStrike/CorpseBloomBurst/QuantumPulse 的入口用 TaskHelper.RunSafely 包裹,而对应 RunXxx 内部又各有顶层 try/catch(Log.Warn)——双层兜底,外层 RunSafely 实际永远收不到异常;DeathRingLash/DivinePulse/SoulDrain 则只有内层。属判定框架 (c) 多层重复 catch。

**改法**:统一为单层:保留各 RunXxx 内部 try/catch(日志信息更具体),入口的 TaskHelper.RunSafely 换成直接丢弃 Task(或统一都走 RunSafely 并删内层),二选一并在文件头注释约定。若想降行数,可把 SpawnRing/SpawnFlash/SpawnBeam/SpawnSoulWisp/纹理工厂等约 350 行通用原语拆成 HextechCombatVfx.Primitives.cs partial,非必须。

## 批次 6b:规范基建(重复合并、缩进归一、.editorconfig)

#### F16. [P2][convention] isClient 探测「取 NetService 裸 catch 回退」模式复制约 9 处

- 位置:`Mayhem/HextechMayhem.PlayerRuneConfig.cs` :: 41, 59(同型:Mayhem/HextechMayhem.RunConfiguration.cs:71,110、Selection/Pool/HextechRunePoolBuilder.cs:282、Forges/HextechForgeGrantHelper.cs:449,473、Forges/HextechForgeShopPriceHelper.cs:15、Hooks/UI/HextechRuneConfigMenuHooks.cs:1786、Selection/Coordinator/HextechForgeSelectionCoordinator.cs:211)
- 来源:defense-audit,置信度 high

**问题**:「RunManager.Instance.NetService.Type == NetGameType.Client,catch → 回退本地配置」这一片段以裸 catch 形式复制了约 9 处,注释雷同;仓库已有 HextechPlayerContextHelper.IsNetworkMultiplayerRun() 这一集中式先例却未复用。分散复制的真实维护成本:未来若 NetService 语义变化(如 0.110 改 API),要改 9 处且漏一处就是主客配置不对称——而这正是本 mod 档案里 SavedProperty 不对称分叉的同族问题。

**改法**:在 HextechPlayerContextHelper 增加 IsClientRun()(内部 RunManager.Instance?.NetService 空条件链 + catch 收窄为 NullReferenceException,唯一现实异常源是未初始化单例),9 处调用点统一替换。

#### F22. [P3][convention] GetDefaultTransformationOptions 可变换性探测片段在两文件完全重复

- 位置:`EnemyHexes/MysteryEnemyHex.cs` :: 68-77(与 Hooks/Combat/HextechCombatHooks.PowerCompat.cs:76-85 逐字同型)
- 来源:defense-audit,置信度 high

**问题**:「CardFactory.GetDefaultTransformationOptions(card, card.CombatState != null).Any() + catch(InvalidOperationException)→false」在两处逐字重复。该判定参与卡牌可变换资格(游戏数值路径),未来若需要修 catch 类型或对齐 FilterForCombat 语义(参见既往 SingularityAI 漏过滤的教训),必须记得改两处。

**改法**:抽成共享 helper(如 HextechCardTransformHelper.CanTransform(CardModel)),两处调用;catch 类型与语义只维护一份。

#### F26. [P2][convention] 隐藏UI勾选框与属性系数悬浮行硬编码中文,绕过 9 语言本地化管线

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechRelicVisibilityHooks.ToggleUi.cs` :: 42, 75 (另见 HextechPlayerStatsHoverHooks.cs:9-12)
- 来源:hooks-ui,置信度 high

**问题**:ToggleUi 的 Label.Text="隐藏 UI" 与 TooltipText="隐藏遗物栏和联机玩家状态条…" 是硬编码中文;HextechPlayerStatsHoverHooks 的四个系数标签(生命系数：/伤害系数：/格挡系数：/治疗系数：)同样硬编码中文并直接拼进原版立绘悬浮说明,英/日/韩等其余 8 个语言的用户会在战斗 UI 里看到中文。而 assets/localization/ 下已有 9 语言目录且 relic_collection.json 里同功能的配置项文案(HEXTECH_SHOW_HIDDEN_RELICS_TOGGLE_*)是全语言齐备的——这是管线内的漏网,不是缺基建。注意 PlayerStatsHover 的中文前缀还兼作 RemoveExistingCoefficientLines 的去重匹配键,换 LocString 后去重逻辑要同步用本地化后的前缀匹配。

**改法**:在 relic_collection(或 gameplay_ui)表新增 HEXTECH_HIDE_UI_LABEL、HEXTECH_HIDE_UI_TOOLTIP、HEXTECH_STAT_COEFF_HEALTH/DAMAGE/BLOCK/HEALING 六个键,走 tools/sync_content_txt.py 既有同步流程补齐 9 语言;代码侧 ToggleUi 用 new LocString(LocTable, key).GetRawText(),PlayerStatsHover 把四个 const 改为静态属性读 LocString,IsCoefficientLine 用同一属性值做 StartsWith 匹配。

#### F33. [P3][compat] 帮助方法 AwaitProcessFrameAsync 在两处重复实现

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechRuneConfigMenuHooks.cs` :: 2683-2698
- 来源:hooks-ui,置信度 high

**问题**:与 Services/HextechUpdateChecker.cs:317 的同名方法逐行相同(IsInstanceValid+IsInsideTree 前置、GetTree 判空、ToSignal(ProcessFrame)、事后复验)。这是 Godot 节点生命周期竞态的关键防线(属于必须保留的 (i) 类防御),两份拷贝将来若只修一份(例如补 tree 在 await 期间失效的场景)会产生行为分叉;归 convention 的重复 helper,也顺带影响所有依赖它的 UI 注入路径的健壮性一致性。

**改法**:移到共享静态类(如 HextechGodotAsync.AwaitProcessFrameAsync),两处调用之;拆分大文件时一并处理。

#### F39. [P2][convention] Draw.cs / Healing.cs 缩进错乱(tab 深度中途漂移)

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechCombatHooks.Draw.cs` :: Draw.cs:3-46; Healing.cs:95-110
- 来源:hooks-combat-assets,置信度 high

**问题**:Draw.cs 的类体大括号多缩进一档、DrawPrefix 前半段比后半段多一层 tab,方法中途从三层 tab 跳回两层(return true; 后的闭括号只有两 tab);Healing.cs 的 HolyFire 段落(List<Creature> enemies 之后)整块多缩进一层。能编译但与项目 tab 风格冲突,后续 diff 审查会把纯缩进噪声误读为逻辑改动,是真实维护成本。

**改法**:对这两个文件跑一次 dotnet format(或编辑器 reindent),单独提交为纯格式 commit 以免污染逻辑 diff。

#### F40. [P3][convention] 同一'伤害结算中'守卫模式三种写法并存

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Powers/HextechPowers.cs` :: HextechPowers.cs:8-16,69-80; HextechNextTurnDamagePower.cs:5-7,42-54; HextechCombatHooks.Outbreak.cs:5-8
- 来源:hooks-combat-assets,置信度 high

**问题**:HextechBurnPower 用 static int _resolveDepth,HextechNextTurnDamagePower 用 AsyncLocal<int>,Outbreak/SleightOfFlesh/Compensation 也用 AsyncLocal——三处 RunWith*Guard 结构几乎逐字相同。static int 版本在跨 await 的并发任务链下语义与 AsyncLocal 不同(全局可见 vs 执行流内可见),读者需要逐处推断哪个语义是刻意的;实际消费方(BurningInterestRune/FirebrandEnemyHex)在同一伤害链内读取,两种实现恰好都工作,纯属历史演化不一致。

**改法**:抽一个 HextechScopedDepthGuard(AsyncLocal 实现,提供 IsActive 与 RunAsync(Func<Task>))统一替换三处;BurnPower 的 static int 迁到 AsyncLocal 与其余对齐,并在类型注释里写明'执行流内可见'语义。

#### F44. [P3][convention] Mayhem modifier 查找写法不统一 + GetMayhemModifier 三处重复定义

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechCombatHooks.Healing.cs` :: Healing.cs:100-104; Shared.cs:5; RunLifecycle/Core.cs:68; Telemetry/PayloadBuilder.cs:98
- 来源:hooks-combat-assets,置信度 high

**问题**:HealAfterOriginal 的 HolyFire 段用 player.RunState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault() 内联查找,而同文件其他位置用 GetMayhemModifier(runState);且 GetMayhemModifier 作为 private 助手在三个不相关文件里重复定义(语义是否一致需读者逐个确认——LastOrDefault vs 其他实现若取 First 会在异常多实例场景分歧)。

**改法**:把 GetMayhemModifier 提升为 internal 静态助手(如放 HextechMayhemModifier 自身或 HextechCombatHooks.Shared),删除三处私有副本,HolyFire 段改用同一入口。

#### F53. [P3][compat] SweepingBlade/OrbSymbiosis 复制模型的取库方式对第三方内容不设防且三种写法并存

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Runes/SweepingBladeRune.cs` :: 117, 155(另 OrbSymbiosisRune.cs:30)
- 来源:runes-content,置信度 medium

**问题**:SweepingBlade 用 ModelDb.DebugPower(power.GetType()) 复制 power(挂在 BeforePowerAmountChanged/AfterPowerAmountChanged 上),OrbSymbiosis 用 GetById<OrbModel>(orb.Id) 复制 orb:当被复制对象来自其他 mod 且类型/Id 未注册时均直接抛,异常进 power/orb 命令链。触发条件比 TwilightVeil 窄(需 Strike 标签 mod 卡上 debuff / mod 自定义 orb),但同属一类;且全库"复制模型"现有 GetById、GetByIdOrNull、DebugPower 三种写法,只有 GetByIdOrNull 是安全形。

**改法**:降级说明:ModelDb.Init 经反编译证实会全量注册所有已加载 mod 程序集的 AbstractModel 子类,"活体实例未注册"架构上近不可达,分叉风险不实。仍可做的一致性清理:把 DebugPower(type)/GetById(id) 统一为 GetByIdOrNull + CanonicalInstance?.Id ?? Id 的既有惯例(与 DoubleVisionRune.cs:711 对齐),取不到时 Warn 并跳过。

#### F61. [P3][convention] 零散死代码:UniversalSpiral.CanEnchant 纯转发 base

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Enchantments/UniversalSpiral.cs` :: 17-20
- 来源:runes-content,置信度 high

**问题**:override 仅 return base.CanEnchant(card),无行为、无注释说明占位意图,是纯死代码;读者会误以为此附魔有特殊可附条件。

**改法**:删除该 override;若是为将来限制预留,改为注释而非空 override。

#### F70. [P3][convention] TryTakeBuffered* 两个方法 45 行反射扫描逐行复制、FieldInfo 每次调用重查

- 位置:`src/Selection/Sync/HextechRuneSelectionCoordinator.RemoteChoices.cs` :: 202-311
- 来源:selection-sync,置信度 high

**问题**:TryTakeBufferedRemoteChoice 与 TryTakeBufferedExpectedRemoteChoice 对 PlayerChoiceSynchronizer._receivedChoices 的反射遍历(senderId/choiceId/completionSource/Task 四次 GetField/GetProperty)完全复制,且 typeof(...).GetField 在每次调用(含轮询路径)重新查找;仓库其他处(如 Rewards/ColorDiscoveryCardReward.cs:7-8 的 RequireField)已有静态缓存惯例。反射目标已核验在 0.107.1/0.109.0 均存在、失败降级完备(Warn+false 走事件路径),兼容面无问题,纯一致性/维护成本。

**改法**:仿 ColorDiscoveryCardReward 把 _receivedChoices 与 ReceivedChoice 三字段的 FieldInfo 提为 static readonly(TryGetField 允许 null 降级);抽共享的 private static IEnumerable<(int index, ulong senderId, uint choiceId, Task<NetPlayerChoiceResult> task)> EnumerateBufferedChoices(synchronizer) 供两方法复用。

#### F71. [P3][convention] 两处成块缩进错位(块体多缩一级、闭括号错位)

- 位置:`src/Selection/Coordinator/HextechRuneSelectionCoordinator.ActRoll.cs` :: 129-137(另:HextechForgeSelectionCoordinator.cs:86-93)
- 来源:selection-sync,置信度 high

**问题**:ActRoll.cs 的 host 分支内 hostSnapshot 声明到 return 共 8 行整体多缩进一个 tab 且 if 闭括号与块体错位;ForgeSelectionCoordinator.cs 的 TrySyncLocalHextechChoice 块同样错位。不影响语义但在这两个联机关键路径上误导嵌套层级阅读,也是仓库无 .editorconfig/formatter 检查的直接证据。

**改法**:修正这两处缩进;考虑补最小 .editorconfig(tab 缩进 + csharp_new_line 规则)并在 CI/pre-commit 跑 dotnet format --verify-no-changes,防再犯。

#### F72. [P3][convention] 跨文件重复小助手,其中 IndexOfRelic 同名双语义是真实陷阱

- 位置:`src/Selection/Coordinator/HextechRuneSelectionCoordinator.Selection.cs` :: 218-229 (对照 ForgeSelectionCoordinator.cs:181-199 / RelicOptionSelectionCoordinator.cs:171-189)
- 来源:selection-sync,置信度 high

**问题**:三个 IndexOfRelic 同名但语义不同:Selection.cs 用 ReferenceEquals(屏幕返回同实例,正确),Forge/RelicOption 用 ModelId 相等——搬用时极易选错导致重复 id 选项(如未来出现同 id 多实例)定位错位。另有 CreateMonsterHexRelic(Pools.cs:105 与 Screen Core.cs:172)、GetMonsterHexSlot(Pools.cs:127 与 EnemyPreview.cs:220)、MarkRelicsSeen 三份、以及『60 次自旋等单例』循环四份(WaitForPlayerChoiceSynchronizerAsync、CreateRuneSelectionScreenAsync、CreateForgeSelectionScreenAsync、WaitForOverlayStackAsync)。

**改法**:IndexOfRelic 改名区分(IndexOfRelicInstance vs IndexOfRelicById)并集中到一个静态助手类;60 次自旋抽成 WaitForSingletonAsync<T>(Func<T?> get, int attempts=60);CreateMonsterHexRelic/GetMonsterHexSlot/MarkRelicsSeen 收敛到单处。

#### F78. [P2][convention] 'isClient' 判定手写 try/catch 在 Mayhem 内重复 4 次,应下沉为 helper

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Mayhem/HextechMayhem.RunConfiguration.cs` :: 66-74, 104-114(另见 HextechMayhem.PlayerRuneConfig.cs:36-44, 53-62)
- 来源:enemyhex-mayhem,置信度 high

**问题**:同一段 'RunManager.Instance.NetService.Type == NetGameType.Client 包裹 bare catch 回退本地配置' 的模式在 GetEffectiveRunConfigurationSnapshot、CreateNewRunConfigurationSnapshot、GetPlayerRuneConfigDisabledIdsForPool、CreateNewRunPlayerRuneConfigDisabledIdsSnapshot 各写一遍,且两处 catch 回退方向还不同(一处回 GetSnapshot 一处回 GetDefaultSnapshot,当前各自正确但极易在下次复制时抄错)。项目已有 HextechPlayerContextHelper.IsNetworkMultiplayerRun() 承担同类职责,却没有 IsClient 版本,导致每个调用点自带一份防御样板。

**改法**:在 HextechPlayerContextHelper 增加 public static bool IsClientRun() { try { return RunManager.Instance?.NetService?.Type == NetGameType.Client; } catch { return false; } },4 处调用点改为 bool isClient = HextechPlayerContextHelper.IsClientRun(),各自保留原有的回退分支选择。

#### F80. [P3][convention] 缩进纪律失守:3 个 Mayhem 文件整体空格缩进 + 效果注册列表 2/3 tab 混排,建议补 .editorconfig

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Mayhem/HextechMayhem.CombatModifiers.cs` :: 全文件(另见 HextechMayhem.CombatStart.cs、HextechMayhem.TurnEnd.cs、EnemyHexes/HextechEnemyHexEffects.cs:21-29,47-60,85-127)
- 来源:enemyhex-mayhem,置信度 high

**问题**:项目约定 tab 缩进,但 CombatModifiers.cs(90 行)、CombatStart.cs(43 行)、TurnEnd.cs(15 行)整体用 4 空格;HextechEnemyHexEffects.cs 的 121 项注册列表在 2 tab 与 3 tab 之间随机漂移(cat -et 实证)。这不是纯口味:混排让这些高频改动文件(每加一个海克斯都要动 Effects 列表)的 diff 持续产生无意义噪声,也说明没有工具在守——这正是审查要点里'无 .editorconfig 是否值得补'的具体证据。

**改法**:一次性 reindent 这 4 处为 tab;在 HextechRunes/src(或仓库模组根)补 .editorconfig:[*.cs] indent_style = tab,配合现有 IDE0005 工作流让格式漂移在编辑期即被拦截。

#### F91. [P3][convention] HextechAssets.cs 混用 4 空格与 tab 缩进

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Assets/HextechAssets.cs` :: 3-190(常量区 4 空格;89-96 等行为 tab;方法体内两种混排)
- 来源:platform,置信度 high

**问题**:项目既有风格是 tab 缩进,本文件常量声明全部 4 空格,TryGetCustomRelicIconPath 内部两种缩进交替出现(如 89-90 行 tab、91-93 行空格)。同文件混排会让后续 diff 产生大量无意义空白噪声,也是无 .editorconfig 的直接受害样本。

**改法**:全文件统一为 tab。顺带建议仓库补一个最小 .editorconfig(indent_style=tab + file-scoped namespace 偏好),一次性锁住该类问题;HextechRuneGrantHelper.cs:62-74 的整块多缩进一级(误导性缩进,内容实际不在新作用域)也一并修正。

#### F93. [P1][convention] GetDataDirectory 逐字节复制粘贴了三份,配置落盘路径存在三处分叉风险

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Config/HextechRuneConfiguration.cs` :: 763-788 (另两份: Hooks/UI/HextechRelicVisibilityHooks.Config.cs:127-150, Telemetry/HextechTelemetry.Config.cs:40-62)
- 来源:conventions-global,置信度 high

**问题**:三个子系统(符文配置、遗物可见性配置、遥测配置)各自维护一份完全相同的 ~25 行 GetDataDirectory(Godot user dir → ApplicationData → UserProfile 三级回退),差异仅在 OS 的 using 写法。将来任何一处路径策略调整(如迁移目录、Godot API 变更)若只改一处,三个子系统的数据会静默落到不同目录,用户配置'丢失'类 bug 排查成本高;这不是联机敏感路径,合并零风险。

**改法**:新建 Helpers/HextechDataPaths.cs: internal static class HextechDataPaths { internal static string GetDataDirectory() { ...现有实现... } internal static string GetFilePath(string fileName) => Path.Combine(GetDataDirectory(), fileName); },三处调用点改为 HextechDataPaths.GetDataDirectory() 并删除本地副本。顺带可把两处 GetConfigPath 也收进去。

#### F95. [P2][convention] 8 个文件偏离 tab 缩进基线,使用 4 空格或 tab/空格混排

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/HextechTypes.cs` :: 全文件 (完整清单见 detail)
- 来源:conventions-global,置信度 high

**问题**:对全部 759 个 .cs 做全量扫描(非抽样):751 个文件 tab 缩进 + file-scoped namespace 一致率 100%(未发现任何块式 namespace)。偏离者 8 个:根目录 HextechTypes.cs、ModInfo.cs 纯空格;Mayhem/HextechMayhem.{TurnEnd,TurnStart,CombatStart,CombatModifiers}.cs 纯空格(同目录其余 20+ 个 partial 均为 tab,同一 partial 类内部两种缩进并存);Runes/HextechGoldrendSync.cs 纯空格;Assets/HextechAssets.cs 与 Content/HextechPlayerRuneRegistry.cs tab/空格混排(常量区与大型集合初始化器用空格)。混排文件在 diff review 时最容易产生看不见的整段缩进噪声。

**改法**:对这 8 个文件做一次纯空白字符归一化提交(空格→tab),不夹带逻辑改动;之后靠补全的 .editorconfig(见另一条)阻止回潮。归一化可用 `expand -t4 | unexpand -t4 --first-only` 或 IDE format,提交前用 `git diff -w` 确认零逻辑差异。

#### F96. [P2][convention] .editorconfig 只有一条 IDE0005 规则,未固化任何实际风格基线

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/.editorconfig` :: 1-4
- 来源:conventions-global,置信度 high

**问题**:任务背景假设'无 .editorconfig',实测 src/.editorconfig 存在但仅含 root=true 与 dotnet_diagnostic.IDE0005.severity=warning(这是 0.8.5 重构轮为 using 清理加的)。tab 缩进、file-scoped namespace 这两条实际一致率极高的约定完全没有被工具固化,导致上一条 finding 的 8 个空格文件能混进来且无告警;协作者(含 AI 协作)每次都靠肉眼对齐。

**改法**:扩为最小匹配现状的版本(不引入新告警面):
```
root = true

[*.cs]
indent_style = tab
charset = utf-8
insert_final_newline = true
csharp_style_namespace_declarations = file_scoped:warning
dotnet_diagnostic.IDE0005.severity = warning

[*.{csproj,json}]
indent_style = space
indent_size = 2
```
csproj 现状即 2 空格,故第二节与现状一致。不建议此轮就开 IDE 全量 style 分析,保持增量。

#### F97. [P2][convention] ApplyDefaultMegaLabelTheme 完全相同的实现存在三份

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechRuneConfigMenuHooks.cs` :: 2447-2461 (另两份: Hooks/Compat/HextechGameOverCompatibilityHooks.cs:118-131, Selection/UI/HextechRuneSelectionScreen.Style.cs:233-246)
- 来源:conventions-global,置信度 high

**问题**:三份逐字节相同(已 diff 核实):把 MegaLabel 的 theme 默认字体/字号固化为 override。这是'照抄原版字体'套路的落地点,分散三处意味着字体策略调整(如后续支持其他 mod 换字体包)要改三遍;纯本地 UI 路径,合并无联机风险。工作树刚改过的 Style.cs 也含其中一份,正是继续复制的趋势信号。

**改法**:提取到 UI 公共处,如新建 UI/HextechUiTheme.cs: internal static class HextechUiTheme { internal static void ApplyDefaultMegaLabelTheme(MegaLabel label) { ... } },三处改调用。与 MarkRelicsSeen(Selection 三个 coordinator 各一份,两份 IReadOnlyList 一份 IEnumerable)可同一轮清理。

#### F98. [P3][convention] IsNetworkMultiplayer 在两个敌方海克斯里绕过既有公共 helper 重新实现

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/EnemyHexes/WarmogsSpiritEnemyHex.cs` :: 103-106 (另一处: EnemyHexes/SwiftAndSafeEnemyHex.cs:116-119)
- 来源:conventions-global,置信度 high

**问题**:Helpers/HextechPlayerContextHelper.cs:7 已有公共 IsNetworkMultiplayerRun(),Relics/Base/HextechRelicBase.PlayerContext.cs:10 也是转发它;但两个 EnemyHex 各自私有重写 `RunManager.Instance.NetService.Type is NetGameType.Host or NetGameType.Client`。'是否联机'这个判断在本 mod 是行为开关(如 NatureIsHealing 联机禁用),若将来判定口径要调整(比如加 rejoin 中间态),散落副本会漏改,造成主客两端行为口径不一致——这类不一致正是联机分叉的温床。

**改法**:两处删除私有实现,改调 HextechPlayerContextHelper.IsNetworkMultiplayerRun()。可再 grep `NetGameType.Host or NetGameType.Client` 确认无其他散落判定。

#### F99. [P3][convention] 日志前缀纪律 443/454 达标,残留 1 条裸 Log 与个别子系统缺口

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechCombatHooks.JeweledGauntlet.cs` :: 101
- 来源:conventions-global,置信度 high

**问题**:全量统计 454 处 Log/HextechLog 调用:443 处以 $"[{ModInfo.Id}]" 开头(其中 431 处带第二段子系统标签,[Mayhem] 289 处最多),12 处仅有 mod 前缀无子系统段(集中在 ModEntry/Bootstrap 的加载期消息,可接受),仅 1 处完全裸奔:JeweledGauntlet.cs:101 的 `Log.Info($"Monster {monster.Id.Entry} repeating move ...")`,玩家日志里无法归因到本 mod,分诊 bug report 时会被当原版日志。

**改法**:该行改为 `HextechLog.Info($"[{ModInfo.Id}][JeweledGauntlet] Monster {monster.Id.Entry} repeating move {repeatState.Move.Id} via enemy Jeweled Gauntlet");`(顺带从始终输出的 Log.Info 降到 verbose 门控,与同类事件级日志一致)。

#### F100. [P3][convention] Hooks 目录类命名 37/40 遵循 Hextech* 前缀,3 个类是例外

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Assets/AssetHooks.cs` :: 类声明行 (另: Hooks/UI/CollectionHooks*.cs, Hooks/Cards/ThoughtOverwriteKeywordPersistenceHooks.cs 及同目录数个 *KeywordPersistence 辅助类)
- 来源:conventions-global,置信度 high

**问题**:Hooks 下 40 个 *Hooks 类中 37 个带 Hextech 前缀;例外是 AssetHooks(本轮工作树刚改过的文件)、CollectionHooks、ThoughtOverwriteKeywordPersistenceHooks,另有 Cosplay/CorruptedBranch/CurtainCall 等 KeywordPersistence 辅助类和 AssetResourceResolver(新文件)也无前缀。internal 类型无跨程序集冲突风险,纯一致性问题,但在按类名 grep 日志/反编译比对时,前缀是快速区分'自家 vs 原版'的信号。

**改法**:低成本时机(如下次动这些文件时)重命名为 HextechAssetHooks / HextechCollectionHooks / HextechAssetResourceResolver 等;若刻意保留(如 KeywordPersistence 系列视为内容类而非 infra),在 docs 里写一句命名边界即可,不必强改。

#### F101. [P3][convention] 死代码: HextechMapLengthReducer.ReduceNodeLengthByOne 无任何调用者

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Helpers/HextechMapLengthReducer.cs` :: 10
- 来源:conventions-global,置信度 high

**问题**:全仓库(含 tests)grep 仅命中定义本身;实际使用的是同类的 ReduceNodeLength(HastyScribbleEnemyHex.cs:12 传 rowsToRemove)。ByOne 版本应是参数化重构后的残留。同轮抽查 Helpers/Api/Services/Assets/State 共 71 个 public/internal 方法,其余未引用的仅 Api/HextechRunesApi.cs 的 TrackPersistentInnate/IsPersistentInnateTracked/RestorePersistentInnate 三件套——那是给外部 mod(SponsorPack/二创)用的公共 API 契约面,不应删。

**改法**:删除 ReduceNodeLengthByOne 方法;若它只是 ReduceNodeLength(…, 1) 的便捷包装且想保留 API 对称性,至少加中文注释说明保留原因,否则 IDE0005 式的'零残留'纪律会被它稀释。

## 批次 7:较大重构(默认跳过,需要用户单独确认后才执行)

涉及大文件拆分与结构性合并,风险与收益都高于前六批。本批次不要在未经用户确认时执行。

#### F25. [P2][convention] HextechRuneConfigMenuHooks.cs 2755 行拆分方案:先聚合 pending 状态,再按职责拆 8 个 partial

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechRuneConfigMenuHooks.cs` :: 273-546, 1394-1658
- 来源:hooks-ui,置信度 high

**问题**:该文件是仓库最大文件,真实维护成本集中在两点:CreateOverlay(273-546)一个方法 273 行,声明 20 个 pending 局部变量并经由闭包网互相引用;CreateBottomBar(1394-1426)有 30 个参数 + 1 个 out 参数,新增任何一项配置都要同时改 CreateOverlay 的声明、CreateBottomBar 的签名/调用点、Save/Reset/Import 三处遍历——本轮读代码时这条链就出现了三次完整重复(Save 1532-1546、buildPendingCode 1560-1574、applyPreview 1583-1610)。

**改法**:第一步(先做):新建 private sealed class RuneConfigPendingState,收纳全部 pending 数组/HashSet/绑定列表(PlayerHexCounts、EnemyHexCounts、RerollLimits、DisabledPlayerIds、DisabledMonsterHexIds、DisabledForgeIds、四组 weights、ForgePrice、五个 bool、numericBindings、booleanBindings、三组 iconBindings),并给它 ToSnapshot()/ApplySnapshot(HextechRunConfigurationSnapshot) 两个方法——Save/Export/Import 三处重复立即坍缩,CreateBottomBar 参数降到 6 个以内。第二步按现有行段拆 partial(保持既有 .Community.cs 命名惯例):.MenuButton.cs=53-197(主菜单按钮注入),.Overlay.cs=199-546(开合动画+CreateOverlay),.Pages.cs=574-1175(四个页面与控件区块),.Tabs.cs=1257-1392(tab 按钮/指示条),.Actions.cs=1394-1675(底栏+分享动作),.RuneIcons.cs=1849-2190(图标构建/切换/悬浮),.Entries.cs=2192-2400(BuildRuneEntries/BuildEnemyHexEntries/BuildForgeEntries/来源键),.Style.cs=2407-2651(CreateLabel/StyleBox/稀有度色/卡片);records 与 AwaitProcessFrameAsync 等小工具留在根文件。

#### F27. [P2][convention] 五个逐帧轮询视觉类重复同一套 130 行脚手架

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechSlowCookAuraHooks.cs` :: 64-273 (同构:HextechBaronAuraHooks.cs, HextechBurnVisualHooks.cs, HextechGlassCannonHealthBarHooks.cs, HextechNearDeathFeastVisualHooks.cs, Runes/FlyingKickCorpseLaunchDriver.cs)
- 来源:hooks-ui,置信度 high

**问题**:TryAttach(IsInstanceValid+IsNodeReady+Hitbox/Entity 判空 → ActiveCreatureNodes 去重 → Start 失败回滚 → TaskHelper.RunSafely)、RunAsync(while IsInstanceValid 轮询+ToSignal(ProcessFrame)+catch Warn+finally QueueFree/Remove)、EnsureRenderOrder、静态 ActiveCreatureNodes/LoggedMissingTexturePaths、LoadTextureOrWarn、ScaleLayer、AuraLayer record——这套模式在 5 个 Hooks/UI 文件加 Runes/FlyingKickCorpseLaunchDriver 里逐字重复(LoadTextureOrWarn 三份、ScaleLayer 三份、AuraLayer record 两份、Install 的三个 patch 目标四份)。已经出现分叉苗头:Burn 版 TryAttach 检查 Entity != null 而 Baron 版检查 Entity?.Player != null,dt 钳制写法有 Min(Max()) 与 Clamp 两种——将来修一个生命周期 bug(如新增 rejoin 清理)要改 6 处。

**改法**:提取 abstract class HextechCreatureVisual:封装 static TryAttach 泛型入口(去重表按具体类型分表)、RunAsync 骨架(虚方法 Tick(float dt) / ShouldShow())、EnsureRenderOrder、LoadTextureOrWarn(带 per-type LoggedMissingTexturePaths)、ScaleLayer 与共享 AuraLayer record;三个 Install 方法也可合并为一个 HextechCreatureVisualHooks.Install 统一 patch NCombatRoom._Ready/AddCreature/NCreature._Ready 后分发给注册的视觉工厂列表(还能把每目标 4 个 postfix 减为 1 个,降低 patch 数量)。

#### F50. [P2][convention] 生成类符文缺统一 helper,FilterForCombat 不变量靠逐文件自觉维持

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Relics/Base/HextechRelicBase.CardGeneration.cs` :: 1-69
- 来源:runes-content,置信度 high

**问题**:"FilterForCombat + Where(CanBeGeneratedByModifiers) + OrderBy(CardKey) + HextechStableRandom.Pick + combatState.CreateCard"这条五段式管线在 SingularityAI/Deadwood/CorruptedBranch/BlankCheck/MindOverMatter/ColorDiscovery/JackpotUpgrade 等至少 7 个文件重复手写,已产生两次同类漏滤事故(SingularityAI 已修、本轮又发现 3 处)。管线不进基类,不变量就永远靠 review 兜底,这是有实际维护成本的重复而非口味问题。

**改法**:在 HextechRelicBase.CardGeneration.cs 增加 protected CardModel? PickStableGeneratedCard(HextechCombatState combatState, Func<CardModel,bool>? extraFilter, params string?[] saltParts):内部固定走 CardFactory.FilterForCombat+CanBeGeneratedByModifiers+OrderBy(CardKey)+StableRandom.Pick+CreateCard,各符文只传额外过滤(如 Type==Power)与盐;7 处调用点逐个迁移。

#### F54. [P2][convention] Pacifist 与 Compensation 各自手写同构的"commandId 挂账+静态登记+战斗边界清账"机制(~90 行×2)

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Runes/PacifistRune.cs` :: 5-160(对照 CompensationRune.cs:5-170)
- 来源:runes-content,置信度 high

**问题**:两个符文的 PendingXxx record、静态 HashSet 注册表、EnqueuePending(同 commandId 合并)、TryTakePending、ClearPendingForCommand/ForRune、RemoveFromPendingRegistryIfEmpty、BeforeCombatStart/AfterCombatEnd/BeforeSideTurnStart 三处清账——结构逐行同构,只有合并策略(Max vs 累加)与键(combatId vs Creature 引用)不同。这套机制与 HextechCombatHooks.CurrentActualDamageCommandId 强耦合,是最容易在第三个使用者手里抄错清账时机的部分。

**改法**:提取 Runes/ 或 Helpers/ 下的 CommandScopedPendingEffects<TKey,TPending>(构造传合并函数),暴露 Enqueue/TryTake/ClearForCommand/ClearAll 与静态 ClearAllForCommand;PacifistRune、CompensationRune 改为组合持有,战斗边界清账收敛到基类一处。

#### F58. [P3][convention] Cards/ 的 Token 卡三件套(Pool/VisualCardPool/PortraitPath 样板)在 13 个类里逐字重复

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Cards/HextechCustomCards.cs` :: 7-17, 52-60, 100-108(另 AllInCard.cs:7-11、BladeWaltzCard.cs:7-11 等)
- 来源:runes-content,置信度 high

**问题**:`Pool => IsMutable && Owner != null ? Owner.Character.CardPool : ModelDb.CardPool<TokenCardPool>()` + `VisualCardPool => Pool` + `AllPortraitPaths => [PortraitPath]` 这组样板在 Cards/ 13 个 CardModel 子类中逐字出现,新增 token 卡时漏抄任意一行都会复现"卡框颜色不对/占位图"类历史 bug。

**改法**:新增 abstract class HextechOwnerPoolTokenCard : CardModel 承载这三个 override(PortraitPath 留 abstract),13 个卡类改继承并删除样板;行为零变化,IL 等价可验证。

#### F59. [P3][convention] Forges 三档间两处真重复:随机升级逻辑双份、开战上 buff 模式 12 类同构

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Forges/HextechForges.Silver.cs` :: 80-107(对照 Gold.cs:244-283;开战 buff 模式见 Silver.cs:3-63 等)
- 来源:runes-content,置信度 high

**问题**:UpgradeForge.UpgradeRandomCards 与 GoldUpgradeForge.AfterObtained 除盐字符串(silver-/gold-upgrade-forge)与张数外逐行相同,是复制粘贴级重复(Gold 版 260-269 行还带着粘贴时的缩进错位);另有约 12 个锻造器(Strength/Dexterity/SilverPlating/Focus/Flesh/Ritual/Regen/Buffer/Slippery/PrismaticArtifact/Void 等)是同一个"BeforeCombatStart 守卫+Flash+PowerCmd.Apply<T>(Stacked(...))"模板。三档文件本身按稀有度分文件是合理组织,但这两处重复会在改守卫条件时漏改一半。

**改法**:①升级逻辑提为 HextechForgeBase.UpgradeStableRandomCards(int count, string salt) 供两档调用;②新增 PowerAtCombatStartForgeBase<TPower>(可选 IsAvailableForPlayer 谓词),12 个类各缩到 CanonicalVars+数值;顺手修正 Gold.cs:260-269 与 GoldenSpatulaRune.cs:36-40 的缩进错位。

#### F60. [P3][convention] Runes/ 356 文件单层平铺,建议按既有基类谱系分子目录

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Runes` :: -
- 来源:runes-content,置信度 medium

**问题**:356 个文件平铺一层,其中 43 个 CardUpgradeRuneBase<T> 子类(XxxUpgradeRune)、8 个抽象基类、若干 partial(SolidTimeRune 三件)与配套 helper(BreadSandwichAssemblyHelper、FlyingKickCorpseLaunchDriver)混排;按文件名前缀查找尚可,但"这个符文属于哪一族/该抄哪个基类"只能靠打开文件确认。全部文件都是 file-scoped `namespace HextechRunes;`,移动不改命名空间,csproj 为通配包含,重组零编译风险。

**改法**:最小分层:Runes/Base/(8 个基类+SuppressionScope 类工具)、Runes/Upgrades/(43 个 XxxUpgradeRune)、Runes/DragonSouls/、其余留根;git mv 一次提交完成,不动任何代码内容。

#### F62. [P3][convention] 约 68 处相同的 hook 守卫前奏可收敛为基类判定,顺带消除写法漂移

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Relics/Base/HextechRelicBase.PlayerContext.cs` :: 1-48
- 来源:runes-content,置信度 medium

**问题**:`player != Owner || Owner == null || Owner.Creature.IsDead`(68 处)及其带 `CombatState is not HextechCombatState` 的变体(另 195 处 IsDead 引用)在符文间条件顺序、是否查 CombatState、是否查 IsAvailableForCharacter 各有漂移;这不是口味问题——漂移意味着新符文容易漏掉某一项(如漏 IsDead 导致死亡后仍触发的历史 bug 类别)。

**改法**:在 HextechRelicBase.PlayerContext.cs 增加 protected bool IsInactiveFor(Player player) => player != Owner || Owner == null || Owner.Creature.IsDead; 与 protected bool TryGetOwnedCombat(Player player, out HextechCombatState combatState);新代码强制使用,存量按触碰顺序渐进迁移即可,不必一次性替换 68 处。

#### F68. [P2][convention] Forge 与 RelicOption 两个选择协调器 ~200 行近似复制且已语义漂移

- 位置:`src/Selection/Coordinator/HextechForgeSelectionCoordinator.cs` :: 全文件 vs HextechRelicOptionSelectionCoordinator.cs 全文件
- 来源:selection-sync,置信度 high

**问题**:两个协调器的骨架(空选项守卫→单机→unsync→无 synchronizer→本地选择+TrySync→远端等待+ResolveRemote)、IndexOfRelic、MarkRelicsSeen、ResolveRemote* 全部逐行同构,仅屏幕工厂与 codec 方法不同。已出现漂移:Forge 的 ResolveRemote 对 synced 模型调 .ToMutable()(:164),RelicOption 不调(:154)——后续修 bug 只改一处的风险已成立。这是三个同步选择流(rune/forge/relicOption)中后两个的复制,且 SelectRelicOption 经 Api/HextechRunesApi.cs:92 暴露为公共 API,行为漂移会变成对外契约漂移。

**改法**:抽一个 internal static Task<RelicModel?> SelectSynchronizedRelic(Player, options, context, ISyncChoiceProtocol) 泛化助手,协议对象封装 Create/IsExpected/TryDecode 与屏幕工厂;Forge 与 RelicOption 各保留 <40 行适配层。顺带统一 ToMutable 语义并写明。

#### F74. [P3][convention] 选择屏 partial 群职责基本清晰,唯 Interaction.cs 混入约 250 行地图预览子系统

- 位置:`src/Selection/UI/HextechRuneSelectionScreen.Interaction.cs` :: 288-506 (map preview/map button 状态机)
- 来源:selection-sync,置信度 medium

**问题**:partial 划分(Core/Layout/PlayerCards/EnemyPreview/Style/Metadata/Metrics/Audio/Hover/LayoutHelpers)整体命名与内容对应良好,是本仓库大文件拆分的正面样板;但 Interaction.cs(517 行)一半是选择输入/确认保护,另一半是只读地图预览状态机(_mapPreviewActive/_mapButtonForceEnabled/_restoreAfterMapReopenQueued 三个状态位 + BeginMapPreview/EndMapPreview/RestoreAfterMapReopenAsync),两套关注点共享文件使状态位归属难读。另 Core.cs 顶部承载了两个非嵌套公共类型(HextechEnemyHexAdjustmentOptions、HextechSelectionMetadataMode)。

**改法**:把地图预览状态机拆为 HextechRuneSelectionScreen.MapPreview.cs partial;HextechEnemyHexAdjustmentOptions 移到独立文件(与 Coordinator 共用的输入契约放 Selection/ 根)。

#### F77. [P2][convention] SwiftAndSafe 与 WarmogsSpirit 是 ~110 行近似孪生实现,含各自私有的 IsNetworkMultiplayer()

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/EnemyHexes/WarmogsSpiritEnemyHex.cs` :: 1-108(对照 SwiftAndSafeEnemyHex.cs:1-120)
- 来源:enemyhex-mayhem,置信度 high

**问题**:两个海克斯结构逐段同构:单机 AfterCardDrawn 递增自己的 per-player 抽牌计数并按 TierValue 里程碑给敌人上 power;联机走 4 个相同的 hook 重载(AfterCardPlayedLate/AfterPlayerTurnStartLate/BeforePlayPhaseStart/BeforeTurnEnd)从 CombatManager.History 重算计数。差异只有:tracking 字典键、tier 数值、施加的 power 类型和 Apply 入口。改任一路径的 bug(如 history 计数口径)必须记得同步改另一份。两文件还各自定义 private static IsNetworkMultiplayer()(WarmogsSpirit:103-106)直接读 RunManager.Instance.NetService.Type、无 null 防护,与已有的 HextechPlayerContextHelper.IsNetworkMultiplayerRun()(有 try/catch 兜底)语义分叉。

**改法**:提取抽象基类(如 DrawMilestoneEnemyHex):子类只提供 TierValues、Dictionary<ulong,int> 选择器(context.Tracking 上的字段)和 Task ApplyMilestoneReward(context, enemies, int stacks);两个私有 IsNetworkMultiplayer() 删除,统一调 HextechPlayerContextHelper.IsNetworkMultiplayerRun()。

#### F81. [P3][convention] 周期触发三重守卫(RoundNumber<=1 / %(N+1) / round-once 防重)在 4 个海克斯中手抄,interval 语义命名漂移

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/EnemyHexes/LagavulinMatriarchEnemyHex.cs` :: 14-22(另见 LeafSlimeEnemyHex.cs:9-17、QueenEnemyHex.cs、DivineInterventionEnemyHex.cs)
- 来源:enemyhex-mayhem,置信度 high

**问题**:'每过 N 回合'的约定实现(RoundNumber<=1 短路 + %(N+1) + ConsumeGlobalProcInCombat("round-once:{Kind}:{Round}") 防额外回合重入)在 4 个文件各写一份,且同名变量含义已经分叉:LeafSlime 的 interval = TierValue+1(含 +1),Lagavulin 的 interval = TierValue(% 时再 +1)。数学上当前都对,但下一个照抄者从哪个文件抄决定了会不会差一——LeafSlime 还专门写注释'照 DivineIntervention'说明作者自己也在靠注释对齐约定。

**改法**:在 HextechEnemyHexContext 增加 internal bool TryConsumeRoundInterval(MonsterHexKind kind, HextechCombatState combatState, int everyNRounds):内部统一做 <=1 守卫、% (everyNRounds+1) 和 round-once 防重;4 个调用点改为 if (!context.TryConsumeRoundInterval(Kind, combatState, context.TierValue(Kind, 3, 2, 1))) return;,'N 是间隔回合数'的语义只存在一处。

#### F82. [P3][convention] 敌方海克斯需要在两份 120+ 项手维护列表中双重登记,仅靠启动异常兜底

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/EnemyHexes/HextechEnemyHexEffects.cs` :: 5-128(对照 Content/HextechMonsterHexRegistry.cs:7-134)
- 来源:enemyhex-mayhem,置信度 medium

**问题**:新增一个敌方海克斯要同时改 HextechMonsterHexRegistry(kind/rarity/icon relic/disabled/burnTip)和 HextechEnemyHexEffects.OrderedEffects(效果实例),两表 131 项平行维护。CreateOrderedEffects 的启动校验(重复/缺失/未注册均 throw)已把漂移变成 fail-fast,所以这不是 bug 风险,而是纯粹的维护税:每次内容更新多一处必改点,列表顺序(隐含的默认分发序)与注册表顺序也各自为政。

**改法**:把效果工厂并入注册项:Monster<TRelic> 改为 Monster<TRelic, TEffect>(TEffect : HextechEnemyHexEffect, new()),MonsterHexRegistration 增加 Func<HextechEnemyHexEffect> EffectFactory,HextechEnemyHexEffects.OrderedEffects 改为按 Registrations 顺序实例化;需要特殊分发序的场景已有 PersistentOrder/EnemyHealOrder 承接。启动校验可保留为断言。改动面大,建议排在下个内容批次一起做。

#### F103. [P3][convention] 注释语言一致率 793/856,英文注释集中在最大文件 HextechRuneConfigMenuHooks.cs

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechRuneConfigMenuHooks.cs` :: 全文件散布 (~20 条英文行注释)
- 来源:conventions-global,置信度 high

**问题**:全量统计:856 条行注释中 793 条含中文(92.6%),符合'中文注释解释为什么'的既有约定;英文注释唯一成规模聚集在 2755 行的 HextechRuneConfigMenuHooks.cs(20 条),其次 Rewards/HextechForgeChoiceReward.cs(4 条)与三份 GetDataDirectory 副本里的英文 fallback 注释。该文件同时也是仓库最大文件,英文注释多提示它是早期代码未跟上后来的注释规范。

**改法**:不建议为改注释单开一轮;在该文件下次因功能改动被拆分时(它本身就是拆分头号候选,其他分片 agent 应有对应 finding)顺手把保留下来的英文注释按'解释为什么'标准换写为中文,GetDataDirectory 合并(P1 那条)落地时英文注释自然只剩一份。

## 8a. 低优先级杂项(P3,顺手做,不单独成批;拿不准就跳过并记录)

- **F5 [P3][compat] NCreature.SetAnimationTrigger finalizer 全局吞 NRE,也会掩盖其他 mod 的动画机 bug,且 5 条日志后完全静默** — `HextechRunes/src/Hooks/UI/HextechAnimTriggerSafetyHooks.cs` :: 26-39
  改法:保留吞 NRE,但把限流从'前 5 条后全静默'改为周期采样(如每 100 次再记 1 条),并在日志里带上 __instance 的 creature/model id 与异常首帧(ex.StackTrace 首行),让被掩盖的第三方动画机 bug 可定位。不建议改回抛出。

- **F6 [P3][compat] 联机断连 finalizer 用 'SavedProperties' 子串匹配分类异常,可能把其他 mod 的序列化异常也转成 ModMismatch 吞掉** — `HextechRunes/src/Hooks/Compat/HextechMultiplayerCompatibilityHooks.cs` :: 104-161
  改法:把 nameof(SavedProperties) 一条从'消息/栈子串'收紧为只匹配异常栈的声明类型命名空间前缀(如 StackTrace 含 "MegaCrit.Sts2.Core.Multiplayer.SavedProperties"),并在 Log.Error 文案里注明'若两端版本一致,此错误可能来自其他 mod 对序列化路径的补丁',降低误归因成本。

- **F8 [P3][convention] AssetHooks 的 HoverTip 反射后备字段解析失败时静默跳过,与同文件其它降级路径的 Warn 纪律不一致** — `HextechRunes/src/Hooks/Assets/AssetHooks.cs` :: 196-225
  改法:在 Install 里对 HoverTipIconField==null 补一条与 52-57 行同格式的一次性 Log.Warn("Power hover tip icon backing field not found; hover tip icons will show vanilla placeholder."),运行时路径保持静默不变。

- **F9 [P3][compat] CardModel.CanPlay 双 postfix 用 Priority.Last 抢终裁位:与其他同样声明 Last 的 mod 顺序未定义,建议文档化语义** — `HextechRunes/src/Hooks/Combat/HextechCombatHooks.Install.cs` :: 52-66
  改法:在 InstallCardPlayHooks 的两个 HarmonyMethod 处加注释:'Priority.Last=禁玩终裁:本 mod 的封禁(Kaka/BackToBasics)必须压过第三方放行;放行(BlueCandle 等)只翻转 !__result 情形,不与第三方封禁抢位'。若未来收到与具体 mod 的冲突报告,可再考虑数值上用 Priority.Last-1 之类显式让位。

- **F30 [P3][convention] 死代码:两个无调用者的私有 helper 与两处死参数** — `/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechRuneConfigMenuHooks.cs` :: 2436-2445, 2575-2583
  改法:删除 SetButtonDisplayText 与 GetRarityAccentColorByKey;CreateStepButton 去掉 disabled 参数与 disabled 样式行;Mikaels CreateLayer 去掉 zIndex 参数(调用点的 0/1/2/3/4 实参一并删除)。若想保留 SetActionButtonText 的语义,把它从 Community 分部移到 .Style.cs 供两侧共用。

- **F31 [P3][convention] tab 按钮尺寸字面量在两处重复,改动会导致指示条错位** — `/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechRuneConfigMenuHooks.cs` :: 442-445, 1264-1272
  改法:提取 private static Vector2 GetTabButtonSize(bool compactLayout) => compactLayout ? new Vector2(108f, 36f) : new Vector2(154f, 42f); 两处调用之。

- **F32 [P3][convention] 中英双语硬编码的收藏册子分类标题依赖原版 starter 头文案逐字替换** — `/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/CollectionHooks.cs` :: 18-49 (另见 CollectionHooks.Reflection.cs:26-46)
  改法:把 9 语言的子分类头/副文案放进 relic_collection.json(键如 HEXTECH_COLLECTION_HEADER_HEXTECH / _BODY 等,走既有 sync 管线),FormatLikeStarterHeader 改为从 starter 模板提取样式骨架(定位原文头/体在模板中的位置后整段替换为本地化文本),或干脆直接用与原版一致的 BBCode 模板拼接,不再依赖逐字 Replace;_starterHeaderTemplate 在 LoadRelicsPostfix 每次刷新时重取而非 ??=。

- **F41 [P3][compat] Storm 重实现丢了 Flash() 且弃用传入的 choiceContext(守卫已核验保留)** — `/Users/iniad/sts2-mods/HextechRunes/src/Mayhem/HextechMayhem.CardEvents.cs` :: 41-85
  改法:AfterCardPlayedLate 补发前调用 owner.Creature.GetPower<StormPower>()?.Flash() 恢复视觉一致;choiceContext 用 new BlockingPlayerChoiceContext() 的原因(而非透传参数)补一行注释。

- **F46 [P3][convention] AssetHooks 收尾残余:类尾多余空行** — `/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Assets/AssetHooks.cs` :: 557-559
  改法:删除第 558 行空行;可随下次提交顺带处理。

- **F47 [P1][compat] TwilightVeil 用 ModelDb.GetById 复制第三方 Power,未注册模型时在 power 管线内抛异常** — `/Users/iniad/sts2-mods/HextechRunes/src/Runes/TwilightVeilRune.cs` :: 65
  改法:改为 ModelDb.GetByIdOrNull<PowerModel>(power.CanonicalInstance?.Id ?? power.Id),取不到时 Log.Warn 一次并 return(不镜像),与 DoubleVisionRune DuplicateObtainedRelic 的处理方式对齐;两端模型注册表一致,skip 判定在两端确定一致,不引入分叉。

- **F57 [P3][compat] ColorDiscovery 硬编码 5 个原版职业池,新职业/模组职业不会自动纳入** — `/Users/iniad/sts2-mods/HextechRunes/src/Runes/ColorDiscoveryRune.cs` :: 123-130
  改法:若刻意只收原版:加中文注释说明"仅收录原版职业池,新职业需手动补"并在 tests 里断言此列表长度等于反射枚举到的原版 CardPoolModel 非无色子类数,版本升级时测试自动提醒;若想自动跟进:改为反射枚举 sts2 程序集内 IsColorless==false 的 CardPoolModel 子类。

- **F69 [P2][compat] ChoiceCodec 旧版本兼容路径在版本偏斜时静默回退默认值,依赖未经确认的『同版本才可联机』前提** — `src/Selection/Sync/HextechChoiceCodec.cs` :: 161-200, 236-336 (TryDecodeDisabledPlayerRuneConfig / TryDecodeRunConfigurationSnapshot 的未知版本号 return true 分支)
  改法:二选一:(a) 在 ActRoll payload 头部加一个 mod 协议版本 int,客户端解码到不认识的版本时 Log.Error+弹可见提示(而非静默默认),明确拒绝跨版本混跑;(b) 若确认 join 门禁保证同版本,删除 -2/-5 与 ordinal legacy 解码路径并在文件头注明门禁依据。

- **F87 [P3][defense] card.Owner 用异常当控制流探测 canonical 实例** — `/Users/iniad/sts2-mods/HextechRunes/src/Helpers/HextechKnifeHelper.cs` :: 28-37 (ShouldFanOfKnivesAffectSovereignBlade), 50-59 (TryCreateBigKnifeReplacement)
  改法:替换为显式守卫:if (!card.IsMutable) { return false; }(AbstractModel 有 IsMutable/IsCanonical 公开语义),删除 try/catch;CombatHelpers 的 potion 同改。行为不变,意图归档,意外异常恢复 fail-visible。

- **F89 [P3][compat] 回合触发计数 key 用短类名,第三方同名符文会共享计数器** — `/Users/iniad/sts2-mods/HextechRunes/src/Relics/Base/LimitedDebuffProcRelicBase.cs` :: 97-100 (GetProcKey)
  改法:改为 GetType().FullName(或 Assembly.GetName().Name + ":" + FullName,与 HextechModelTypeIdentity 的身份口径一致)。注意:若该 key 已进联机同步载荷,需确认两端版本一致后一起换,避免混装版本分叉——可挂在下一个破坏性版本一起发。

- **F90 [P3][defense] 遥测 pending 队列读-传-写非原子,失败原因被裸 catch 抹掉** — `/Users/iniad/sts2-mods/HextechRunes/src/Telemetry/HextechTelemetry.UploadQueue.cs` :: 15-52 (UploadPendingThenCurrentAsync), 37-40 (裸 catch)
  改法:用一个 static SemaphoreSlim(1,1) 把整个 UploadPendingThenCurrentAsync 串行化(替代 QueueLock);post 的 catch 至少记一次 Log.Warn(首个异常的 GetType().Name+Message,限一条防刷屏);SubmittedRunIds.Add 挪进锁内。

- **F102 [P3][convention] csproj 保留 0.103.2-0.107.0 共 5 组历史 DefineConstants 与 ModInfo 的 8 级 #elif 链** — `/Users/iniad/sts2-mods/HextechRunes/src/HextechRunes.csproj` :: 18-36 (对应 ModInfo.cs:11-25 的 #elif 链)
  改法:两选一:(a) 删除 0.103.2-0.107.0 的 PropertyGroup 与对应 #elif 分支、清理 HextechPowerCmdCompat 旧分支,csproj 顶部加中文注释'支持目标: 0.107.1 / 0.109.0';(b) 若想保留考古能力,仅加注释声明'0.107.0 及以下分支自 0.108 迁移后未再验证,不保证可编译'。倾向 (a),因为假分支在下次 API 迁移时会白白增加 grep 噪声。


## 8b. 禁改清单(对抗验证已驳倒,按原思路改会引入回归)

### F0. CardRewardAlternative.Generate 被无条件整体替换,冻结原版逻辑并压制其他 mod 的 prefix

- 位置:`HextechRunes/src/Hooks/Compat/HextechRewardSafetyHooks.cs` :: 65-94
- **为什么不改**:反编译证实原版 Generate 末尾有 `Count > 2` 即 throw 的硬限制,整体替换的存在理由正是删掉它(DoubleVision 复制佩尔之翼、百炼成钢第三按钮都会触发原版崩溃);改 postfix 不可行——原方法抛异常时 postfix 不执行,恰在需要它的场景失效。唯一可做:在 prefix 上方补注释写明"冻结原版 0.109 实现,游戏升版需 diff CardRewardAlternative.Generate"。

### F4. 核心 hook 组安装失败会留下'半安装'状态,且重入 Initialize 会对已装组重复打补丁

- 位置:`HextechRunes/src/ModEntry.cs` :: 29-76
- **为什么不改**:触发前提被反证:两个受支持宿主版本上不存在会重入 Initialize 的调用方,双重打补丁场景不可达。不改。

### F28. OnFocusPrefix 在 try 保护圈外求值 RunManager.Instance,防御缺口与自身意图矛盾

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/UI/HextechPlayerStatsHoverHooks.cs` :: 33-36
- **为什么不改**:RunManager.Instance 经反编译证实为急切初始化、无 setter 的静态只读单例,进程内不可能为 null;DebugOnlyGetState() 仅返回属性不会抛。按原建议加 `?.` 反而是引入本轮要削减的死防御。不改。

### F37. Entomancer.SpitMove 被无条件完整重实现(改变原版怪物行为)

- 位置:`/Users/iniad/sts2-mods/HextechRunes/src/Hooks/Combat/HextechEncounterCompatibilityHooks.cs` :: 23-43
- **为什么不改**:反编译三版(0.103.3/0.107.1/0.109.0)证实重实现是忠实移植,真实动机是 0.107.1 原版用 `First()` 取 PersonalHivePower 无空守卫(0.109 官方已改 FirstOrDefault),蜂巢被本 mod 系统移除时会在怪物回合任务链内抛异常——正当的空安全修复。唯一可做:补注释说明动机;0.109 变体可考虑用 `#if !STS2_109_OR_NEWER` 条件编译掉该 hook(官方已修)。


另有 4 条降级条目(F11、F48、F53、F65)保留在批次内,但其原始分叉论证已被驳倒,只执行修正后的日志/一致性动作,详见各条"降级说明"。


---

## 9. 完成报告要求

执行完毕后输出:
1. 每批次的 commit hash 与一句话摘要;
2. 跳过的条目编号与原因;
3. 最终验证输出(两目标测试计数、双版本编译结果、警告计数——应为 0);
4. 对批次 7 的评估意见(哪些值得做、预估改动面),供用户决定是否另开一轮。
