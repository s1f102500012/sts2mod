# 寰宇支配之剑

作者：Natsuki

《杀戮尖塔 2》模组，为涅奥添加先古遗物“寰宇支配之剑”和专属 0 费攻击牌
“抹杀”。

- 获得遗物时，向持有者的牌组中加入一张“抹杀”。
- “抹杀”是一张单体 0 费攻击牌。它按所选敌人的对象、战斗 ID 和模型实例
  追踪直接续命谱系，并收敛生命值、战斗列表、状态订阅与场景节点，不触发
  常规死亡事件；每次打出后，这张牌在本局游戏中的基础耗能永久增加 1。
- 遗物图标使用实时 Godot 材质：9 帧剑身、28 帧高光和 9 帧遮罩各自按原始
  Minecraft 时序运行，遮罩内部渲染移动的宇宙星场。

第三方素材与许可见 `THIRD_PARTY_NOTICES.md`。

## 游戏版本兼容

同一个发布包内含分别使用官方引用程序集编译的 `0.107.1` 与 `0.110.0`
实现。根目录中的稳定 Loader 会读取游戏版本，并从 `lib/<版本>/` 选择不高于
当前游戏版本的最新实现；每个实现均带兼容目标标记与 SHA-256 校验。

Release 构建启用优化与确定性输出，不发布调试符号。构建脚本会分别使用
`versioned-dll-backups` 中对应版本的引用程序集编译，并在部署前验证 Loader、
兼容标记和每个变体 DLL 的 SHA-256。

## 抹杀实现结构

- `ErasureKill.cs`：入口、共享反射句柄和选中目标初始化。
- `ErasureKill.Patches.cs`：Harmony 注册、各层阻断回调与 Godot 延迟回调的因果
  作用域传播；已提交且完成收敛的血缘会在下一具可见实体创建前封闭其延迟续命。
- `ErasureKill.ManagerState.cs`：跨版本战斗身份与生命周期状态读取。
- `ErasureKill.Tracking.cs`：目标身份、直接死亡/转阶段因果令牌、通用槽位分配来源
  与战斗内绑定。
- `ErasureKill.Convergence.cs`：生命、状态列表、订阅与节点的幂等收敛。
- `ErasureKill.Stabilization.cs`：有界跨帧续命租约与稳定证书签发。
- `ErasureKill.Persistence.cs`：卡牌永久耗能提交前的同战斗结算租约。
- `ErasureKill.CombatCompletion.cs`：原版动作完成后的胜利入口协调与单航班结算。
- `ErasureLineage.cs`：不依赖游戏运行时的谱系准入策略；只有携带所选目标因果
  令牌的生命周期变更才能建立续命边，类型与槽位不构成正向准入证据。
- `ErasureMutationJournal.cs`：记录有界、确定性的续命变更边与拒绝原因。
- `ErasureLineage.Completion.cs`：谱系活动修订、续命租约与稳定完成证书。
- `ErasureCompletionPolicy.cs`：正常战斗结算前的纯门禁策略。
