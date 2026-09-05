# 寰宇支配之剑

作者：Natsuki

《杀戮尖塔 2》模组，为涅奥添加固定的第四个先古遗物选项"寰宇支配之剑"，获得时向牌组中加入一张
0 费攻击牌"抹杀"。

- **寰宇支配之剑**（先古遗物）：获得时，向牌组底部加入 1 张"抹杀"。
- **抹杀**（0 费单体攻击，Token）：剥去目标的全部能力，再通过游戏自身的伤害流程造成上限伤害
  （999,999,999），无视格挡，不受力量、易伤等能力修正；若目标仍未消散，立即结算敌方回合结束并对所有
  敌人再次抹杀，至多重复 16 次；每次打出后，这张牌在本局游戏中的基础耗能永久 +1。
- 遗物图标是实时 Godot 材质：9 帧剑身、28 帧高光与 9 帧遮罩各自按原始 Minecraft 时序运行，
  遮罩内渲染移动的宇宙星场。第三方素材与许可见 `THIRD_PARTY_NOTICES.md`。

## 设计边界

"抹杀"是一个**效果**，不是一套接管生命与死亡的**机制**。它只是 `CreatureCmd.Damage` 的一个调用方，
使用与原版"失去生命"效果相同的 `Unblockable | Unpowered` 标志。目标身上的死亡防止
（`Hook.ShouldDie`）、掉血修正（`Hook.ModifyHpLost`，如无实体、缓冲）以及任何对生命值与
死亡流程的接管照常生效。

它先剥去目标的全部能力（逐个走原版 `PowerCmd.Remove`）：原版里所有"死后不离场、轮到自己再站起来"的
存活手段（千足虫节段的重新接合、实验体的适应、寄生之类）都是挂在身上的能力，能力没了，死亡就是普通
死亡，尸体正常离场。无法移除的能力保持原状。

它也不给敌人喘息：目标挨了一刀却仍未消散时，卡牌立即结算一次**敌方回合结束**（原版
`Hook.AfterSideTurnEnd`，参与者与原版回合循环一样是全部敌人），让一切"等回合结束再说"的存活手段
（无实体倒计时、按回合分段的形态、回合末才推进的阶段）现在就结算，然后对场上所有敌人再挥一次，
至多十七刀。普通敌人第一刀就消散，后面的循环不会运行；一刀无法结束的东西会在同一次打出里被连续
逼到它自己的终点。循环在目标真正死亡、一轮没造成任何伤害、场上没有可命中的敌人或达到上限时停止。

本模组只用后缀补丁：不跳过原方法、不重写 IL、不打 `Hook.*` 与日志、联机通道的补丁；这些设计决定
由 `tests/` 里的护栏守着。

## 补丁裁决表

| 补丁 id | 目标 | 种类 | 为什么官方扩展点不够 |
| --- | --- | --- | --- |
| `neow.initial-options` | `Neow.GenerateInitialOptions` | postfix | 涅奥初始选项集合没有可覆写的虚方法或 Hook；后缀只在原结果上追加一项。带修正器的局保持原版三选项。 |
| `neow.all-possible-options` | `Neow.AllPossibleOptions` getter | postfix | 图鉴/预览列表同上。 |
| `visual.relic-node` | `NRelic.Reload` | postfix（可选） | 静态贴图走 `PackedIconPath` 虚属性；动画材质只能挂在节点上，后缀在原版取图之后套材质并隐藏描边。 |
| `visual.relic-inspect` | `NInspectRelicScreen.UpdateRelicDisplay` | postfix（可选） | 检视界面复用同一张 `TextureRect`，后缀按当前遗物套/还原材质。 |
| `visual.relic-event-option` | `NEventOptionButton._Ready` | postfix（可选） | 涅奥选项按钮的遗物图标。 |

私有成员集中在 `src/VanillaMembers.cs`（`NRelic._model`、`NInspectRelicScreen._relics/_index/_relicImage`、
`AncientEventModel.RelicOption`），缺失时在启动摘要里列出，对应补丁经 `[HarmonyPrepare]` 自行降级。

## 游戏版本兼容

同一发布包内含分别使用官方引用程序集编译的 `0.107.1`、`0.110.0` 与 `0.111.0` 实现。根目录的稳定
Loader 读取游戏版本，从 `lib/<版本>/` 选择不高于当前游戏版本的最新实现；`0.108+` 走官方
`ModManager.AssociateAssemblyWithMod`，只有旧版本才回退到反射桥，两条路径绝不同时生效。
共享代码不含 `#if`：三个版本都存在 `CreatureCmd.Damage(ctx, targets, amount, props, dealer)` 重载；唯一的版本差异
（敌方回合结束钩子在 0.107.1 叫 `Hook.AfterTurnEnd`，0.108 起叫 `Hook.AfterSideTurnEnd`）收在
`UniversalDominionSwordCard.TurnEnd.Legacy.cs` / `.Official.cs` 两个整文件分部里。

`[SavedProperty]` 只有 `UniversalDominionSwordCard.PermanentCostIncrease` 一项（快照见
`tests/UniversalDominionSword.Tests/saved_property_manifest.txt`）；属性集合变化即联机 net-id 布局变化。

## 构建与验证

```zsh
zsh tools/build_and_deploy.sh      # 三变体 + Loader，Godot 4.5 导入资源后打 PCK，部署到游戏 mods 目录
zsh tools/run_tests.sh             # 补丁清单/存档属性快照、声明完整性、"仅后缀"与禁碰目标护栏（按三个版本各跑一遍）
zsh ../tools/verify_headless_load.sh UniversalDominionSword
```

快照变动时用 `UDS_WRITE_MANIFESTS=1 zsh tools/run_tests.sh` 重生成，并在更新日志里写明。
`UDS_DEPLOY=0` 只构建不部署。资源导入默认使用工作区 `.tools/godot-4.5.1` 的编辑器，可用 `GODOT_EDITOR` 覆盖。

验证层级必须分开报告：编译通过、护栏通过、headless 加载通过，都不等于实机或联机验证通过。

## 目录

- `src/`：模组本体（入口、卡牌、遗物、补丁元数据与应用器、私有成员登记、星空材质与着色器）。
- `src/Patches/`：五个后缀补丁。
- `loader/`：稳定 Loader（按游戏版本选择变体）。
- `tests/`：设计护栏与快照。
- `tools/`：构建、打包、测试与素材脚本；`multi_version/` 为变体清单生成与校验。
- `assets/`：清单、图片与本地化。
