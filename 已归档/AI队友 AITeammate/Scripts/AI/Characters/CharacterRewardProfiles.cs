namespace AITeammate.Scripts;

internal static class CharacterRewardProfiles
{
    public static readonly ActOneRewardProfile Ironclad = new()
    {
        PremiumAttackTokens =
        [
            "ANGER", "愤怒",
            "BODY_SLAM", "BodySlam", "全身撞击",
            "BREAKTHROUGH", "BREAK_THROUGH", "DROPKICK", "突破",
            "TREMBLE", "战栗",
            "PERFECTED_STRIKE", "PERFECT_STRIKE", "PerfectedStrike", "完美打击",
            "CINDER", "余烬",
            "INFLAME", "燃烧",
            "INFERNAL_BLADE", "InfernalBlade", "狱火",
            "BLOODLETTING", "OFFERING", "御血术",
            "UPPERCUT", "Uppercut", "上勾拳",
            "RAMPAGE", "无情猛攻",
            "PRIMAL_POWER", "PRIMITIVE_POWER", "BRUTALITY", "原始力量"
        ],
        WeakAttackTokens =
        [
            "RUPTURE", "撕裂",
            "BEYOND_ROAR", "OTHERWORLDLY_ROAR", "彼岸咆哮",
            "SEVER_SOUL", "契约终结",
            "BRAND", "烙印",
            "THRASH",
            "PUMMEL", "连环拳",
            "BLUDGEON", "痛殴",
            "SHRED", "REND", "TEAR_APART", "扯碎",
            "FIEND_FIRE", "恶魔之焰",
            "JUGGERNAUT", "势不可挡",
            "MALICE", "凌虐",
            "DEMON_FORM", "DemonForm", "恶魔形态"
        ],
        PremiumDefenseTokens =
        [
            "ARMAMENTS", "ARMAMENT", "武装",
            "RAGE", "Rage", "狂怒",
            "COLOSSUS", "COLOSSAL", "巨像",
            "TRUE_GRIT", "坚毅",
            "SHRUG_IT_OFF", "ShrugItOff", "耸肩无视",
            "IRON_WAVE", "铁斩波",
            "INTIMIDATE", "TAUNT", "挑衅",
            "FLAME_BARRIER", "火焰屏障",
            "CRIMSON_CLOAK", "绯红披风",
            "IMPERVIOUS", "Impervious", "岿然不动"
        ],
        WeakDefenseTokens =
        [
            "METALLICIZE", "ROCK_ARMOR", "岩石铠甲",
            "EVIL_EYE", "邪眼",
            "FEEL_NO_PAIN", "无惧疼痛",
            "NOT_THE_TIME", "NO_TIME", "时候未到",
            "BARRICADE", "Barricade", "壁垒"
        ]
    };

    public static readonly ActOneRewardProfile Silent = new()
    {
        PremiumAttackTokens =
        [
            "DAGGER_THROW", "DaggerThrow", "投掷匕首",
            "DEADLY_POISON", "DeadlyPoison", "致命毒药",
            "PRECISE_SLICE", "PRECISION_CUT", "ACCURATE_SLASH", "精确切击",
            "BACKSTAB", "Backstab", "背刺",
            "HIDDEN_DAGGERS", "HiddenDaggers", "隐秘匕首",
            "FLECHETTES", "DART", "飞镖",
            "BOUNCING_FLASK", "BouncingFlask", "弹跳药瓶",
            "DASH", "Dash", "冲刺",
            "ACCURACY", "PRECISE_AIM", "精密瞄准",
            "ASSASSINATE", "Assassinate", "刺杀",
            "THE_HUNT", "HUNT", "狩猎",
            "ECHOING_SLASH", "ECHO_SLASH", "回响斩击"
        ],
        WeakAttackTokens =
        [
            "SLICE", "Slice", "切割",
            "OPENING_STRIKE", "PREEMPTIVE_STRIKE", "FIRST_STRIKE", "先制打击",
            "BLADE_DANCE", "BladeDance", "刀刃之舞",
            "SUCKER_PUNCH", "SuckerPunch", "突然一拳",
            "SNAKEBITE", "SNAKE_BITE", "蛇咬",
            "MEMENTO_MORI", "REMEMBER_DEATH", "铭记死亡",
            "FINISHER", "Finisher", "终结技",
            "PREDATOR", "HUNTER_KILLER", "猎杀者",
            "SKEWER", "Skewer", "串刺"
        ],
        PremiumDefenseTokens =
        [
            "ANTICIPATE", "预判",
            "DEFLECT", "Deflect", "偏折",
            "PIERCING_WAIL", "SCREAM", "尖啸",
            "WRAITH_FORM", "WraithForm", "INTANGIBLE", "触不可及",
            "MELD_WITH_SHADOWS", "INTO_SHADOW", "BLEND_SHADOW", "融入暗影"
        ],
        WeakDefenseTokens =
        [
            "DODGE_AND_ROLL", "DodgeAndRoll", "闪躲翻滚",
            "CLOAK_AND_DAGGER", "CloakAndDagger", "斗篷与匕首",
            "EXPERTISE", "TOOLS_OF_THE_TRADE", "手上技法",
            "AFTER_IMAGE", "AfterImage", "残影",
            "HAWK_STORM", "EAGLE_STORM", "鹰暴",
            "MALAISE", "Malaise", "萎靡",
            "CORROSION", "ABRASION", "磨蚀"
        ]
    };

