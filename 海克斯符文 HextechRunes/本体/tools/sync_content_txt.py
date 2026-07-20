#!/usr/bin/env python3
"""同步三个内容 txt 与注册表/本地化真值。

真值来源:
- 玩家符文: src/Content/HextechPlayerRuneRegistry.cs + 拓展包 ModEntry.cs 的 RegisterPlayerRune
- 敌方海克斯: src/Content/HextechMonsterHexRegistry.cs
- 锻造器: src/Content/HextechForgeRegistry.cs + 拓展包 RegisterForge
- 标题/flavor/敌方描述: assets/localization/zhs/relics.json(本体+拓展包)
- 标签中文: assets/localization/zhs/relic_collection.json 的 HEXTECH_TAG.*

目标文件与策略:
- hextech_rune_tags_todo.txt   纯生成物,全量重生成(保留既有行序,新条目插到注册表邻位)。
- hextech_relic_flavors.txt    生成物,全量重生成;PERMANENT/PENDING_OVERRIDES 保留 txt 人工值。
- hextech_relics_summary.txt   混合物,只做增量:补缺失条目、修 #禁用前缀/品级前缀;
                               描述永不覆盖(单条采纳用 --accept-json);删除需 --prune。

模式:
- 默认 --check: 报告差异;存在需落盘的差异时 exit 1(待裁决项不影响退出码)。
- --apply: 写回三个 txt。
- --apply --prune: 同时删除 summary 中已从注册表移除的条目。
- --accept-json "锚": 单条采纳 JSON 描述覆盖 summary 条目,锚格式见 --help 示例,
  如 "怪物:棱彩:金铲铲"、"[我方 / 通用]:黄金:大法师"、"卡牌:白洞"。

类名 -> 本地化键不手写驼峰规则:对 JSON 键去下划线 casefold 反查
(SingularityAIRune 等连续大写由 JSON 键侧真值决定)。
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPONSOR = ROOT.parent / "HextechRunesSponsorPack"

TAGS_TXT = ROOT / "hextech_rune_tags_todo.txt"
FLAVORS_TXT = ROOT / "hextech_relic_flavors.txt"
SUMMARY_TXT = ROOT / "hextech_relics_summary.txt"

RARITY_ZH = {"Silver": "白银", "Gold": "黄金", "Prismatic": "棱彩"}
POOL_SECTION = {
    None: "[我方 / 通用]",
    "Ironclad": "[我方 / 铁甲战士]",
    "Silent": "[我方 / 静默猎手]",
    "Regent": "[我方 / 储君]",
    "Defect": "[我方 / 故障机器人]",
    "Necrobinder": "[我方 / 亡灵契约师]",
}
TAG_SECTION_ORDER = [
    "[我方 / 通用]",
    "[我方 / 铁甲战士]",
    "[我方 / 静默猎手]",
    "[我方 / 储君]",
    "[我方 / 故障机器人]",
    "[我方 / 亡灵契约师]",
]

# ---------------------------------------------------------------------------
# 人工 flavor 保留区。
# PERMANENT: JSON 没有对应字段/刻意的敌方专属文案,永久保留 txt 值。
# PENDING:   txt 与 zhs JSON flavor 方向不一致,方向未裁决——保留 txt 现状,
#            check 时列为“待裁决”不影响退出码。裁决后:采纳 JSON 就删掉对应条目;
#            采纳 txt 就把 JSON 文案改成 txt 值后再删掉条目。
# 键: (章节, 品级或 None, 标题) -> txt 保留值。
# 2026-07-05 用户裁决:金铲铲敌方条目改用与玩家侧共用的官方 flavor("它什么都做得到"),
# 原手工稿弃用,此表清空;以后确需敌方专属人工文案再往这里加。
PERMANENT_FLAVOR_OVERRIDES: dict[tuple[str, str | None, str], str] = {}
# 2026-07-05 裁决:22 条分歧全部采纳 JSON(游戏内已发布版本,8 语言译文以其为源;
# 赞助包体例统一「赞助者物品：」前缀)。以后出现新分歧仍按上方注释的流程往这里收。
PENDING_FLAVOR_OVERRIDES: dict[tuple[str, str | None, str], str] = {}

# 拓展包锻造器/新增拓展包符文条目在 flavors 属性锻造器区使用的后缀约定,
# 与 summary 一致(仅用于新生成条目;既有条目文本由 override/JSON 决定)。
SPONSOR_SUFFIX = "（赞助者拓展包）"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def strip_markup(text: str) -> str:
    text = re.sub(r"\[/?[A-Za-z]+\]", "", text)
    return text.replace("\n", "")


# ---------------------------------------------------------------------------
# 真值装载


class Loc:
    """本体+拓展包 zhs relics.json;类名按字段做 casefold 反查。"""

    def __init__(self) -> None:
        self.sources = [
            json.loads(read(ROOT / "assets" / "localization" / "zhs" / "relics.json")),
            json.loads(read(SPONSOR / "assets" / "localization" / "zhs" / "relics.json")),
        ]
        self.fold: dict[str, dict[str, tuple[dict, str]]] = {}
        for loc in self.sources:
            for key in loc:
                stem, _, field = key.rpartition(".")
                folded = stem.replace("_", "").casefold()
                self.fold.setdefault(field, {}).setdefault(folded, (loc, stem))

    def get(self, class_name: str, field: str) -> str | None:
        entry = self.fold.get(field, {}).get(class_name.replace("_", "").casefold())
        if entry is None:
            return None
        loc, stem = entry
        return loc.get(f"{stem}.{field}")


class Truth:
    def __init__(self) -> None:
        loc = self.loc = Loc()
        tags_loc = json.loads(
            read(ROOT / "assets" / "localization" / "zhs" / "relic_collection.json")
        )
        self.tag_zh = {
            key.split(".", 1)[1]: value
            for key, value in tags_loc.items()
            if key.startswith("HEXTECH_TAG.")
        }

        registry = read(ROOT / "src" / "Content" / "HextechPlayerRuneRegistry.cs")
        sponsor_src = read(SPONSOR / "src" / "ModEntry.cs")

        # 玩家符文(注册表顺序 = 真值顺序;本体在前,拓展包在后)。
        self.player: list[dict] = []
        for match in re.finditer(
            r"Rune<(\w+)>\(\s*HextechRarityTier\.(\w+)([^)]*)\)", registry
        ):
            cls, rarity, args = match.group(1), match.group(2), match.group(3)
            flags = set(re.findall(r"PlayerRuneFlags\.(\w+)", args))
            if "Retired" in flags:
                continue
            pool_match = re.search(r"characterPool:\s*PlayerRuneCharacterPool\.(\w+)", args)
            tag_match = re.search(r'tagKey:\s*"(\w+)"', args)
            self.player.append(
                {
                    "class": cls,
                    "rarity": rarity,
                    "pool": pool_match.group(1) if pool_match else None,
                    "tag_key": tag_match.group(1) if tag_match else "COMPREHENSIVE",
                    "disabled": "Disabled" in flags,
                    "source": "main",
                }
            )
        for match in re.finditer(
            r"RegisterPlayerRune<(\w+)>\(\s*HextechRarityTier\.(\w+),\s*tagKey:\s*\"(\w+)\"",
            sponsor_src,
        ):
            self.player.append(
                {
                    "class": match.group(1),
                    "rarity": match.group(2),
                    "pool": None,
                    "tag_key": match.group(3),
                    "disabled": False,
                    "source": "sponsor",
                }
            )

        # 敌方海克斯。
        monster_src = read(ROOT / "src" / "Content" / "HextechMonsterHexRegistry.cs")
        config_src = read(ROOT / "src" / "Config" / "HextechRuneConfiguration.cs")
        config_default_disabled_monsters = set(
            re.findall(r"MonsterHexKind\.(\w+)", config_src)
        )
        self.monster: list[dict] = []
        for match in re.finditer(
            r"Monster<(\w+)>\(\s*MonsterHexKind\.(\w+),\s*HextechRarityTier\.(\w+)([^)]*)\)",
            monster_src,
        ):
            self.monster.append(
                {
                    "class": match.group(1),
                    "kind": match.group(2),
                    "rarity": match.group(3),
                    "disabled": bool(re.search(r"disabled:\s*true", match.group(4)))
                    or match.group(2) in config_default_disabled_monsters,
                }
            )

        # 锻造器(本体+拓展包)。
        forge_src = read(ROOT / "src" / "Content" / "HextechForgeRegistry.cs")
        self.forge: list[dict] = []
        for match in re.finditer(r"Forge<(\w+)>\(\s*HextechRarityTier\.(\w+)\s*\)", forge_src):
            self.forge.append(
                {"class": match.group(1), "rarity": match.group(2), "source": "main"}
            )
        for match in re.finditer(
            r"RegisterForge<(\w+)>\(\s*HextechRarityTier\.(\w+)", sponsor_src
        ):
            self.forge.append(
                {"class": match.group(1), "rarity": match.group(2), "source": "sponsor"}
            )

        # 事件遗物(排除奥术锻造器的附魔三选一 ChoiceRelic:UI 伪遗物,不入清单)。
        self.event_relics = [
            match.group(1)
            for match in re.finditer(r"RegisterEventRelic<(\w+)>", sponsor_src)
            if not match.group(1).endswith("ChoiceRelic")
        ]

        # 卡牌(本体 cards.json 全部 .title)。
        cards = json.loads(read(ROOT / "assets" / "localization" / "zhs" / "cards.json"))
        self.cards: dict[str, dict[str, str]] = {}
        for key, value in cards.items():
            stem, _, field = key.rpartition(".")
            self.cards.setdefault(stem, {})[field] = value

    def resolve_placeholders(self, cls: str, text: str) -> str:
        """把描述中的 {DynamicVar} 占位符按该类源码里的 DynamicVar 基准值代入。"""
        if "{" not in text:
            return text
        values: dict[str, str] = {}
        for src_root in (ROOT / "src", SPONSOR / "src"):
            for path in src_root.rglob(f"{cls}.cs"):
                for name, num in re.findall(
                    r'new DynamicVar\("(\w+)",\s*([0-9.]+)m', read(path)
                ):
                    values[name] = num.rstrip("0").rstrip(".") if "." in num else num
        return re.sub(
            r"\{(\w+)\}", lambda m: values.get(m.group(1), m.group(0)), text
        )

    def title(self, cls: str) -> str | None:
        return self.loc.get(cls, "title")

    def flavor(self, cls: str) -> str | None:
        return self.loc.get(cls, "flavor")

    def enemy_description(self, cls: str) -> str | None:
        return self.loc.get(cls, "enemyDescription")


# ---------------------------------------------------------------------------
# 通用: 「标题：正文」拆分(标题可能含全角冒号,如「升级：放血」)


def split_titled(body: str, known_titles: set[str]) -> tuple[str, str]:
    parts = body.split("：")
    for cut in range(len(parts) - 1, 0, -1):
        candidate = "：".join(parts[:cut])
        if candidate in known_titles:
            return candidate, "：".join(parts[cut:])
    if body.startswith(("升级：", "质变：")) and body.count("：") >= 2:
        cut = body.index("：", body.index("：") + 1)
        return body[:cut], body[cut + 1 :]
    cut = body.index("：")
    return body[:cut], body[cut + 1 :]


def insert_position(existing: list[str], anchor_order: list[str], item: str) -> int:
    """按真值顺序把 item 插到既有序列中:排在真值序中最近的、已存在的前驱之后。"""
    if item not in anchor_order:
        return len(existing)
    idx = anchor_order.index(item)
    for prev in reversed(anchor_order[:idx]):
        if prev in existing:
            return existing.index(prev) + 1
    return 0


# ---------------------------------------------------------------------------
# 1) hextech_rune_tags_todo.txt —— 全量重生成


def generate_tags(truth: Truth, current_text: str, report: list[str]) -> str:
    known = {reg["class"]: reg for reg in truth.player}

    # 现有顺序(粘行按“我方\t”再切一次)。
    section = None
    order: dict[str, list[str]] = {name: [] for name in TAG_SECTION_ORDER}
    seen: set[str] = set()
    for raw in current_text.splitlines():
        line = raw.strip()
        if line.startswith("[") and line.endswith("]"):
            section = line
            continue
        for match in re.finditer(r"#?我方\t(\w+)\t", raw):
            cls = match.group(1)
            if section in order and cls not in seen:
                order[section].append(cls)
                seen.add(cls)

    ghosts = sorted(seen - set(known))
    if ghosts:
        report.append(f"tags: 移除幽灵条目 {len(ghosts)} 条: {', '.join(ghosts)}")

    # 目标归属与真值顺序。
    target: dict[str, list[str]] = {name: [] for name in TAG_SECTION_ORDER}
    for reg in truth.player:
        target[POOL_SECTION[reg["pool"]]].append(reg["class"])

    blocks: list[str] = []
    added: list[str] = []
    for name in TAG_SECTION_ORDER:
        existing = [cls for cls in order[name] if cls in known and POOL_SECTION[known[cls]["pool"]] == name]
        # 跨节漂移的既有条目也按真值节归位。
        for other in TAG_SECTION_ORDER:
            if other == name:
                continue
            for cls in order[other]:
                if cls in known and POOL_SECTION[known[cls]["pool"]] == name and cls not in existing:
                    existing.insert(insert_position(existing, target[name], cls), cls)
                    report.append(f"tags: {cls} 从 {other} 移至 {name}")
        for cls in target[name]:
            if cls not in existing:
                existing.insert(insert_position(existing, target[name], cls), cls)
                added.append(cls)
        lines = [name]
        for cls in existing:
            reg = known[cls]
            title = truth.title(cls)
            if title is None:
                report.append(f"tags: {cls} 在 zhs relics.json 中找不到标题,跳过")
                continue
            prefix = "#" if reg["disabled"] else ""
            tag = truth.tag_zh.get(reg["tag_key"], reg["tag_key"])
            lines.append(f"{prefix}我方\t{cls}\t{title}\t标签={tag}")
        blocks.append("\n".join(lines))
    if added:
        report.append(f"tags: 新增缺失条目 {len(added)} 条: {', '.join(added)}")
    return "\n\n".join(blocks) + "\n"


# ---------------------------------------------------------------------------
# 2) hextech_relic_flavors.txt —— 全量重生成(含 override 保留)


def flavor_truth_entries(truth: Truth) -> dict[str, list[tuple[str | None, str, str]]]:
    """章节 -> [(品级中文或 None, 标题, flavor)],顺序 = 注册表真值顺序。"""
    sections: dict[str, list[tuple[str | None, str, str]]] = {
        "玩家海克斯": [],
        "敌方海克斯": [],
        "属性锻造器": [],
        "商店": [],
        "事件遗物": [],
    }
    for reg in truth.player:
        title, flavor = truth.title(reg["class"]), truth.flavor(reg["class"])
        if title and flavor:
            sections["玩家海克斯"].append((RARITY_ZH[reg["rarity"]], title, flavor))
    for reg in truth.monster:
        title, flavor = truth.title(reg["class"]), truth.flavor(reg["class"])
        if title and flavor:
            sections["敌方海克斯"].append((RARITY_ZH[reg["rarity"]], title, flavor))
    for reg in truth.forge:
        title, flavor = truth.title(reg["class"]), truth.flavor(reg["class"])
        if title and flavor:
            sections["属性锻造器"].append((RARITY_ZH[reg["rarity"]], title, flavor))
    title = truth.title("RandomForgeShopRelic")
    flavor = truth.flavor("RandomForgeShopRelic")
    if title and flavor:
        sections["商店"].append((None, title, flavor))
    for cls in truth.event_relics:
        title, flavor = truth.title(cls), truth.flavor(cls)
        if title and flavor:
            sections["事件遗物"].append((None, title, flavor))
    return sections


def generate_flavors(truth: Truth, current_text: str, report: list[str]) -> str:
    entries = flavor_truth_entries(truth)
    all_titles = {title for items in entries.values() for _, title, _ in items}

    # 现有条目顺序与现值: (章节, 品级, 标题) -> (顺位, txt flavor)。
    lines = current_text.splitlines()
    header: list[str] = []
    section = sub = None
    current: dict[tuple[str, str | None, str], tuple[int, str]] = {}
    order_counter = 0
    for line in lines:
        stripped = line.strip()
        if section is None and not stripped.startswith("- ") and not stripped.endswith("："):
            header.append(line)
            continue
        if stripped.endswith("：") and not stripped.startswith("- "):
            name = stripped[:-1]
            if name in ("白银", "黄金", "棱彩"):
                sub = name
            else:
                section, sub = name, None
            if section is None:
                header.append(line)
            continue
        if stripped.startswith("- ") and section:
            title, flavor = split_titled(stripped[2:], all_titles)
            key = (section, sub, title)
            if key not in current:
                current[key] = (order_counter, flavor)
                order_counter += 1

    # 头部(到第一个章节标题前)原样保留。
    first_section_idx = next(
        i for i, line in enumerate(lines) if line.strip() == "玩家海克斯："
    )
    header_lines = lines[:first_section_idx]

    out: list[str] = list(header_lines)
    removed: list[str] = []
    diverged: list[str] = []
    added: list[str] = []
    consumed: set[tuple[str, str | None, str]] = set()

    def emit(section_name: str, rarity: str | None, items: list[tuple[str | None, str, str]]) -> list[str]:
        nonlocal added
        picked = [(t, f) for r, t, f in items if r == rarity]
        anchor_order = [t for t, _ in picked]
        existing_sorted = sorted(
            (t for t, _ in picked if (section_name, rarity, t) in current),
            key=lambda t: current[(section_name, rarity, t)][0],
        )
        # 跨品级漂移的旧条目沿用其旧值判断 override 之外一律 JSON。
        ordered: list[str] = list(existing_sorted)
        for t, _ in picked:
            if t not in ordered:
                ordered.insert(insert_position(ordered, anchor_order, t), t)
                found_elsewhere = any(
                    k[0] == section_name and k[2] == t for k in current
                )
                if not found_elsewhere:
                    added.append(f"[{section_name}/{rarity or '-'}] {t}")
        flavor_of = dict(picked)
        result = []
        for t in ordered:
            key = (section_name, rarity, t)
            consumed.add(key)
            value = flavor_of[t]
            if key in PERMANENT_FLAVOR_OVERRIDES:
                value = PERMANENT_FLAVOR_OVERRIDES[key]
            elif key in PENDING_FLAVOR_OVERRIDES:
                value = PENDING_FLAVOR_OVERRIDES[key]
                diverged.append(
                    f"[{section_name}/{rarity or '-'}] {t}\n"
                    f"    txt : {value}\n    json: {flavor_of[t]}"
                )
            result.append(f"- {t}：{value}")
        return result

    for section_name in ("玩家海克斯", "敌方海克斯", "属性锻造器"):
        out.append(f"{section_name}：")
        out.append("")
        for rarity in ("白银", "黄金", "棱彩"):
            out.append(f"{rarity}：")
            out.extend(emit(section_name, rarity, entries[section_name]))
            out.append("")
    for section_name in ("商店", "事件遗物"):
        out.append(f"{section_name}：")
        out.extend(emit(section_name, None, entries[section_name]))
        out.append("")

    truth_keys = {
        (section_name, rarity, title)
        for section_name, items in entries.items()
        for rarity, title, _ in items
    }
    moved: list[str] = []
    for key in current:
        if key in consumed:
            continue
        # 真值中同名条目挂在别的品级、且旧文件在那个品级下没有同名行 -> 品级归位;否则是真移除。
        relocated = any(
            truth_key[0] == key[0]
            and truth_key[2] == key[2]
            and truth_key[1] != key[1]
            and truth_key not in current
            for truth_key in truth_keys
        )
        if relocated:
            moved.append(f"[{key[0]}] {key[2]}: {key[1]} -> 真值品级")
        else:
            removed.append(f"[{key[0]}/{key[1] or '-'}] {key[2]}")

    player_total = len(truth.player)
    player_enabled = sum(1 for reg in truth.player if not reg["disabled"])
    monster_total = len(truth.monster)
    monster_enabled = sum(1 for reg in truth.monster if not reg["disabled"])
    forge_main = sum(1 for reg in truth.forge if reg["source"] == "main")
    forge_sponsor = sum(1 for reg in truth.forge if reg["source"] == "sponsor")
    forge_by_rarity = {
        rarity: sum(1 for reg in truth.forge if reg["rarity"] == rarity)
        for rarity in ("Silver", "Gold", "Prismatic")
    }
    out.append(
        "总计：玩家海克斯 {} 个（可选 {} 个）；敌方海克斯 {} 个（可选 {} 个）；"
        "属性锻造器 {} 个（本体 {} + 赞助者拓展包 {}；白银 {}、黄金 {}、棱彩 {}）；"
        "商店锻造器 1 个。".format(
            player_total,
            player_enabled,
            monster_total,
            monster_enabled,
            forge_main + forge_sponsor,
            forge_main,
            forge_sponsor,
            forge_by_rarity["Silver"],
            forge_by_rarity["Gold"],
            forge_by_rarity["Prismatic"],
        )
    )

    if added:
        report.append(f"flavors: 新增缺失条目 {len(added)} 条: " + "; ".join(added))
    if moved:
        report.append(f"flavors: 品级归位 {len(moved)} 条: " + "; ".join(sorted(moved)))
    if removed:
        report.append(f"flavors: 移除已失效条目 {len(removed)} 条: " + "; ".join(sorted(removed)))
    if diverged:
        report.append(
            f"flavors: 待裁决分歧 {len(diverged)} 条(保留 txt 现状,不影响退出码):\n  "
            + "\n  ".join(diverged)
        )
    return "\n".join(out) + "\n"


# ---------------------------------------------------------------------------
# 3) hextech_relics_summary.txt —— 增量同步


def summary_truth(truth: Truth) -> dict[str, list[dict]]:
    sections: dict[str, list[dict]] = {name: [] for name in TAG_SECTION_ORDER}
    sections["怪物"] = []
    sections["属性锻造器"] = []
    sections["卡牌"] = []
    sections["事件遗物"] = []
    for reg in truth.player:
        title = truth.title(reg["class"])
        if title is None:
            continue
        sections[POOL_SECTION[reg["pool"]]].append(
            {
                "rarity": RARITY_ZH[reg["rarity"]],
                "title": title,
                "disabled": reg["disabled"],
                "desc": truth.resolve_placeholders(
                    reg["class"], strip_markup(truth.loc.get(reg["class"], "description") or "")
                ),
                "suffix": SPONSOR_SUFFIX if reg["source"] == "sponsor" else "",
            }
        )
    for reg in truth.monster:
        title = truth.title(reg["class"])
        if title is None:
            continue
        sections["怪物"].append(
            {
                "rarity": RARITY_ZH[reg["rarity"]],
                "title": title,
                "disabled": reg["disabled"],
                "desc": strip_markup(truth.enemy_description(reg["class"]) or ""),
                "suffix": "",
            }
        )
    for reg in truth.forge:
        title = truth.title(reg["class"])
        if title is None:
            continue
        sections["属性锻造器"].append(
            {
                "rarity": RARITY_ZH[reg["rarity"]],
                "title": title,
                "disabled": False,
                "desc": truth.resolve_placeholders(
                    reg["class"], strip_markup(truth.loc.get(reg["class"], "description") or "")
                ),
                "suffix": SPONSOR_SUFFIX if reg["source"] == "sponsor" else "",
            }
        )
    for stem, fields in truth.cards.items():
        title = fields.get("title")
        if not title:
            continue
        desc = fields.get("hoverTip") or fields.get("description") or ""
        sections["卡牌"].append(
            {"rarity": None, "title": title, "disabled": False,
             "desc": strip_markup(desc), "suffix": ""}
        )
    for cls in truth.event_relics:
        title = truth.title(cls)
        if title is None:
            continue
        sections["事件遗物"].append(
            {"rarity": None, "title": title, "disabled": False,
             "desc": strip_markup(truth.loc.get(cls, "description") or ""), "suffix": ""}
        )
    return sections


def sync_summary(
    truth: Truth,
    current_text: str,
    report: list[str],
    prune: bool,
    accepts: set[str],
) -> str:
    sections = summary_truth(truth)
    known_titles = {
        entry["title"] for items in sections.values() for entry in items
    }

    lines = current_text.splitlines()
    # 定位章节: 段名 -> (起始行号, 结束行号开区间)。
    marks: list[tuple[int, str]] = []
    for i, line in enumerate(lines):
        stripped = line.strip()
        if stripped in POOL_SECTION.values():
            marks.append((i, stripped))
        elif stripped in ("卡牌：", "怪物：", "属性锻造器：", "事件遗物：", "特定规则："):
            marks.append((i, stripped.rstrip("：")))
    bounds: dict[str, tuple[int, int]] = {}
    for idx, (start, name) in enumerate(marks):
        end = marks[idx + 1][0] if idx + 1 < len(marks) else len(lines)
        bounds[name] = (start, end)

    rarity_entry = re.compile(r"^(#?)(白银|黄金|棱彩)：(.+)$")
    edits: dict[int, str | None] = {}  # 行号 -> 替换内容(None=删除)
    inserts: dict[int, list[str]] = {}  # 在该行号之前插入
    stale: list[dict] = []  # {anchor, rarity, title, desc, disabled, line}
    missing: list[dict] = []  # {anchor, entry, pos, use_rarity}
    prefix_fixed: list[str] = []
    accepted: list[str] = []
    unknown_accepts = set(accepts)

    def handle_section(name: str, use_rarity: bool) -> None:
        if name not in bounds:
            report.append(f"summary: 找不到章节 {name}")
            return
        start, end = bounds[name]
        want = {
            ((entry["rarity"], entry["title"]) if use_rarity else entry["title"]): entry
            for entry in sections[name]
        }

        # 预扫: 收集本节已存在的锚,避免把“品级漂移”误判到已有同名条目的品级上。
        present: set[object] = set()
        parsed: list[tuple[int, bool, str | None, str, str, str]] = []
        for i in range(start + 1, end):
            stripped = lines[i].strip()
            if not stripped:
                continue
            if use_rarity:
                match = rarity_entry.match(stripped)
                if not match:
                    continue  # 章节头部说明行等,原样保留
                has_hash, rarity, body = match.group(1) == "#", match.group(2), match.group(3)
                title, desc = split_titled(body, known_titles)
                key = (rarity, title)
            else:
                if "：" not in stripped:
                    continue
                has_hash, rarity = False, None
                title, desc = split_titled(stripped, known_titles)
                body = stripped
                key = title
            parsed.append((i, has_hash, rarity, title, desc, body))
            present.add(key)

        seen: dict[object, int] = {}
        last_line_of_rarity: dict[str | None, int] = {}
        for i, has_hash, rarity, title, desc, body in parsed:
            if not use_rarity:
                # 卡牌升级行(标题+…)归属基础卡锚,不单独建锚。
                base_title = re.sub(r"\+.*$", "", title)
                key = base_title if base_title in want else title
                if key != title:
                    last_line_of_rarity[None] = i
                    seen.setdefault(key, i)
                    continue
            else:
                key = (rarity, title)
            last_line_of_rarity[rarity] = i
            if key in want:
                seen.setdefault(key, i)
                entry = want[key]
                anchor = f"{name}:{rarity}:{title}" if use_rarity else f"{name}:{title}"
                if use_rarity and has_hash != entry["disabled"]:
                    new_prefix = "#" if entry["disabled"] else ""
                    edits[i] = f"{new_prefix}{rarity}：{body}"
                    prefix_fixed.append(anchor)
                if anchor in accepts:
                    unknown_accepts.discard(anchor)
                    prefix = "#" if entry["disabled"] and use_rarity else ""
                    head = f"{prefix}{rarity}：" if use_rarity else ""
                    edits[i] = f"{head}{title}：{entry['desc']}{entry['suffix']}"
                    accepted.append(anchor)
                continue

            # 品级漂移: 同名条目真值挂在本节别的品级,且该品级下尚无同名条目。
            drift = None
            if use_rarity:
                for entry in sections[name]:
                    if entry["title"] == title and (entry["rarity"], title) not in present:
                        drift = entry
                        break
            if drift is not None:
                new_prefix = "#" if drift["disabled"] else ""
                edits[i] = f"{new_prefix}{drift['rarity']}：{title}：{desc}"
                prefix_fixed.append(f"{name}:{rarity}:{title} 品级改为 {drift['rarity']}")
                seen.setdefault((drift["rarity"], title), i)
            else:
                stale.append(
                    {
                        "anchor": f"{name}:{rarity}:{title}" if use_rarity else f"{name}:{title}",
                        "rarity": rarity,
                        "title": title,
                        "desc": desc,
                        "line": i,
                    }
                )

        # 缺失条目: 记录插入点(对应品级块末尾;无该品级块则章节末尾)。
        for key, entry in want.items():
            if key in seen:
                continue
            pos = last_line_of_rarity.get(entry["rarity"] if use_rarity else None)
            if pos is None:
                pos = end - 1
                while pos > start and not lines[pos].strip():
                    pos -= 1
            anchor = (
                f"{name}:{entry['rarity']}:{entry['title']}"
                if use_rarity
                else f"{name}:{entry['title']}"
            )
            missing.append(
                {"anchor": anchor, "entry": entry, "pos": pos + 1, "use_rarity": use_rarity}
            )

    for name in TAG_SECTION_ORDER:
        handle_section(name, use_rarity=True)
    handle_section("怪物", use_rarity=True)
    handle_section("属性锻造器", use_rarity=True)
    handle_section("卡牌", use_rarity=False)
    handle_section("事件遗物", use_rarity=False)

    # 跨节漂移: 缺失条目与已移除条目按(品级,标题)配对 -> 移动既有手写行,不重写描述。
    moved_notes: list[str] = []
    for miss in missing:
        entry = miss["entry"]
        match = next(
            (
                item
                for item in stale
                if item["rarity"] == entry["rarity"] and item["title"] == entry["title"]
            ),
            None,
        )
        if match is None:
            continue
        miss["move_desc"] = match["desc"]
        stale.remove(match)
        edits[match["line"]] = None
        moved_notes.append(f"{match['anchor']} -> {miss['anchor']}")

    added: list[str] = []
    for miss in missing:
        entry = miss["entry"]
        prefix = "#" if entry["disabled"] and miss["use_rarity"] else ""
        head = f"{prefix}{entry['rarity']}：" if miss["use_rarity"] else ""
        desc = miss.get("move_desc", f"{entry['desc']}{entry['suffix']}")
        inserts.setdefault(miss["pos"], []).append(f"{head}{entry['title']}：{desc}")
        added.append(miss["anchor"])

    for item in stale:
        if prune:
            edits[item["line"]] = None

    out: list[str] = []
    for i, line in enumerate(lines):
        if i in inserts:
            out.extend(inserts[i])
        if i in edits:
            if edits[i] is not None:
                out.append(edits[i])
        else:
            out.append(line)
    if len(lines) in inserts:
        out.extend(inserts[len(lines)])

    if added:
        report.append(f"summary: 新增缺失条目 {len(added)} 条: " + "; ".join(added))
    if moved_notes:
        report.append(f"summary: 跨节移动 {len(moved_notes)} 条(保留原描述): " + "; ".join(moved_notes))
    if prefix_fixed:
        report.append(
            f"summary: 修正禁用/品级前缀 {len(prefix_fixed)} 条: " + "; ".join(prefix_fixed)
        )
    if stale:
        action = "已删除" if prune else "保留原位(--prune 才删除)"
        report.append(
            f"summary: 已从注册表移除的条目 {len(stale)} 条,{action}: "
            + "; ".join(item["anchor"] for item in stale)
        )
    if accepted:
        report.append(f"summary: 按 --accept-json 采纳 JSON 描述 {len(accepted)} 条: " + "; ".join(accepted))
    if unknown_accepts:
        report.append(f"summary: --accept-json 未匹配到锚: {', '.join(sorted(unknown_accepts))}")
    return "\n".join(out) + "\n"


# ---------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true", help="写回三个 txt(默认只检查)")
    parser.add_argument("--prune", action="store_true", help="配合 --apply,删除 summary 中已移除条目")
    parser.add_argument(
        "--accept-json",
        action="append",
        default=[],
        metavar="锚",
        help='单条采纳 JSON 描述覆盖 summary 条目,如 "怪物:棱彩:金铲铲" 或 "卡牌:白洞"',
    )
    args = parser.parse_args()

    truth = Truth()
    report: list[str] = []

    tags_new = generate_tags(truth, read(TAGS_TXT), report)
    flavors_new = generate_flavors(truth, read(FLAVORS_TXT), report)
    summary_new = sync_summary(
        truth, read(SUMMARY_TXT), report, prune=args.prune, accepts=set(args.accept_json)
    )

    changed = {
        path.name: new != read(path)
        for path, new in (
            (TAGS_TXT, tags_new),
            (FLAVORS_TXT, flavors_new),
            (SUMMARY_TXT, summary_new),
        )
    }

    for line in report:
        print(line)

    if args.apply:
        for path, new in ((TAGS_TXT, tags_new), (FLAVORS_TXT, flavors_new), (SUMMARY_TXT, summary_new)):
            if new != read(path):
                path.write_text(new, encoding="utf-8")
                print(f"已写入 {path.name}")
            else:
                print(f"{path.name} 无变化")
        return 0

    dirty = [name for name, flag in changed.items() if flag]
    if dirty:
        print(f"需要同步(--apply): {', '.join(dirty)}")
        return 1
    print("三个 txt 与真值一致(待裁决分歧除外)。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
