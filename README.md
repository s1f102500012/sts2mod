# Slay the Spire 2 Mods

这里是 Natsuki 制作和维护的《杀戮尖塔 2》模组源码合集。每个目录对应一个独立模组，目录名为「中文名 + 英文名」。

This is a collection of Slay the Spire 2 mods made and maintained by Natsuki. Each top-level directory is a standalone mod, named as "Chinese name + English name".

模组目录内通常包含：

- `assets/`：模组清单、本地化文本、图片、音频等资源
- `src/`：C# 源码
- `tools/`：本地构建、打包和部署脚本

仓库只保存源码和可编辑资源，不提交 `dist/`、`src/bin/`、`src/obj/` 等构建产物。

## 模组列表 / Mod List

| 目录 | 模组 ID | 版本 | 最低游戏版本 | 简介 |
| --- | --- | --- | --- | --- |
| `海克斯符文 HextechRunes` | `HextechRunes` | `0.8.7` | `0.107.1` | 海克斯大乱斗（ARAM: Mayhem）：每幕选择海克斯符文、敌人同步获得敌方海克斯的大型玩法拓展，300+ 符文、属性锻造器、新卡牌与联机同步。`本体` 为主模组，`拓展包` 为赞助者额外内容（ARAM: Mayhem: Extra Expansion Pack）。 |
| `集成战略事件 IntegratedStrategyEvents` | `IntegratedStrategyEvents` | `0.5.3` | `0.109.0` | 集成战略风格的事件拓展：先古防线、树洞、秘密节点等大量自定义事件与遭遇。 |
| `俄洛伊 Illaoi` | `Illaoi` | `0.2.0` | `0.107.1` | 新角色模组，围绕生长、触手、灵魂与信仰机制构建。 |
| `我路过 WoLuGuo` | `WoLuGuo` | `1.0.2` | `0.107.1` | 为事件增加离开选项，可以不执行事件专属操作而直接跳过事件。 |
| `可重复附魔 RepeatableEnchantments` | `RepeatableEnchantments` | `0.107.1` | `0.107.1` | 允许卡牌同时拥有多种附魔；重复获得同类层数型附魔时会叠加层数。 |
| `AI队友 AITeammate` | `sts2AITeammate` | `0.1.1` | - | 加入 AI 多人模式、AI 队友和主机 AI 托管。 |
| `无尽模式 EndlessMode` | `EndlessMode` | `0.4.1` | `0.107.1` | 通关后继续深入的无尽挑战流程；提供跨轮楼层与轮次查询的公共 Interop。 |
| `自定义难度 CustomDifficulty` | `CustomDifficulty` | `0.2.1` | `0.107.1` | 在角色选择界面加入怪物血量与攻击倍率自定义滑条；可选按房间递进模式，联动无尽模式跨轮叠加。 |
| `更多进阶挑战 MoreAscensionChallenge` | `MoreAscensionChallenge` | `0.1.1` | - | 增加额外的进阶（负进阶）挑战内容。 |
| `基石符文 KeystoneRunes` | `KeystoneRunes` | `0.4.1` | `0.107.1` | 加入基石符文机制。 |
| `心之钢 Heartsteel` | `Heartsteel` | `0.1.0` | - | 心之钢主题机制。 |
| `奖励附魔 RewardEnchants` | `RewardEnchants` | `0.2.1` | - | 为奖励流程加入附魔相关变化。 |
| `手牌上限解除 RemoveHandLimit` | `RemoveHandLimit` | `0.3.1` | `0.107.1` | 调整或解除手牌数量上限。 |
| `初始牌组调整 StartingDeckTweaks` | `StartingDeckTweaks` | `1.0.1` | `0.107.1` | 调整角色初始牌组。 |
| `更好的角色遗物 BetterCharacterRelics` | `BetterCharacterRelics` | `1.1.1` | `0.107.1` | 强化各角色的初始/角色遗物。 |
| `PRTS动态光标 PRTSCursor` | `PRTSCursor` | `1.0.3` | `0.107.1` | P.R.T.S 动态光标：替换游戏内光标为明日方舟风格动态光标。 |
| `第四幕 StS1Act4` | `StS1Act4` | `0.1.0` | - | 还原/扩展类似一代第四幕的内容。 |

## 创意工坊 / Steam Workshop

海克斯大乱斗（ARAM: Mayhem）：

- 正式版（Public 分支）：https://steamcommunity.com/sharedfiles/filedetails/?id=3747501308
- Beta 版（beta 分支专用）：https://steamcommunity.com/sharedfiles/filedetails/?id=3758775855
- 额外拓展包：https://steamcommunity.com/sharedfiles/filedetails/?id=3749708876

其余模组请在创意工坊搜索对应英文名，或查看作者主页：https://steamcommunity.com/profiles/76561199076042004/myworkshopfiles/?appid=2868840

## 构建说明 / Building

这些模组面向本机《杀戮尖塔 2》开发环境。多数模组目录下提供 `tools/build_and_deploy.sh`，可用于本地编译、打包 PCK 并部署到游戏的 `mods/` 目录。

不同机器上的游戏安装路径可能不同，直接构建前需要先确认 `.csproj` 和脚本里的本地路径是否匹配。

## 反馈与贡献 / Feedback & Contributions

欢迎通过 Issue 反馈问题（附上日志或复现步骤更佳），也欢迎本地化与修复类 PR。

Bug reports (ideally with logs / repro steps), localization improvements and fix PRs are all welcome.

## 开源协议 / License

本仓库整体采用 MIT License，详见 [LICENSE](LICENSE)。
