using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace IntegratedStrategyEvents.Encounters;

public sealed class CalendarKingsPincerBossEncounter :
	IntegratedStrategyTwoSidedBossEncounter<LugalszargusCalendarKing>
{
	public const string BossNodePathBase = $"res://{ModInfo.ModId}/images/map/two_rivals_boss_icon";

	public override string BossNodePath => BossNodePathBase;

	protected override bool UseProgrammaticCombatBackground => true;

	protected override BackgroundAssets? BuildProgrammaticCombatBackground(ActModel parentAct, Rng rng)
	{
		return new BackgroundAssets("the_insatiable_boss", rng);
	}

	public override IEnumerable<string> ExtraAssetPaths =>
	[
		BossNodePathBase + ".png",
		BossNodePathBase + "_outline.png",
		IntegratedStrategyBossMusic.CalendarKingsTrackPath
	];

	public override IEnumerable<MonsterModel> AllPossibleMonsters =>
	[
		Monster<LugalszargusCalendarKing>(),
		Monster<HaranduhEarthwhip>()
	];

	public override float GetCameraScaling()
	{
		return 0.82f;
	}

	public override Vector2 GetCameraOffset()
	{
		return Vector2.Down * 42f;
	}

	protected override MonsterModel CreateRightMonster()
	{
		return MutableMonster<HaranduhEarthwhip>();
	}
}
