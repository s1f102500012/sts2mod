namespace HextechRunes;

// 敌方专属海克斯的图标/标题/描述载体 relic：只作为 MonsterHexIconRelicTypes 的模型，
// 不进玩家 rune 池（不在 HextechPlayerRuneRegistry），IsAvailableForPlayer=false 双保险。
// 图标按约定路径 res://HextechRunes/images/relics/{stem}.png；文件缺失时
// HextechRelicBase.GetResolvedIconPath 回退占位图，不会崩。
// loc：relics.json 的 {stem}.title/.description/.flavor/.enemyDescription。

/// <summary>升级：鬼祟珊瑚群（旧 ImmortalBone 敌方海克斯的独立身份）</summary>
public sealed class SkulkingColonyHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：花园幽灵鳗（旧 ScaredStiff 敌方海克斯的独立身份）</summary>
public sealed class PhantasmalGardenerHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：女王（旧 Queen 敌方海克斯的独立身份）</summary>
public sealed class QueenHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：乐加维林族母（旧 Misery 敌方海克斯的独立身份）</summary>
public sealed class LagavulinMatriarchHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：外骨骼虫（旧 GhostForm 敌方海克斯的独立身份）</summary>
public sealed class ExoskeletonHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：实验体（旧 SymphonyOfWar 敌方海克斯的独立身份）</summary>
public sealed class TestSubjectHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：树叶史莱姆</summary>
public sealed class LeafSlimeHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：缩小甲虫</summary>
public sealed class ShrinkerBeetleHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：墨宝</summary>
public sealed class InkletHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：异蛙寄生虫</summary>
public sealed class PhrogParasiteHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：墨影幻灵</summary>
public sealed class VantomHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：永世沙漏</summary>
public sealed class AeonglassHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：失落之物</summary>
public sealed class TheLostHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：遗忘之物</summary>
public sealed class TheForgottenHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：史莱姆狂战士</summary>
public sealed class SlimedBerserkerHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：电球头</summary>
public sealed class GlobeHeadHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：异螨</summary>
public sealed class MyteHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：化石追踪者</summary>
public sealed class FossilStalkerHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：钨合金棍</summary>
public sealed class TungstenRodHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>百炼成钢</summary>
public sealed class HundredRefinementsHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：旧日雕像</summary>
public sealed class AncientStatueHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>升级：多尼斯异鸟</summary>
public sealed class ByrdonisHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>我饥饿</summary>
public sealed class HungryHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>我细看</summary>
public sealed class InspectHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}

/// <summary>我紧握</summary>
public sealed class GripHex : HextechRelicBase
{
	public override bool IsAvailableForPlayer(Player player) => false;
}