    public static readonly ActOneRewardProfile Defect = new()
    {
        PremiumAttackTokens =
        [
            "COMPILE_DRIVER", "CompileDriver", "编译冲击",
            "BALL_LIGHTNING", "BallLightning", "球状闪电",
            "FILTHY_ATTACK", "FOUL_STRIKE", "污秽攻击",
            "MOMENTUM_STRIKE", "趁势打击",
            "COLD_SNAP", "ColdSnap", "寒流",
            "LIGHTSPEED", "BEYOND_LIGHTSPEED", "HYPER_SPEED", "超越光速",
            "THUNDER", "雷霆",
            "CALL_OF_THE_VOID", "空无",
            "REBOUND", "Rebound", "折射",
            "SCRAPE", "Scrape", "打碎",
            "ADAPTIVE_STRIKE", "AdaptiveStrike", "适应打击",
            "RAINBOW", "Rainbow", "彩虹"
        ],
        PremiumDefenseTokens =
        [
            "GO_FOR_THE_EYES", "GoForTheEyes", "眼部攻击",
            "HIGH_SPEED_ESCAPE", "HYPER_ESCAPE", "高速脱离",
            "CHARGE_BATTERY", "ChargeBattery", "充电",
            "BOOT_SEQUENCE", "BootSequence", "启动流程",
            "COOLHEADED", "Coolheaded", "冰寒",
            "STACK", "Stack", "强撑",
            "COMPACT", "压缩",
            "SHADOW_SHIELD", "DARK_SHIELD", "暗影之盾",
            "GLACIER", "Glacier", "冰川",
            "GENETIC_ALGORITHM", "GeneticAlgorithm", "遗传算法"
        ],
        WeakAttackTokens =
        [
            "CLAW", "Claw", "爪击",
            "驱动",
            "SYNTHESIS", "ARTIFICIAL_SYNTHESIS", "人工合成",
            "CONSUMING_SHADOW", "ConsumingShadow", "吞噬暗影"
        ],
        WeakDefenseTokens = ["LEAP", "Leap", "飞跃"]
    };

    public static readonly ActOneRewardProfile Regent = new()
    {
        PremiumAttackTokens =
        [
            "KNOW_THY_PLACE", "何人僭越",
            "COLLISION_COURSE", "碰撞轨迹",
            "BEGONE", "BE_GONE", "下去",
            "PHOTON_CUT", "光子切割",
            "CELESTIAL_MIGHT", "HEAVENLY_POWER", "天堂之力",
            "MOONSHOT", "MOON_SHOT", "LUNAR_SHOT", "月面射击",
            "GAMMA_BLAST", "伽马爆破",
            "BURY", "葬送",
            "GUIDING_STAR", "RADIANT_STRIKE", "BRIGHT_STRIKE", "明耀打击",
            "STARDUST", "DYING_STAR", "STAR_EXTINCTION", "星灭",
            "THE_SMITH", "FORGED_FORM", "FORGE_FORM", "锻打成型",
            "CRASH_LANDING", "迫降"
        ],
        PremiumDefenseTokens =
        [
            "CLOAK_OF_STARS", "群星斗篷",
            "COSMIC_INDIFFERENCE", "宇宙冷漠",
            "PARTICLE_WALL", "粒子墙",
            "REFLECT", "倒映",
            "MAKE_IT_SO", "君权自授",
            "I_AM_INVINCIBLE", "所向无敌",
            "GUARDS", "护驾"
        ],
        WeakAttackTokens =
        [
            "WROUGHT_IN_WAR", "战火铸就",
            "SPOILS_OF_BATTLE", "战利品",
            "REFINE_BLADE", "淬炼刀刃",
            "CONSCRIPT", "征召上前",
            "SWORD_SAGE", "SWORDMASTER", "SWORD_MASTER", "剑圣",
            "MONOLOGUE", "独白",
            "KNOCKOUT_BLOW", "DECISIVE_STRIKE", "决胜一击",
            "BOMBARDMENT", "BOMBARD", "轰击"
        ],
        WeakDefenseTokens =
        [
            "GLITTERSTREAM", "GLITTER_STREAM", "流光溢彩",
            "MONARCHS_GAZE", "王之凝视",
            "RESONANCE", "共鸣",
            "PARRY", "招架"
        ]
    };

    public static readonly ActOneRewardProfile Necrobinder = new()
    {
        PremiumAttackTokens =
        [
            "POKE", "戳击",
            "SEVERANCE", "DEPRIVE", "DEBILITATE", "剥夺",
            "NEGATIVE_PULSE", "负能量脉冲",
            "FEAR", "恐惧",
            "FETCH", "RECLAIM", "RETRIEVE", "取回",
            "BONE_SHARDS", "碎骨",
            "VEILPIERCER", "VEIL_PIERCER", "刺破帷幕",
            "DEATHBRINGER", "DEATH_BRINGER", "死亡使者"
        ],
        PremiumDefenseTokens =
        [
            "DEFY", "违逆",
            "GRAVE_WARDEN", "守墓人",
            "DEATHS_DOOR", "DEATH_DOOR", "死亡之门",
            "ENFEEBLING_TOUCH", "弱化之触",
            "DELAY", "拖延",
            "UNDEATH", "不死"
        ],
        WeakAttackTokens =
        [
            "DRAIN_POWER", "能量汲取",
            "GRAB", "SNATCH", "抓取",
            "BLIGHT_STRIKE", "荒疫打击",
            "REAP", "收割",
            "RIGHT_HAND_HAND", "RIGHT_HAND", "得力助手",
            "BURY", "埋葬",
            "NECRO_MASTERY", "亡灵精通",
            "THE_SCYTHE", "GIANT_SCYTHE", "巨镰"
        ],
        WeakDefenseTokens =
        [
            "DANSE_MACABRE", "DEATH_DANCE", "死亡之舞",
            "PRODUCTION", "GROWTH", "增生",
            "AFTERLIFE", "来生",
            "PULL_AGGRO", "吸引仇恨",
            "SACRIFICE", "牺牲",
            "REANIMATE", "死者苏生"
        ]
    };
}
