namespace HextechRunes;

internal static class HextechCustomModelRegistry
{
	internal static IReadOnlyList<Type> EventRelicTypes { get; } = [];

	// 自定义稀有度 run modifier(白银/黄金/棱彩)。内容清单放注册表层,
	// 供 Bootstrap 的 SavedProperty 注入与 Hooks 的 UI 追加共同消费(避免 Bootstrap 反向依赖 Hooks)。
	internal static IReadOnlyList<Type> CustomRarityModifierTypes { get; } =
	[
		typeof(HextechSilverRunModifier),
		typeof(HextechGoldRunModifier),
		typeof(HextechPrismaticRunModifier)
	];

	internal static IReadOnlyList<Type> ShopOnlyRelicTypes { get; } =
	[
		typeof(RandomForgeShopRelic)
	];

	// 敌方专属海克斯的图标/文案载体 relic：注册进 ModelDb 但不进任何玩家获取池。
	// 注意：新增敌方专属 relic 时这里必须同步登记，否则图标路径不被认定（游戏内显示 NOPE）。
	internal static IReadOnlyList<Type> EnemyHexIconRelicTypes { get; } =
	[
		typeof(SkulkingColonyHex),
		typeof(PhantasmalGardenerHex),
		typeof(QueenHex),
		typeof(LagavulinMatriarchHex),
		typeof(ExoskeletonHex),
		typeof(TestSubjectHex),
		typeof(LeafSlimeHex),
		typeof(ShrinkerBeetleHex),
		typeof(InkletHex),
		typeof(PhrogParasiteHex),
		typeof(VantomHex),
		typeof(AeonglassHex),
		typeof(TheLostHex),
		typeof(TheForgottenHex),
		typeof(SlimedBerserkerHex),
		typeof(GlobeHeadHex),
		typeof(MyteHex),
		typeof(FossilStalkerHex),
		typeof(TungstenRodHex),
		typeof(HundredRefinementsHex),
		typeof(AncientStatueHex),
		typeof(ByrdonisHex),
		typeof(HungryHex),
		typeof(InspectHex),
		typeof(GripHex)
	];

	internal static IReadOnlyList<Type> CustomCardTypes { get; } =
	[
		typeof(ElicitCard),
		typeof(TrickMagicCard),
		typeof(BladeWaltzCard),
		typeof(CatalystCard),
		typeof(AllInCard),
		typeof(WhiteHoleCard),
		typeof(SearingAttackCard),
		typeof(FeelTheBurnCard),
		typeof(OkBoomerangCard),
		typeof(ReprogramCard),
		typeof(MikaelsBlessingCard),
		typeof(OstyWishCard),
		typeof(OceanDragonSoulCard),
		typeof(InfernalDragonSoulCard),
		typeof(HextechDragonSoulCard),
		typeof(MountainDragonSoulCard),
		typeof(ChemtechDragonSoulCard),
		typeof(CloudDragonSoulCard)
	];
}
