# 事件图片、文案与选项布局工作标准

本文件适用于 `IntegratedStrategyEvents` 的新增和改版事件。目标是在不删改原始文案、不缩小既有字号的前提下，让事件图片、正文、选项和悬浮提示在游戏内保持清晰、稳定且易读。

## 1. 事件图片

### 1.1 文件规格

- 统一使用 `1280×720`、`16:9` 的 PNG。
- 使用 8-bit RGBA / sRGB；保留 Alpha 通道，不转成 JPEG，也不使用有损调色板压缩。
- 文件名使用小写 `snake_case`，放入 `assets/images/events/`。
- 保留用户提供的原图；只处理放入模组资源目录的副本。
- 如果原图不是 `16:9`，先根据主体位置人工确定裁切范围，不直接拉伸人物或场景。

### 1.2 标准处理命令

当原图已经是 `16:9` 时，使用 ImageMagick 的 Lanczos 缩放和 PNG 无损压缩：

```bash
magick SOURCE.png \
  -filter Lanczos \
  -resize '1280x720!' \
  -strip \
  -alpha on \
  -define png:color-type=6 \
  -define png:compression-level=9 \
  -define png:compression-filter=5 \
  -define png:compression-strategy=2 \
  OUTPUT.png
```

这里的压缩只改变 PNG 编码体积，不引入 JPEG 式画质损失。不要为了进一步缩小文件而擅自降低色数。

处理后必须检查真实产物：

```bash
magick identify -format '%m %wx%h depth=%z type=%[type] channels=%[channels] opaque=%[opaque] compression=%C quality=%Q bytes=%b\n' OUTPUT.png
```

验收结果应为：PNG、`1280x720`、8-bit、`TrueColorAlpha`、RGBA、ZIP 压缩。即使画面肉眼看起来完全不透明，也保留统一的 RGBA 格式。

### 1.3 Godot 导入与打包

- 构建脚本把 `assets/` 复制到临时 Godot 项目后统一执行资源导入，不手写或提交 `.png.import`。
- 事件图保持 Godot `CompressedTexture2D` 的无损导入模式：`compress/mode=0`。
- 不生成 mipmap：`mipmaps/generate=false`。
- 不在 Godot 导入阶段二次缩放：`process/size_limit=0`。
- PCK 打包导入描述和 `.ctex` 产物；存在 `.import` 时不重复打包原始 PNG。

## 2. 文案与富文本

- 用户提供的标题、正文、选项标题、选项说明和后续文本默认视为定稿；不得为了排版擅自删减、改写、概括或压缩。
- 不通过缩小标题、正文或选项说明字号来解决遮挡。页面说明沿用项目统一的 28px，选项说明沿用 22px；标题和选项标题使用游戏原生字号。
- 只按叙事段落使用 `\n\n`；不要用手工换行硬凑某个分辨率下的行宽。
- 富文本按语义少量使用：
  - `[red]`：伤害、死亡、危险、敌意。
  - `[green]`：生命、恢复、生长。
  - `[gold]`：奖励、珍贵物、关键抉择。
  - `[blue]` / `[aqua]`：寒冷、水、晶体、空间。
  - `[purple]`：异常、梦境、仪式、诅咒。
  - `[orange]`：火焰、温暖、商业和金币语境。
  - `[jitter]`：震动、狂躁、崩裂或不安。
  - `[sine]`：回声、流动、反复或带韵律的语句。
- 嵌套标签必须反向闭合，例如 `[sine][red]文本[/red][/sine]`。
- 中英文的标签骨架、变量占位符和选项含义必须对应；不要在翻译时丢失标签或 SmartFormat 占位符。

## 3. 选项内容

- 选项标题写“玩家要做什么”，选项说明写明确的代价、奖励或叙事结果。
- 涉及数值时，说明必须与实际逻辑一致；支付、失去、获得、锁定门槛要逐项核对。
- 锁定选项沿用相同标题，只把说明改成真实门槛。
- 保持用户给出的选项顺序。离开选项通常放在最后。
- 后续页只显示该页仍可执行的选项；纯结局页直接结束事件。
- 卡牌、遗物、药水或能力预览使用项目现有 hover tip helper，不用纯文本伪造预览。

