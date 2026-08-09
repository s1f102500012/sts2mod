using Godot;
using MegaCrit.Sts2.Core.Models;

namespace IntegratedStrategyEvents.Encounters;

public sealed class PrimalEntertainmentMioEncounter : IntegratedStrategyEliteEncounter
{
	public override IEnumerable<MonsterModel> AllPossibleMonsters => [Monster<Mio>()];

	protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
	{
		return [(MutableMonster<Mio>(), null)];
	}

	public override float GetCameraScaling()
	{
		return 0.95f;
	}

	public override Vector2 GetCameraOffset()
	{
		return Vector2.Down * 15f;
	}
}
