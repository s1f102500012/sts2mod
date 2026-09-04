using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AITeammate.Scripts;

internal static class GuideCardRewardPlanner
{
    private const double EmergingPackageSignal = 2.0d;

    private static readonly IReadOnlyDictionary<CharacterFamily, GuideProfile> Profiles =
        new Dictionary<CharacterFamily, GuideProfile>
        {
            [CharacterFamily.Ironclad] = new(
                "ironclad",
                CharacterRewardProfiles.Ironclad.PremiumAttackTokens,
                CharacterRewardProfiles.Ironclad.PremiumDefenseTokens,
                CharacterRewardProfiles.Ironclad.WeakAttackTokens,
                CharacterRewardProfiles.Ironclad.WeakDefenseTokens,
                TransitionTokens:
                [
                    "PERFECTED_STRIKE", "PERFECT_STRIKE", "完美打击",
                    "CINDER", "余烬",
                    "PRIMAL_POWER", "PRIMITIVE_POWER", "原始力量",
                    "INFLAME", "燃烧",
                    "INFERNAL_BLADE", "浴火", "狱火"
                ],
                AoeTokens:
                [
                    "BREAKTHROUGH", "DROPKICK", "突破",
                    "THUNDERCLAP", "闪电霹雳",
                    "WHIRLWIND", "旋风斩",
                    "CLEAVE", "BURN", "焚烧",
                    "INFERNAL_BLADE", "浴火", "狱火"
                ],
                DrawTokens:
                [
                    "POMMEL_STRIKE", "剑柄打击",
                    "BURNING_PACT", "燃烧契约",
                    "BATTLE_TRANCE", "战斗专注",
                    "OFFERING", "祭品",
                    "WARCRY", "WAR_CRY", "战鼓",
                    "DARK_EMBRACE", "黑暗之拥"
                ],
                EnergyTokens:
                [
                    "BLOODLETTING", "放血",
                    "SEEING_RED", "星火之源",
                    "FORGOTTEN_RITUAL", "被遗忘的仪式"
                ],
                ScalingTokens:
                [
                    "INFLAME", "燃烧",
                    "SPOT_WEAKNESS", "与我一战",
                    "LIMIT_BREAK", "突破极限",
                    "DEMON_FORM", "恶魔形态",
                    "BARRICADE", "壁垒"
                ],
                Packages:
                [
                    new(
                        "ironclad_vulnerable",
                        Enablers:
                        [
                            "BASH", "痛击",
                            "TREMBLE", "战栗",
                            "THUNDERCLAP", "闪电霹雳",
                            "UPPERCUT", "上勾拳",
                            "INTIMIDATE", "TAUNT", "挑衅"
                        ],
                        Payoffs:
                        [
                            "DROPKICK", "突破",
                            "COLOSSUS", "巨像",
                            "HEAVY_BLADE", "灰烬打击",
                            "BULLY", "欺凌",
                            "TRAMPLE", "踩踏",
                            "FEROCIOUS", "凶恶"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 12d,
                        UnsupportedPenalty: 12d),
                    new(
                        "ironclad_block_kill",
                        Enablers:
                        [
                            "RAGE", "狂怒",
                            "ARMAMENT", "武装",
                            "FLAME_BARRIER", "火焰屏障",
                            "IMPERVIOUS", "岿然不动",
                            "BARRICADE", "壁垒",
                            "ENTRENCH", "坚定不移",
                            "POWER_THROUGH", "血墙"
                        ],
                        Payoffs:
                        [
                            "BODY_SLAM", "全身撞击",
                            "JUGGERNAUT", "势不可挡"
                        ],
                        MinimumEnablers: 2,
                        SupportedBonus: 14d,
                        UnsupportedPenalty: 8d),
                    new(
                        "ironclad_strength_hits",
                        Enablers:
                        [
                            "INFLAME", "燃烧",
                            "SPOT_WEAKNESS", "与我一战",
                            "STRENGTH", "力量"
                        ],
                        Payoffs:
                        [
                            "SWORD_BOOMERANG", "飞剑回旋镖",
                            "TWIN_STRIKE", "双重打击",
                            "PUMMEL", "连环拳",
                            "WHIRLWIND", "旋风斩",
                            "BURN", "焚烧"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 10d,
                        UnsupportedPenalty: 6d)
                ],
                EarlyHardAvoidTokens:
                [
                    "BLOODLETTING", "放血",
                    "DEMON_FORM", "恶魔形态",
                    "BARRICADE", "壁垒"
                ]),
            [CharacterFamily.Silent] = new(
                "silent",
                CharacterRewardProfiles.Silent.PremiumAttackTokens,
                CharacterRewardProfiles.Silent.PremiumDefenseTokens,
                CharacterRewardProfiles.Silent.WeakAttackTokens,
                CharacterRewardProfiles.Silent.WeakDefenseTokens,
                TransitionTokens:
                [
                    "DASH", "冲刺",
                    "BACKSTAB", "背刺",
                    "ASSASSINATE", "刺杀",
                    "POISONED_STAB", "带毒刺击",
                    "DAGGER_SPRAY", "匕首雨",
                    "ECHOING_SLASH", "回响斩击"
                ],
                AoeTokens:
                [
                    "DAGGER_SPRAY", "匕首雨",
                    "ECHOING_SLASH", "回响斩击",
                    "DIE_DIE_DIE", "死吧死吧死吧",
                    "CRIPPLING_CLOUD", "迷雾",
                    "CORPSE_EXPLOSION", "尸爆"
                ],
                DrawTokens:
                [
                    "DAGGER_THROW", "投掷匕首",
                    "BACKFLIP", "后空翻",
                    "ACROBATICS", "杂技",
                    "CALCULATED_GAMBLE", "计算下注",
                    "ESCAPE_PLAN", "逃脱计划",
                    "ADRENALINE", "肾上腺素",
                    "PREPARED", "准备",
                    "REFLEX", "本能反应",
                    "WELL_LAID_PLANS", "计划妥当"
                ],
                EnergyTokens:
                [
                    "TACTICIAN", "战术大师",
                    "CONCENTRATE", "集中",
                    "SNEAKY_STRIKE", "隐秘打击",
                    "ADRENALINE", "肾上腺素"
                ],
                ScalingTokens:
                [
                    "ACCURACY", "精密瞄准",
                    "FOOTWORK", "灵动步法",
                    "WRAITH_FORM", "触不可及",
                    "MELD_WITH_SHADOWS", "融入暗影",
                    "CATALYST", "毒性爆发"
                ],
                Packages:
                [
                    new(
                        "silent_discard_trick",
                        Enablers:
                        [
                            "DISCARD", "弃牌",
                            "DAGGER_THROW", "投掷匕首",
                            "HIDDEN_DAGGERS", "隐秘匕首",
                            "PREPARED", "准备",
                            "ACROBATICS", "杂技",
                            "CALCULATED_GAMBLE", "计算下注",
                            "SURVIVOR", "生存者",
                            "BACKPACK", "背包"
                        ],
                        Payoffs:
                        [
                            "TACTICIAN", "战术大师",
                            "REFLEX", "本能反应",
                            "SNEAKY_STRIKE", "隐秘打击",
                            "EVISCERATE", "剔骨",
                            "TECHNIQUE", "技巧",
                            "REBOUNDING", "连续反弹",
                            "MIST", "迷雾"
                        ],
                        MinimumEnablers: 2,
                        SupportedBonus: 16d,
                        UnsupportedPenalty: 20d),
                    new(
                        "silent_poison_boss",
                        Enablers:
                        [
                            "DEADLY_POISON", "致命毒药",
                            "BOUNCING_FLASK", "弹跳药瓶",
                            "POISON", "毒",
                            "NOXIOUS_FUMES", "毒雾"
                        ],
                        Payoffs:
                        [
                            "CATALYST", "毒性爆发",
                            "CORPSE_EXPLOSION", "尸爆",
                            "BURST", "爆发"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 13d,
                        UnsupportedPenalty: 12d),
                    new(
                        "silent_shiv",
                        Enablers:
                        [
                            "SHIV", "小刀",
                            "BLADE_DANCE", "刀刃之舞",
                            "CLOAK_AND_DAGGER", "斗篷与匕首",
                            "STORM_OF_STEEL", "钢铁风暴"
                        ],
                        Payoffs:
                        [
                            "ACCURACY", "精密瞄准",
                            "AFTER_IMAGE", "残影",
                            "FINISHER", "终结技",
                            "THOUSAND_CUTS", "千刀万剐"
                        ],
                        MinimumEnablers: 2,
                        SupportedBonus: 10d,
                        UnsupportedPenalty: 14d)
                ],
                EarlyHardAvoidTokens:
                [
                    "TACTICIAN", "战术大师",
                    "REFLEX", "本能反应",
                    "BLADE_DANCE", "刀刃之舞",
                    "AFTER_IMAGE", "残影"
                ]),
            [CharacterFamily.Defect] = new(
                "defect",
                CharacterRewardProfiles.Defect.PremiumAttackTokens,
                CharacterRewardProfiles.Defect.PremiumDefenseTokens,
                CharacterRewardProfiles.Defect.WeakAttackTokens,
                CharacterRewardProfiles.Defect.WeakDefenseTokens,
                TransitionTokens:
                [
                    "BALL_LIGHTNING", "球状闪电",
                    "SCRAPE", "打碎",
                    "BEAM_CELL", "光束射线",
                    "SWEEPING_BEAM", "扫荡射线",
                    "SUNDER", "分离",
                    "HYPER_BEAM", "超能光束"
                ],
                AoeTokens:
                [
                    "SCRAPE", "打碎",
                    "SWEEPING_BEAM", "扫荡射线",
                    "ELECTRODYNAMICS", "电流相生",
                    "ELECTRODYNAMIC", "电动力学",
                    "REFRACTION", "折射",
                    "GLASS", "玻璃球",
                    "THUNDER", "雷霆"
                ],
                DrawTokens:
                [
                    "COMPILE_DRIVER", "编译冲击",
                    "COOLHEADED", "冰寒", "冷静头脑",
                    "SKIM", "浏览",
                    "HOLOGRAM", "全息影像",
                    "OVERCLOCK", "超频",
                    "ITERATION", "迭代",
                    "REBOOT", "重启"
                ],
                EnergyTokens:
                [
                    "CORE_ACCELERATION", "内核加速",
                    "TURBO", "超频",
                    "DOUBLE_ENERGY", "双重释放",
                    "AGGREGATE", "聚变",
                    "ENERGY_SURGE", "能量涌动"
                ],
                ScalingTokens:
                [
                    "DEFRAGMENT", "碎片整理",
                    "FOCUS", "集中",
                    "CAPACITOR", "电容",
                    "LOOP", "循环",
                    "ECHO_FORM", "回响形态",
                    "BIASED_COGNITION", "偏差认知"
                ],
                Packages:
                [
                    new(
                        "defect_status_engine",
                        Enablers:
                        [
                            "STATUS", "状态",
                            "DAZED", "眩晕",
                            "WOUND", "伤口",
                            "BURN", "灼伤",
                            "HIGH_SPEED_ESCAPE", "高速脱离",
                            "STACK", "强撑",
                            "OVERCLOCK", "超频",
                            "ITERATION", "迭代",
                            "CORE_ACCELERATION", "内核加速"
                        ],
                        Payoffs:
                        [
                            "COMPACT", "压缩",
                            "EVOLVE", "进化",
                            "FIRE_BREATHING", "火焰吐息",
                            "RECYCLE", "回收",
                            "SCRAPE", "打碎",
                            "TREASURE_TRASH", "化废为宝"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 16d,
                        UnsupportedPenalty: 6d),
                    new(
                        "defect_orb_engine",
                        Enablers:
                        [
                            "ORB", "充能球",
                            "BALL_LIGHTNING", "球状闪电",
                            "COLD_SNAP", "寒流",
                            "COOLHEADED", "冰寒", "冷静头脑",
                            "GLACIER", "冰川",
                            "RAINBOW", "彩虹",
                            "CHAOS", "混沌",
                            "DARKNESS", "漆黑"
                        ],
                        Payoffs:
                        [
                            "FOCUS", "集中",
                            "DEFRAGMENT", "碎片整理",
                            "CAPACITOR", "电容",
                            "LOOP", "循环",
                            "ELECTRODYNAMICS", "电流相生",
                            "MULTI_CAST", "多重释放",
                            "FISSION", "裂变"
                        ],
                        MinimumEnablers: 2,
                        SupportedBonus: 15d,
                        UnsupportedPenalty: 10d),
                    new(
                        "defect_zero_physical",
                        Enablers:
                        [
                            "FTL", "超越光速",
                            "LIGHTSPEED", "超越光速",
                            "BEAM_CELL", "光束射线",
                            "GO_FOR_THE_EYES", "眼部攻击",
                            "MOMENTUM_STRIKE", "趁势打击",
                            "CLAW", "爪击",
                            "ZERO", "零费"
                        ],
                        Payoffs:
                        [
                            "ALL_FOR_ONE", "万物一心",
                            "SCRAPE", "打碎",
                            "BOOT_SEQUENCE", "启动流程",
                            "WILD", "野性",
                            "HYPER_BEAM", "超能光束"
                        ],
                        MinimumEnablers: 3,
                        SupportedBonus: 12d,
                        UnsupportedPenalty: 12d)
                ],
                EarlyHardAvoidTokens:
                [
                    "CLAW", "爪击",
                    "CREATIVE_AI", "创造性AI",
                    "STORM", "雷暴",
                    "ARTIFICIAL_SYNTHESIS", "人工合成",
                    "FUSION", "聚变"
                ]),
            [CharacterFamily.Regent] = new(
                "regent",
                CharacterRewardProfiles.Regent.PremiumAttackTokens,
                CharacterRewardProfiles.Regent.PremiumDefenseTokens,
                CharacterRewardProfiles.Regent.WeakAttackTokens,
                CharacterRewardProfiles.Regent.WeakDefenseTokens,
                TransitionTokens:
                [
                    "COLLISION_COURSE", "碰撞轨迹",
                    "BEGONE", "下去",
                    "GUIDING_STAR", "明耀打击",
                    "CRASH_LANDING", "迫降",
                    "STAR_LANCE", "星月长矛",
                    "ASTRAL_PULSE", "星界脉冲"
                ],
                AoeTokens:
                [
                    "CRASH_LANDING", "迫降",
                    "ASTRAL_PULSE", "星界脉冲",
                    "STARDUST", "星灭",
                    "GAMMA_BLAST", "伽马爆破"
                ],
                DrawTokens:
                [
                    "PHOTON_CUT", "光子切割",
                    "GLOW", "辉光",
                    "GLIMMER", "微光",
                    "CHARGE", "冲锋",
                    "FIXED_EVENT", "既定事象",
                    "TYRANNY", "暴政",
                    "BIG_BANG", "大爆炸",
                    "PROPHECY", "预言"
                ],
                EnergyTokens:
                [
                    "GLOW", "辉光",
                    "TREASURE", "藏品",
                    "STAR_SEQUENCE", "星位序列",
                    "ORBIT", "环绕轨道",
                    "CONFLUENCE", "汇流",
                    "GUIDING_STAR", "明耀打击"
                ],
                ScalingTokens:
                [
                    "STAR", "星辉",
                    "FORGE", "铸造",
                    "COLORLESS", "无色",
                    "VIGOR", "活力",
                    "FURNACE", "熔炉",
                    "ARSENAL", "军火库",
                    "ORBIT", "环绕轨道"
                ],
                Packages:
                [
                    new(
                        "regent_starlight",
                        Enablers:
                        [
                            "STAR", "星辉",
                            "GLOW", "辉光",
                            "STAR_SEQUENCE", "星位序列",
                            "ORBIT", "环绕轨道",
                            "VENERATE", "天穹之力",
                            "CLOAK_OF_STARS", "群星斗篷"
                        ],
                        Payoffs:
                        [
                            "GAMMA_BLAST", "伽马爆破",
                            "SEVEN_STARS", "七星",
                            "STARDUST", "星灭",
                            "PARTICLE_WALL", "粒子墙",
                            "DYING_STAR", "星灭"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 14d,
                        UnsupportedPenalty: 10d),
                    new(
                        "regent_forge",
                        Enablers:
                        [
                            "FORGE", "铸造",
                            "THE_SMITH", "锻打成型",
                            "BUILD_WALL", "筑墙",
                            "CONQUEROR", "征服者",
                            "SPOILS_OF_BATTLE", "战利品"
                        ],
                        Payoffs:
                        [
                            "FORGE_BLADE", "铸造之刃",
                            "SWORDSMITH", "铸剑者",
                            "CONSCRIPT", "征召上前",
                            "THE_SMITH", "锻打成型"
                        ],
                        MinimumEnablers: 2,
                        SupportedBonus: 14d,
                        UnsupportedPenalty: 12d),
                    new(
                        "regent_colorless",
                        Enablers:
                        [
                            "COLORLESS", "无色",
                            "MAKE_IT_SO", "君权自授",
                            "QUASAR", "类星体",
                            "ARSENAL", "军火库",
                            "PRISM", "棱镜"
                        ],
                        Payoffs:
                        [
                            "FURNACE", "熔炉",
                            "ORBIT", "环绕轨道",
                            "SUPERMASSIVE", "超质量体",
                            "SPECTRAL_SHIFT", "光谱偏移"
                        ],
                        MinimumEnablers: 2,
                        SupportedBonus: 10d,
                        UnsupportedPenalty: 11d)
                ],
                EarlyHardAvoidTokens:
                [
                    "DECISION", "抉择",
                    "ORBIT", "环绕轨道",
                    "GLITTERSTREAM", "流光溢彩",
                    "MONARCHS_GAZE", "王之凝视",
                    "PARRY", "招架"
                ]),
            [CharacterFamily.Necrobinder] = new(
                "necrobinder",
                CharacterRewardProfiles.Necrobinder.PremiumAttackTokens,
                CharacterRewardProfiles.Necrobinder.PremiumDefenseTokens,
                CharacterRewardProfiles.Necrobinder.WeakAttackTokens,
                CharacterRewardProfiles.Necrobinder.WeakDefenseTokens,
                TransitionTokens:
                [
                    "POKE", "戳击",
                    "NEGATIVE_PULSE", "负能量脉冲",
                    "BONE_SHARDS", "碎骨",
                    "DOOMSDAY", "末日降临",
                    "SOWING", "播种",
                    "ROOT_OUT", "根除"
                ],
                AoeTokens:
                [
                    "NEGATIVE_PULSE", "负能量脉冲",
                    "BONE_SHARDS", "碎骨",
                    "DEATHBRINGER", "死亡使者",
                    "SOWING", "播种",
                    "DOOMSDAY", "末日降临"
                ],
                DrawTokens:
                [
                    "DEPRIVE", "剥夺",
                    "FETCH", "取回",
                    "SCAVENGE", "清淤",
                    "COMPREHEND", "领会",
                    "CAPTURE_SOUL", "捕捉灵魂",
                    "DESCEND", "降临",
                    "DIRGE", "挽歌"
                ],
                EnergyTokens:
                [
                    "BORROWED_TIME", "预借时间",
                    "MENTAL_OVERLOAD", "精神过载",
                    "EVOKE", "唤起",
                    "GHOST_FIRE", "鬼火",
                    "DOMAIN", "领域",
                    "FRIENDSHIP", "友谊"
                ],
                ScalingTokens:
                [
                    "CALAMITY", "灾厄",
                    "VOID", "虚无",
                    "SOUL", "灵魂",
                    "SUMMON", "召唤",
                    "OSTY", "奥斯提",
                    "WRAITH_PULL", "亡魂牵引"
                ],
                Packages:
                [
                    new(
                        "necrobinder_void",
                        Enablers:
                        [
                            "VOID", "虚无",
                            "VEILPIERCER", "刺破帷幕",
                            "WRAITH_PULL", "亡魂牵引",
                            "ASHEN_SPIRIT", "灰烬之灵",
                            "DEADLY", "致死性"
                        ],
                        Payoffs:
                        [
                            "WRAITH_PULL", "亡魂牵引",
                            "QUEEN_HOWL", "女王之嚎",
                            "BURY", "埋葬",
                            "VOID_ILLUSION", "虚空之幻"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 14d,
                        UnsupportedPenalty: 9d),
                    new(
                        "necrobinder_calamity",
                        Enablers:
                        [
                            "CALAMITY", "灾厄",
                            "NEGATIVE_PULSE", "负能量脉冲",
                            "DEATHS_DOOR", "死亡之门",
                            "DOOM", "厄运",
                            "NO_ESCAPE", "无处可逃"
                        ],
                        Payoffs:
                        [
                            "NO_ESCAPE", "无处可逃",
                            "DEADLINE", "大限已至",
                            "DOOM_SOUND", "厄运之音",
                            "WEAKENING_TRICK", "削弱戏法"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 14d,
                        UnsupportedPenalty: 10d),
                    new(
                        "necrobinder_soul",
                        Enablers:
                        [
                            "SOUL", "灵魂",
                            "CAPTURE_SOUL", "捕捉灵魂",
                            "COMPREHEND", "领会",
                            "DEATH_MARCH", "死亡行军"
                        ],
                        Payoffs:
                        [
                            "ANNIHILATE", "湮灭",
                            "HANGING", "吊杀",
                            "RECONSTRUCT", "重构",
                            "DIRGE", "挽歌"
                        ],
                        MinimumEnablers: 2,
                        SupportedBonus: 13d,
                        UnsupportedPenalty: 10d),
                    new(
                        "necrobinder_osty",
                        Enablers:
                        [
                            "SUMMON", "召唤",
                            "OSTY", "奥斯提",
                            "POKE", "戳击",
                            "BONE_SHARDS", "碎骨",
                            "GUARD_MODE", "守卫模式"
                        ],
                        Payoffs:
                        [
                            "PRESSURE", "重压",
                            "CALCIFY", "钙化",
                            "FERMENT", "猛化",
                            "EXTRACT", "榨取"
                        ],
                        MinimumEnablers: 1,
                        SupportedBonus: 11d,
                        UnsupportedPenalty: 8d)
                ],
                EarlyHardAvoidTokens:
                [
                    "DRAIN_POWER", "能量汲取",
                    "GRAB", "抓取",
                    "BURY", "埋葬",
                    "REANIMATE", "死者苏生",
                    "SACRIFICE", "牺牲"
                ])
        };

    public static double ScoreRewardCard(ResolvedCardView card, CardEvaluationContext context)
    {
        CharacterFamily family = ResolveFamily(context, card);
        if (!Profiles.TryGetValue(family, out GuideProfile? profile))
        {
            return 0d;
        }

        DeckSummary deck = context.DeckSummary;
        double score = 0d;
        bool foundationReady = HasEnoughFoundation(deck);
        bool actOne = context.CurrentActIndex == 0;
        bool earlyActOne = actOne && (context.ActFloor <= 8 || context.TotalFloor <= 8);

        score += ScoreBucket(card, context, profile.PremiumAttacks, GuideRole.Attack, desiredGroupTotal: 3, desiredCopies: 1, baseScore: 28d);
        score += ScoreBucket(card, context, profile.PremiumDefenses, GuideRole.Defense, desiredGroupTotal: 3, desiredCopies: 1, baseScore: 29d);
        score += ScoreBucket(card, context, profile.AoeTokens, GuideRole.Aoe, desiredGroupTotal: 2, desiredCopies: 1, baseScore: 22d);
        score += ScoreBucket(card, context, profile.DrawTokens, GuideRole.Draw, desiredGroupTotal: DesiredDrawSourcesForGuide(family, deck), desiredCopies: 2, baseScore: 18d);
        score += ScoreBucket(card, context, profile.EnergyTokens, GuideRole.Energy, desiredGroupTotal: DesiredEnergySourcesForGuide(deck), desiredCopies: 2, baseScore: 15d);
        score += ScoreBucket(card, context, profile.ScalingTokens, GuideRole.Scaling, desiredGroupTotal: DesiredScalingSourcesForGuide(deck), desiredCopies: 1, baseScore: 16d);

        score += ScoreGuidePackages(card, context, profile, foundationReady);
        score += ScoreGuideAvoidance(card, context, profile, family);
        score += ScoreGuidePhaseDiscipline(card, context, profile, family, foundationReady, earlyActOne);
        score += ScoreGuideCleanup(card, context);

        return Math.Clamp(score, -70d, 90d);
    }

    private static double ScoreBucket(
        ResolvedCardView card,
        CardEvaluationContext context,
        IReadOnlyList<string> tokens,
        GuideRole role,
        int desiredGroupTotal,
        int desiredCopies,
        double baseScore)
    {
        if (!MatchesTokens(card, tokens) && !MatchesRoleByEffect(card, role))
        {
            return 0d;
        }

        DeckSummary deck = context.DeckSummary;
        int groupCopies = CountMatches(context.DeckCards, tokens, role);
        int sameCopies = CountSameCard(context.DeckCards, card);
        if (desiredGroupTotal > 0 && groupCopies >= desiredGroupTotal)
        {
            return -GetOverPlanPenalty(role, card, context, groupCopies, desiredGroupTotal);
        }

        if (desiredCopies > 0 && sameCopies >= desiredCopies)
        {
            return -GetDuplicatePlanPenalty(role, card, context, sameCopies);
        }

        double score = baseScore;
        score *= GetRoleNeedMultiplier(role, card, context);
        score *= GetPhaseMultiplier(role, context);

        if (role == GuideRole.Attack && context.CurrentActIndex >= 1 && IsPlainTransitionCard(card))
        {
            score *= 0.45d;
        }

        if (role == GuideRole.Draw && context.CurrentActIndex == 0 && !HasEnoughFoundation(deck) && !HasImmediateCombatValue(card))
        {
            score *= 0.38d;
        }

        if (role == GuideRole.Energy && !CanConvertEnergyToCards(deck, card))
        {
            score -= 18d;
        }

        if (role == GuideRole.Scaling && context.CurrentActIndex == 0 && context.TotalFloor <= 5 && !HasImmediateCombatValue(card))
        {
            score *= 0.45d;
        }

        if (context.ChoiceSource == CardChoiceSource.Shop)
        {
            score *= role is GuideRole.Attack or GuideRole.Defense ? 0.9d : 0.96d;
        }

        return score;
    }

    private static double ScoreGuidePackages(
        ResolvedCardView card,
        CardEvaluationContext context,
        GuideProfile profile,
        bool foundationReady)
    {
        double score = 0d;
        foreach (GuidePackage package in profile.Packages)
        {
            int enablers = CountMatches(context.DeckCards, package.Enablers);
            int payoffs = CountMatches(context.DeckCards, package.Payoffs);
            bool isEnabler = MatchesTokens(card, package.Enablers);
            bool isPayoff = MatchesTokens(card, package.Payoffs);
            if (!isEnabler && !isPayoff)
            {
                continue;
            }

            if (isEnabler)
            {
                double enablerScore = foundationReady || HasImmediateCombatValue(card)
                    ? 8d + Math.Min(payoffs, 3) * 2d
                    : 3d;
                if (enablers >= package.MinimumEnablers + 2 && context.CurrentActIndex == 0)
                {
                    enablerScore *= 0.55d;
                }

                score += enablerScore;
            }

            if (isPayoff)
            {
                if (enablers >= package.MinimumEnablers)
                {
                    score += package.SupportedBonus + Math.Min(enablers, 5) * 2.2d;
                }
                else
                {
                    double penalty = package.UnsupportedPenalty;
                    if (context.CurrentActIndex == 0 && !foundationReady)
                    {
                        penalty *= 1.45d;
                    }

                    score -= penalty;
                }

                if (payoffs >= 2 && !IsTerminalPayoff(card))
                {
                    score -= 6d;
                }
            }
        }

        return score;
    }

    private static double ScoreGuideAvoidance(
        ResolvedCardView card,
        CardEvaluationContext context,
        GuideProfile profile,
        CharacterFamily family)
    {
        double score = 0d;
        bool actOne = context.CurrentActIndex == 0;
        bool earlyActOne = actOne && (context.ActFloor <= 8 || context.TotalFloor <= 8);
        if (MatchesTokens(card, profile.WeakAttacks) || MatchesTokens(card, profile.WeakDefenses))
        {
            double penalty = actOne ? earlyActOne ? 28d : 18d : 8d;
            if (HasPackageSupport(card, context, profile))
            {
                penalty *= 0.45d;
            }

            score -= penalty;
        }

        if (earlyActOne && MatchesTokens(card, profile.EarlyHardAvoidTokens) && !HasPackageSupport(card, context, profile))
        {
            score -= 18d;
        }

        score += family switch
        {
            CharacterFamily.Ironclad => ScoreIroncladGuideExceptions(card, context),
            CharacterFamily.Silent => ScoreSilentGuideExceptions(card, context),
            CharacterFamily.Defect => ScoreDefectGuideExceptions(card, context),
            CharacterFamily.Regent => ScoreRegentGuideExceptions(card, context),
            CharacterFamily.Necrobinder => ScoreNecrobinderGuideExceptions(card, context),
            _ => 0d
        };

        return score;
    }

    private static double ScoreGuidePhaseDiscipline(
        ResolvedCardView card,
        CardEvaluationContext context,
        GuideProfile profile,
        CharacterFamily family,
        bool foundationReady,
        bool earlyActOne)
    {
        DeckSummary deck = context.DeckSummary;
        double score = 0d;

        if (MatchesTokens(card, profile.TransitionTokens))
        {
            int transitionCopies = CountMatches(context.DeckCards, profile.TransitionTokens);
            if (transitionCopies >= 2 && (deck.QualityDamageSources >= DesiredQualityDamageSources(deck) || context.CurrentActIndex >= 1))
            {
                score -= context.CurrentActIndex == 0 ? 12d : 20d;
            }

            if (context.CurrentActIndex >= 1 && IsPlainTransitionCard(card))
            {
                score -= 10d;
            }
        }

        if (foundationReady && IsLowImpactPlainCard(card) && !HasPackageSupport(card, context, profile))
        {
            score -= context.SkipAllowed ? 12d : 7d;
        }

        if (!foundationReady && earlyActOne && IsFutureOnlyCard(card))
        {
            score -= 18d;
        }

        if (NeedsMoreDefense(deck) && card.GetEstimatedProtection() > 0 && card.GetCardsDrawn() > 0)
        {
            score += 6d;
        }

        if (NeedsMoreDraw(deck) && card.GetCardsDrawn() > 0 && family == CharacterFamily.Defect)
        {
            score += 8d;
        }

        if (NeedsMoreDamage(deck) && (card.DealsDamageToAllEnemies() || card.GetEnemyVulnerableAmount() > 0))
        {
            score += 5d;
        }

        return score;
    }

    private static double ScoreGuideCleanup(ResolvedCardView card, CardEvaluationContext context)
    {
        if (!StatusCardStrategy.IsLikelyHandCleanupCard(card))
        {
            return 0d;
        }

        DeckSummary deck = context.DeckSummary;
        double score = deck.StatusHandlingCards == 0
            ? context.CurrentActIndex == 0 ? 28d : 20d
            : deck.BadCards > 0 ? 12d : 4d;

        if (card.GetCardsDrawn() > 0)
        {
            score += Math.Min(card.GetCardsDrawn(), 3) * 3d;
        }

        if (deck.StatusHandlingCards >= 2 && deck.BadCards == 0)
        {
            score -= 10d;
        }

        return score;
    }

    private static double ScoreIroncladGuideExceptions(ResolvedCardView card, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        double score = 0d;
        if (MatchesTokens(card, ["BLOODLETTING", "放血"]) &&
            context.CurrentActIndex == 0 &&
            deck.DrawSources < 2 &&
            deck.ZeroCostCards >= 2)
        {
            score -= 34d;
        }

        if (MatchesTokens(card, ["BODY_SLAM", "全身撞击"]) &&
            context.CurrentActIndex == 0 &&
            context.ActFloor <= 5 &&
            deck.UpgradedCardCount <= 1 &&
            deck.QualityDefenseSources <= 1)
        {
            score -= 8d;
        }

        if (MatchesTokens(card, ["ARMAMENT", "ARMAMENTS", "武装"]))
        {
            score += 14d;
        }

        if (context.CurrentActIndex >= 1 && NeedsMoreDefense(deck) && card.GetEstimatedProtection() > 0)
        {
            score += 8d;
        }

        return score;
    }

    private static double ScoreSilentGuideExceptions(ResolvedCardView card, CardEvaluationContext context)
    {
        int discardEnablers = CountMatches(context.DeckCards,
        [
            "DISCARD", "弃牌",
            "DAGGER_THROW", "投掷匕首",
            "HIDDEN_DAGGERS", "隐秘匕首",
            "PREPARED", "准备",
            "ACROBATICS", "杂技",
            "CALCULATED_GAMBLE", "计算下注",
            "SURVIVOR", "生存者"
        ]);
        double score = 0d;
        if (MatchesTokens(card, ["TACTICIAN", "战术大师", "REFLEX", "本能反应", "TECHNIQUE", "技巧"]) &&
            discardEnablers < 2)
        {
            score -= context.CurrentActIndex == 0 ? 28d : 12d;
        }

        if (MatchesTokens(card, ["DEADLY_POISON", "致命毒药", "BOUNCING_FLASK", "弹跳药瓶"]) &&
            context.CurrentActIndex == 0 &&
            context.DeckSummary.AoESources <= 1)
        {
            score += 8d;
        }

        if (MatchesTokens(card, ["WRAITH_FORM", "触不可及", "MELD_WITH_SHADOWS", "融入暗影"]) &&
            CountSameCard(context.DeckCards, card) == 0)
        {
            score += 12d;
        }

        return score;
    }

    private static double ScoreDefectGuideExceptions(ResolvedCardView card, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        double score = 0d;
        if (MatchesTokens(card, ["COMPILE_DRIVER", "编译冲击", "COOLHEADED", "冰寒", "冷静头脑", "HOLOGRAM", "全息影像"]))
        {
            score += deck.DrawSources < DesiredDrawSourcesForGuide(CharacterFamily.Defect, deck) ? 12d : 3d;
        }

        if (MatchesTokens(card, ["CORE_ACCELERATION", "内核加速"]) && deck.DrawSources >= 2)
        {
            score += 10d;
        }

        if (MatchesTokens(card, ["CLAW", "爪击"]) &&
            CountMatches(context.DeckCards, ["ALL_FOR_ONE", "万物一心", "SCRAPE", "打碎", "ZERO", "零费"]) < 2)
        {
            score -= 26d;
        }

        if (MatchesTokens(card, ["CREATIVE_AI", "创造性AI", "STORM", "雷暴"]) &&
            deck.PowerCount < 3 &&
            context.CurrentActIndex <= 1)
        {
            score -= 22d;
        }

        return score;
    }

    private static double ScoreRegentGuideExceptions(ResolvedCardView card, CardEvaluationContext context)
    {
        int blueSupport = CountMatches(context.DeckCards,
        [
            "GLOW", "辉光",
            "STAR_SEQUENCE", "星位序列",
            "ORBIT", "环绕轨道",
            "CONFLUENCE", "汇流",
            "GUIDING_STAR", "明耀打击"
        ]);
        double score = 0d;
        if (MatchesTokens(card, ["GAMMA_BLAST", "伽马爆破", "BURY", "葬送", "DECISION", "抉择", "ORBIT", "环绕轨道"]) &&
            blueSupport == 0 &&
            context.CurrentActIndex == 0)
        {
            score -= MatchesTokens(card, ["BURY", "葬送"]) ? 4d : 18d;
        }

        if (MatchesTokens(card, ["PHOTON_CUT", "光子切割", "MAKE_IT_SO", "君权自授", "GUARDS", "护驾"]))
        {
            score += 10d;
        }

        if (MatchesTokens(card, ["SPECTRAL_SHIFT", "光谱偏移", "PROPHECY", "预言"]) &&
            CountMatches(context.DeckCards, ["COLORLESS", "无色", "MAKE_IT_SO", "君权自授", "ARSENAL", "军火库"]) < 2)
        {
            score -= 14d;
        }

        return score;
    }

    private static double ScoreNecrobinderGuideExceptions(ResolvedCardView card, CardEvaluationContext context)
    {
        double score = 0d;
        if (MatchesTokens(card, ["DEATHS_DOOR", "死亡之门", "NO_ESCAPE", "无处可逃", "WRAITH_PULL", "亡魂牵引"]))
        {
            score += 12d;
        }

        if (card.GetSummonAmount() > 0)
        {
            score += NeedsMoreDefense(context.DeckSummary) ? 8d : 3d;
            if (CountMatches(context.DeckCards, ["SUMMON", "召唤", "OSTY", "奥斯提", "BONE_SHARDS", "碎骨"]) == 0 &&
                context.CurrentActIndex == 0)
            {
                score -= 10d;
            }
        }

        if (MatchesTokens(card, ["BORROWED_TIME", "预借时间"]))
        {
            score += 10d;
        }

        if (MatchesTokens(card, ["REANIMATE", "死者苏生", "SACRIFICE", "牺牲"]) &&
            CountMatches(context.DeckCards, ["EXTRACT", "榨取", "CALCIFY", "钙化", "SUMMON", "召唤"]) < 2)
        {
            score -= 16d;
        }

        return score;
    }

    private static bool HasPackageSupport(ResolvedCardView card, CardEvaluationContext context, GuideProfile profile)
    {
        foreach (GuidePackage package in profile.Packages)
        {
            if (!MatchesTokens(card, package.Payoffs))
            {
                continue;
            }

            if (CountMatches(context.DeckCards, package.Enablers) >= package.MinimumEnablers)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesRoleByEffect(ResolvedCardView card, GuideRole role)
    {
        return role switch
        {
            GuideRole.Attack => card.Type == CardType.Attack && card.GetEstimatedDamage() >= 12,
            GuideRole.Defense => card.GetEstimatedProtection() >= 9 || card.GetEnemyWeakAmount() > 0,
            GuideRole.Aoe => card.DealsDamageToAllEnemies(),
            GuideRole.Draw => card.GetCardsDrawn() > 0,
            GuideRole.Energy => card.GetEnergyGain() > 0,
            GuideRole.Scaling => card.Type == CardType.Power ||
                                 card.GetSelfStrengthAmount() > card.GetSelfTemporaryStrengthAmount() ||
                                 card.GetSelfDexterityAmount() > card.GetSelfTemporaryDexterityAmount(),
            _ => false
        };
    }

    private static double GetRoleNeedMultiplier(GuideRole role, ResolvedCardView card, CardEvaluationContext context)
    {
        DeckSummary deck = context.DeckSummary;
        bool defenseBehind = NeedsMoreDefense(deck);
        bool drawBehind = NeedsMoreDraw(deck);
        bool damageBehind = NeedsMoreDamage(deck);
        bool severeDamageDeficit = HasSevereDamageDeficit(deck);
        return role switch
        {
            GuideRole.Attack when severeDamageDeficit => 1.5d,
            GuideRole.Attack when IsAttackAhead(deck) && (defenseBehind || drawBehind) => 0.38d,
            GuideRole.Attack when damageBehind && !defenseBehind => 1.22d,
            GuideRole.Defense when defenseBehind => 1.48d,
            GuideRole.Defense when severeDamageDeficit && card.GetEnemyWeakAmount() == 0 && card.GetCardsDrawn() == 0 => 0.65d,
            GuideRole.Aoe when deck.AoESources == 0 && context.CurrentActIndex <= 1 => 1.62d,
            GuideRole.Aoe when deck.AoESources >= 2 => 0.45d,
            GuideRole.Draw when drawBehind => deck.EnergySources > deck.DrawSources ? 1.62d : 1.32d,
            GuideRole.Draw when deck.DrawSources >= DesiredDrawSources(deck) + 1 => 0.58d,
            GuideRole.Energy when deck.DrawSources >= 2 && NeedsMoreEnergy(deck) => 1.25d,
            GuideRole.Energy when deck.DrawSources <= 1 || deck.EnergySources >= DesiredEnergySources(deck) => 0.42d,
            GuideRole.Scaling when context.CurrentActIndex >= 1 || context.TotalFloor >= 10 => 1.22d,
            _ => 1d
        };
    }

    private static double GetPhaseMultiplier(GuideRole role, CardEvaluationContext context)
    {
        if (context.CurrentActIndex == 0 && context.TotalFloor <= 8)
        {
            return role is GuideRole.Attack or GuideRole.Defense or GuideRole.Aoe ? 1.16d : 0.82d;
        }

        if (context.CurrentActIndex == 0)
        {
            return role is GuideRole.Draw or GuideRole.Energy or GuideRole.Scaling ? 1.04d : 0.96d;
        }

        return role is GuideRole.Attack or GuideRole.Aoe ? 0.76d : 1.1d;
    }

    private static double GetOverPlanPenalty(GuideRole role, ResolvedCardView card, CardEvaluationContext context, int count, int target)
    {
        if (role == GuideRole.Attack && HasSevereDamageDeficit(context.DeckSummary))
        {
            return 0d;
        }

        if (role == GuideRole.Defense && NeedsMoreDefense(context.DeckSummary))
        {
            return 0d;
        }

        double penalty = role switch
        {
            GuideRole.Attack when IsPlainTransitionCard(card) => 16d,
            GuideRole.Attack => 10d,
            GuideRole.Defense => 8d,
            GuideRole.Draw => 7d,
            GuideRole.Energy => 10d,
            GuideRole.Aoe => 9d,
            GuideRole.Scaling => 7d,
            _ => 6d
        };
        return penalty + Math.Max(0, count - target) * 2d;
    }

    private static double GetDuplicatePlanPenalty(GuideRole role, ResolvedCardView card, CardEvaluationContext context, int sameCopies)
    {
        if (role == GuideRole.Draw && context.DeckSummary.DrawSources < DesiredDrawSources(context.DeckSummary))
        {
            return 0d;
        }

        if (role == GuideRole.Defense && NeedsMoreDefense(context.DeckSummary))
        {
            return 0d;
        }

        if (role == GuideRole.Attack && HasSevereDamageDeficit(context.DeckSummary))
        {
            return 0d;
        }

        return (role == GuideRole.Attack && IsPlainTransitionCard(card) ? 12d : 6d) + sameCopies * 2d;
    }

    private static int CountMatches(IEnumerable<ResolvedCardView> deckCards, IReadOnlyList<string> tokens, GuideRole? role = null)
    {
        return deckCards.Count(card => MatchesTokens(card, tokens) || role.HasValue && MatchesRoleByEffect(card, role.Value));
    }

    private static int CountSameCard(IEnumerable<ResolvedCardView> deckCards, ResolvedCardView card)
    {
        return deckCards.Count(deckCard => string.Equals(deckCard.CardId, card.CardId, StringComparison.Ordinal));
    }

    private static bool MatchesTokens(ResolvedCardView card, IReadOnlyList<string> tokens)
    {
        return tokens.Count > 0 && card.MatchesCardToken(tokens.ToArray());
    }

    private static bool HasEnoughFoundation(DeckSummary deck)
    {
        return deck.QualityDamageSources >= DesiredQualityDamageSources(deck) &&
               deck.QualityDefenseSources >= Math.Max(2, DesiredQualityDefenseSources(deck) - 1) &&
               deck.FrontloadDamageSources >= Math.Max(4, DesiredDamageSources(deck) - 1);
    }

    private static bool CanConvertEnergyToCards(DeckSummary deck, ResolvedCardView card)
    {
        return card.GetCardsDrawn() > 0 ||
               deck.DrawSources >= 2 ||
               deck.HighCostCards >= 4 ||
               deck.AverageCost >= 1.35d ||
               deck.CardCount >= 20;
    }

    private static bool HasImmediateCombatValue(ResolvedCardView card)
    {
        return card.GetEstimatedDamage() > 0 ||
               card.GetEstimatedProtection() > 0 ||
               card.GetEnemyWeakAmount() > 0 ||
               card.GetEnemyVulnerableAmount() > 0 ||
               card.GetEnemyPoisonAmount() > 0;
    }

    private static bool IsPlainTransitionCard(ResolvedCardView card)
    {
        return card.Type == CardType.Attack &&
               card.GetEstimatedDamage() > 0 &&
               card.GetCardsDrawn() == 0 &&
               card.GetEnergyGain() == 0 &&
               card.GetEnemyWeakAmount() == 0 &&
               card.GetEnemyVulnerableAmount() == 0 &&
               card.GetSelfStrengthAmount() == 0 &&
               card.GetSelfDexterityAmount() == 0 &&
               !card.DealsDamageToAllEnemies();
    }

    private static bool IsLowImpactPlainCard(ResolvedCardView card)
    {
        int value = card.GetEstimatedDamage() +
                    card.GetEstimatedProtection() +
                    card.GetCardsDrawn() * 4 +
                    card.GetEnergyGain() * 5 +
                    card.GetEnemyWeakAmount() * 3 +
                    card.GetEnemyVulnerableAmount() * 3 +
                    card.GetRecognizedUtilityAmount();
        return card.Type is CardType.Attack or CardType.Skill && value <= 10;
    }

    private static bool IsFutureOnlyCard(ResolvedCardView card)
    {
        return card.Type == CardType.Power && !HasImmediateCombatValue(card) ||
               card.GetEnergyGain() > 0 && card.GetCardsDrawn() == 0 ||
               card.EffectiveCost >= 3 && card.GetEstimatedDamage() + card.GetEstimatedProtection() < 20 ||
               card.MatchesCardToken("FORM", "形态", "BARRICADE", "壁垒", "CREATIVE_AI", "创造性AI", "DECISION", "抉择");
    }

    private static bool IsTerminalPayoff(ResolvedCardView card)
    {
        return card.Type == CardType.Power ||
               card.MatchesCardToken(
                   "DEMON_FORM", "恶魔形态",
                   "BARRICADE", "壁垒",
                   "ECHO_FORM", "回响形态",
                   "ALL_FOR_ONE", "万物一心",
                   "WRAITH_PULL", "亡魂牵引",
                   "NO_ESCAPE", "无处可逃",
                   "DEADLINE", "大限已至",
                   "FORGE_BLADE", "铸造之刃");
    }

    private static CharacterFamily ResolveFamily(CardEvaluationContext context, ResolvedCardView? candidate)
    {
        string characterId = Normalize(context.Player.Character.Id.Entry);
        if (characterId.Contains("IRONCLAD", StringComparison.Ordinal))
        {
            return CharacterFamily.Ironclad;
        }

        if (characterId.Contains("SILENT", StringComparison.Ordinal))
        {
            return CharacterFamily.Silent;
        }

        if (characterId.Contains("DEFECT", StringComparison.Ordinal))
        {
            return CharacterFamily.Defect;
        }

        if (characterId.Contains("REGENT", StringComparison.Ordinal))
        {
            return CharacterFamily.Regent;
        }

        if (characterId.Contains("NECROBINDER", StringComparison.Ordinal) || characterId.Contains("NECRO", StringComparison.Ordinal))
        {
            return CharacterFamily.Necrobinder;
        }

        IEnumerable<ResolvedCardView> cards = candidate != null
            ? context.DeckCards.Append(candidate)
            : context.DeckCards;
        return GuessFamily(cards);
    }

    private static CharacterFamily GuessFamily(IEnumerable<ResolvedCardView> cards)
    {
        int ironclad = 0;
        int silent = 0;
        int defect = 0;
        int regent = 0;
        int necrobinder = 0;
        foreach (ResolvedCardView card in cards)
        {
            if (card.MatchesCardToken("BASH", "BLOODLETTING", "IRONCLAD", "痛击", "放血"))
            {
                ironclad++;
            }

            if (card.MatchesCardToken("SILENT", "NEUTRALIZE", "SHIV", "POISON", "生存者", "小刀", "毒"))
            {
                silent++;
            }

            if (card.MatchesCardToken("DEFECT", "ZAP", "DUALCAST", "ORB", "FOCUS", "充能球", "集中"))
            {
                defect++;
            }

            if (RegentCharacterStrategy.IsRegentCard(card))
            {
                regent++;
            }

            if (card.MatchesCardToken("NECROBINDER", "NECRO", "VOID", "CALAMITY", "SUMMON", "亡灵", "虚无", "灾厄"))
            {
                necrobinder++;
            }
        }

        (CharacterFamily Family, int Score)[] scores =
        [
            (CharacterFamily.Ironclad, ironclad),
            (CharacterFamily.Silent, silent),
            (CharacterFamily.Defect, defect),
            (CharacterFamily.Regent, regent),
            (CharacterFamily.Necrobinder, necrobinder)
        ];
        (CharacterFamily family, int score) = scores.OrderByDescending(static item => item.Score).First();
        return score > 0 ? family : CharacterFamily.Unknown;
    }

    private static int DesiredDamageSources(DeckSummary deck)
    {
        return deck.CardCount < 12 ? 5 : deck.CardCount < 20 ? 6 : 7;
    }

    private static int DesiredQualityDamageSources(DeckSummary deck)
    {
        return deck.CardCount < 12 ? 2 : deck.CardCount < 20 ? 3 : 4;
    }

    private static int DesiredBlockSources(DeckSummary deck)
    {
        return deck.CardCount < 15 ? 5 : 7;
    }

    private static int DesiredQualityDefenseSources(DeckSummary deck)
    {
        return deck.CardCount < 12 ? 2 : deck.CardCount < 20 ? 4 : 6;
    }

    private static int DesiredDrawSources(DeckSummary deck)
    {
        return deck.CardCount < 18 ? 2 : deck.CardCount < 26 ? 3 : 4;
    }

    private static int DesiredDrawSourcesForGuide(CharacterFamily family, DeckSummary deck)
    {
        int baseTarget = DesiredDrawSources(deck);
        return family == CharacterFamily.Defect ? baseTarget + 1 : baseTarget;
    }

    private static int DesiredEnergySources(DeckSummary deck)
    {
        return deck.CardCount < 18 ? 1 : 2;
    }

    private static int DesiredEnergySourcesForGuide(DeckSummary deck)
    {
        return deck.CardCount < 18 ? 1 : 2;
    }

    private static int DesiredScalingSourcesForGuide(DeckSummary deck)
    {
        return deck.CardCount < 15 ? 1 : 2;
    }

    private static bool NeedsMoreDamage(DeckSummary deck)
    {
        return deck.FrontloadDamageSources < DesiredDamageSources(deck) ||
               deck.QualityDamageSources < DesiredQualityDamageSources(deck);
    }

    private static bool HasSevereDamageDeficit(DeckSummary deck)
    {
        return deck.FrontloadDamageSources <= Math.Max(2, DesiredDamageSources(deck) - 3) ||
               deck.QualityDamageSources == 0 && deck.FrontloadDamageSources < DesiredDamageSources(deck);
    }

    private static bool NeedsMoreDefense(DeckSummary deck)
    {
        return deck.BlockSources < DesiredBlockSources(deck) ||
               deck.QualityDefenseSources < DesiredQualityDefenseSources(deck);
    }

    private static bool NeedsMoreDraw(DeckSummary deck)
    {
        return deck.DrawSources < DesiredDrawSources(deck);
    }

    private static bool NeedsMoreEnergy(DeckSummary deck)
    {
        return deck.EnergySources < DesiredEnergySources(deck);
    }

    private static bool IsAttackAhead(DeckSummary deck)
    {
        return deck.FrontloadDamageSources >= DesiredDamageSources(deck) + 1 ||
               deck.QualityDamageSources >= DesiredQualityDamageSources(deck) + 1 ||
               deck.AttackCount >= deck.SkillCount + 3;
    }

    private static string Normalize(string value)
    {
        return value.Replace(' ', '_').Replace('-', '_').Replace(':', '_').Replace('/', '_').ToUpperInvariant();
    }

    private enum CharacterFamily
    {
        Unknown,
        Ironclad,
        Silent,
        Defect,
        Regent,
        Necrobinder
    }

    private enum GuideRole
    {
        Attack,
        Defense,
        Aoe,
        Draw,
        Energy,
        Scaling
    }

    private sealed record GuideProfile(
        string Id,
        IReadOnlyList<string> PremiumAttacks,
        IReadOnlyList<string> PremiumDefenses,
        IReadOnlyList<string> WeakAttacks,
        IReadOnlyList<string> WeakDefenses,
        IReadOnlyList<string> TransitionTokens,
        IReadOnlyList<string> AoeTokens,
        IReadOnlyList<string> DrawTokens,
        IReadOnlyList<string> EnergyTokens,
        IReadOnlyList<string> ScalingTokens,
        IReadOnlyList<GuidePackage> Packages,
        IReadOnlyList<string> EarlyHardAvoidTokens);

    private sealed record GuidePackage(
        string Id,
        IReadOnlyList<string> Enablers,
        IReadOnlyList<string> Payoffs,
        int MinimumEnablers,
        double SupportedBonus,
        double UnsupportedPenalty);
}