### 3.1 选项说明的富文本

- 选项说明以快速、准确地传达结果为主，不能把正文的气氛标签机械地复制到选项里。
- 战斗倾向、敌意、危险程度和角色态度等叙事性说明默认使用普通字体。例如“我与它同样渴望战斗。”“错误的敌意应当回避。”“遭遇一场特殊的战斗。”均不额外染色或抖动。
- 会进入战斗的事件保持项目既有格式：选项标题说明行动，选项说明使用普通文本写“遭遇一场特殊的战斗。”或“遭遇一场艰难的战斗。”。
- `[jitter]`、`[sine]` 等动效主要用于事件正文；除非选项本身存在必须表现的异常状态，否则不用于选项标题和说明。
- 颜色标签只标记具有明确游戏含义的信息：
  - `[red]`：实际失去的生命、最大生命或其他明确代价。
  - `[blue]`：数量、需求门槛或选择张数。
  - `[gold]`：金币、遗物、卡牌奖励、药水等奖励对象。
- 不要仅因为“战斗”“危险”“敌意”等词听起来紧张，就给它们添加 `[red]`；也不要仅为了强调一句话而添加动效。
- 中英文选项使用相同的标签逻辑；中文为普通文本时，对应英文也保持普通文本。

## 4. 文案与选项布局

布局调整解决的是可读性，不是文案长度。先观察背景的主体、亮区和高对比细节，再把整组标题、正文和选项放到视觉干扰较少的一侧。

### 4.1 Layout Profile 选择

- 默认从 `IntegratedStrategyEventLayoutProfile.Standard` 开始。
- 左侧留白更干净时使用 `Left(...)` 或现有 `LeftWide`、`LeftMedium`、`LeftCompact` 等配置。
- `ContentWidthScale` 只调整文字容器和按钮宽度，改变自动换行；它不应改变字号或删减文案。
- `HorizontalOffset` 用于避开背景主体；移动后仍由布局系统限制在安全边距内。
- `VerticalOffset` 用于避开亮区或容纳较多按钮。仅特定选项数量需要移动时，必须同时设置 `VerticalOffsetOptionCount`，避免后续页也被错误抬高或下移。
- 四个选项可参考 `VerticalOffset=-70` 且 `VerticalOffsetOptionCount=4`；五个选项可参考现有 `StandardRaised`。最终数值仍以目标背景和实际页面为准。

### 4.2 不允许的处理方式

- 不删标题、正文或选项说明。
- 不为单个事件添加更小的 `[font_size]` 来塞进画面。
- 不只移动可见文字而留下按钮 hitbox、焦点区域或 hover tip 在原位。
- 不用大量手工换行固定某一种分辨率的布局。
- 不把文字压到画面主体、强光、高密度纹理或同色背景上。

### 4.3 Hover tip

- 左侧选项带卡牌、遗物、药水或能力预览时，检查提示是否会向屏幕外展开。
- 必要时在事件 Definition 中启用 `AlignHoverTipsRight: true`。
- 是否需要右对齐以 `Option.HoverTips.Any()` 为准，不能只检查遗物字段。

## 5. 每个事件的验收清单

1. 图片为 `1280×720` 的 8-bit RGBA PNG，原图仍被保留。
2. 标题、正文、选项标题、选项说明和后续文本与用户定稿一致。
3. 中文和英文均检查富文本闭合、占位符及含义。
4. 初始页和所有后续页分别检查；不能只看选项最多的第一页。
5. 在目标背景上确认文字对比度、自动换行、按钮宽度、按钮 hitbox 和 hover tip 位置。
6. 同步 `event_descriptions.txt`、`event_options.txt` 和 `event_refresh_conditions.txt`。
7. 依次运行：

```bash
tools/validate_event_structure.sh
tools/build_and_deploy.sh
../tools/verify_headless_load.sh IntegratedStrategyEvents
```

结构校验、编译部署和 headless 加载不能替代实际事件页面的视觉检查；最终布局由玩家在游戏内确认。
