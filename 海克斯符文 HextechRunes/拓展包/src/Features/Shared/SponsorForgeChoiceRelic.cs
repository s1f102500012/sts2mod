using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunesSponsorPack;

// 锻造器选项遗物的共用外观基类:只承担稀有度与图标,不带行为。棱彩 / 黄金两档对应海克斯本体的两张锻造器底图。
public abstract class SponsorForgeChoiceRelic : RelicModel
{
	public override RelicRarity Rarity => RelicRarity.Event;
}

public abstract class PrismaticForgeChoiceRelic : SponsorForgeChoiceRelic
{
	private const string ChoiceIconPath = "res://HextechRunes/images/relics/prismaticForge.png";

	public override string PackedIconPath => ChoiceIconPath;

	protected override string PackedIconOutlinePath => ChoiceIconPath;

	protected override string BigIconPath => ChoiceIconPath;
}

public abstract class GoldForgeChoiceRelic : SponsorForgeChoiceRelic
{
	private const string ChoiceIconPath = "res://HextechRunes/images/relics/goldForge.png";

	public override string PackedIconPath => ChoiceIconPath;

	protected override string PackedIconOutlinePath => ChoiceIconPath;

	protected override string BigIconPath => ChoiceIconPath;
}
