# 自定义难度

《杀戮尖塔 2》0.107.1 模组：在角色选择界面添加怪物血量和怪物攻击倍率滑条。

## 功能

- 怪物血量倍率：`x0.1` 到 `x5.0`，步进 `0.1`
- 怪物攻击倍率：`x0.1` 到 `x5.0`，步进 `0.1`
- 联机时只有房主可以调整，客户端只接收房主设置
- 攻击倍率通过隐藏 power 应用于怪物攻击，不占用可见 buff 栏
- 滑条值会保存到当前 profile；关闭游戏后继续 run 会读回倍率

## 安装

把发行包里的三个文件放到游戏的模组目录：

```text
mods/CustomDifficulty/
  CustomDifficulty.dll
  CustomDifficulty.json
  CustomDifficulty.pck
```

## 构建

```bash
./tools/build_and_deploy.sh
```

脚本会编译 `net9.0` DLL、使用游戏本体 headless 打包 PCK，并部署到本机 STS2 的 `mods/CustomDifficulty` 目录。

## 联机

该模组会影响游戏模型集合和战斗数值。联机时所有玩家需要安装同一版本、同一文件内容的模组。
