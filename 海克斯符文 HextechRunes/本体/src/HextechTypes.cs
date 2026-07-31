namespace HextechRunes;

public enum HextechRarityTier
{
	Silver = 0,
	Gold = 1,
	Prismatic = 2
}

internal enum MonsterHexKind
{
	Slap = 0,
	EscapePlan = 1,
	HeavyHitter = 2,
	BigStrength = 3,
	Tormentor = 4,
	ProtectiveVeil = 5,
	Repulsor = 6,
	Thornmail = 7,
	Sturdy = 8,
	DawnbringersResolve = 9,
	ShrinkRay = 10,
	Firebrand = 11,
	SuperBrain = 12,
	AstralBody = 13,
	Nightstalking = 14,
	CourageOfColossus = 15,
	GlassCannon = 16,
	Goliath = 17,
	// 18 旧 Queen 已退役 → Queen = 106（身份独立化），勿复用
	HandOfBaron = 19,
	CantTouchThis = 20,
	MasterOfDuality = 21,
	Goldrend = 22,
	TankEngine = 23,
	GetExcited = 24,
	ShrinkEngine = 25,
	FeelTheBurn = 26,
	LightEmUp = 27,
	MountainSoul = 28,
	TwiceThrice = 29,
	Loop = 30,
	ServantMaster = 31,
	BackToBasics = 32,
	MadScientist = 34,
	FirstAidKit = 35,
	SpeedDemon = 36,
	DivineIntervention = 37,
	Sonata = 38,
	FeyMagic = 39,
	FinalForm = 40,
	UnmovableMountain = 41,
	MikaelsBlessing = 42,
	DevilsDance = 43,
	FrostWraith = 44,
	CuttingEdgeAlchemist = 45,
	BloodPact = 46,
	// 47 旧 ImmortalBone 已退役 → SkulkingColony = 104，勿复用
	Doomsday = 48,
	ClownCollege = 49,
	SingularityAI = 50,
	ProteinShake = 51,
	GoldenSpatula = 52,
	StartupRoutine = 53,
	WarmogsSpirit = 54,
	HailToTheKing = 55,
	EightPennyGate = 56,
	HastyScribble = 57,
	DizzySpinning = 58,
	BrutalForce = 59,
	BloodArmor = 60,
	JinlianBox = 61,
	MirrorReflection = 62,
	DuffsVintage = 63,
	// 64 旧 Misery 已退役 → LagavulinMatriarch = 107，勿复用
	ShoulderVaku = 65,
	Upgrade = 66,
	NearDeathFeast = 67,
	BlueCandleMedkit = 68,
	TanksShield = 69,
	Zealot = 70,
	// 71 旧 ScaredStiff 已退役 → PhantasmalGardener = 105，勿复用
	// 72 旧 GhostForm 已退役 → Exoskeleton = 108，勿复用
	SerpentsFang = 73,
	PandorasBox = 74,
	ForbiddenGrimoire = 75,
	AncientWine = 76,
	Porcupine = 77,
	MonarchsGaze = 78,
	SwiftAndSafe = 79,
	TezcatarasMercy = 80,
	ArcanePunch = 81,
	// 82 旧 SymphonyOfWar 已退役 → TestSubject = 109，勿复用
	Mystery = 83,
	MindOverMatter = 84,
	Omega = 85,
	ManipulateReality = 86,
	Compensation = 87,
	OminousPact = 88,
	SolidTime = 89,
	ForgottenSoul = 90,
	Cerberus = 91,
	NatureIsHealing = 92,
	Archmage = 93,
	BloodIdol = 94,
	OmniDragonSoul = 95,
	BlankCheck = 96,
	Corrosion = 97,
	Brutality = 98,
	Judicator = 99,
	SoulEater = 100,
	DeathHarvest = 101,
	GiantSlayer = 102,
	DualWield = 103,

	// 以下为"改名敌方海克斯"独立身份化后的新成员（2026-07-02）：
	// 旧成员借用我方 rune 的名字/图标，仅靠 .enemyTitle loc 改显示名；现改为独立身份。
	// 旧值(18/47/64/71/72/82)已退役为墓碑,本地持久化经 MonsterHexKindMigration remap 到新值。
	SkulkingColony = 104,      // 升级：鬼祟珊瑚群（旧 ImmortalBone=47）
	PhantasmalGardener = 105,  // 升级：花园幽灵鳗（旧 ScaredStiff=71）
	Queen = 106,               // 升级：女王（旧 Queen=18，名字沿用、值退役换新）
	LagavulinMatriarch = 107,  // 升级：乐加维林族母（旧 Misery=64）
	Exoskeleton = 108,         // 升级：外骨骼虫（旧 GhostForm=72）
	TestSubject = 109,         // 升级：实验体（旧 SymphonyOfWar=82）

	// 以下为独立敌方海克斯（多数无对应我方 rune,个别如珠光护手为我方符文的敌方版）。
	// 枚举值 append-only:net-id/存档兼容依赖数值,只在尾部追加,勿重排勿复用旧值。
	LeafSlime = 110,           // 升级：树叶史莱姆（白银）
	ShrinkerBeetle = 111,      // 升级：缩小甲虫（白银）
	Inklet = 112,              // 升级：墨宝（白银）
	PhrogParasite = 113,       // 升级：异蛙寄生虫（黄金）
	Vantom = 114,              // 升级：墨影幻灵（黄金）
	Aeonglass = 115,           // 升级：永世沙漏（棱彩）

	TheLost = 116,             // 升级：失落之物（白银）
	TheForgotten = 117,        // 升级：遗忘之物（白银）
	SlimedBerserker = 118,     // 升级：史莱姆狂战士（黄金）
	GlobeHead = 119,           // 升级：电球头（黄金）
	Myte = 120,                // 升级：异螨（黄金）
	Byrdonis = 121,            // 升级：多尼斯异鸟（棱彩）

	JeweledGauntlet = 122,     // 珠光护手（棱彩）
	FossilStalker = 123,       // 升级：化石追踪者（黄金）
	TungstenRod = 124,         // 升级：钨合金棍（黄金）

	AncientStatue = 125,       // 升级：旧日雕像（棱彩）
	HundredRefinements = 126,  // 百炼成钢（黄金）
	VitalitySurge = 127,       // 生机迸发（黄金）
	IInspect = 128,            // 我细看（棱彩）
	IGrip = 129                // 我紧握（棱彩）
}
