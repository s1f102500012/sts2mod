namespace HextechRunes;

public sealed class OrbSymbiosisRune : HextechRelicBase
{
	private bool _duplicatingOrb;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("OrbCount", 1m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsDefectPlayer(player);
	}

	public override async Task AfterOrbChanneled(PlayerChoiceContext choiceContext, Player player, OrbModel orb)
	{
		if (_duplicatingOrb || player != Owner || Owner == null || Owner.Creature.IsDead)
		{
			return;
		}

		ModelId orbId = orb.Id;
		OrbModel? canonicalOrb = orbId == ModelId.none
			? null
			: ModelDb.GetByIdOrNull<OrbModel>(orbId);
		if (canonicalOrb == null)
		{
			Log.Warn(
				$"[{ModInfo.Id}][OrbSymbiosis] Skipped orb duplication because its model is not registered: "
				+ $"orb={orbId.Entry} type={orb.GetType().FullName}.");
			return;
		}

		Flash();
		_duplicatingOrb = true;
		try
		{
			for (int i = 0; i < DynamicVars["OrbCount"].IntValue; i++)
			{
				OrbModel duplicate = canonicalOrb.ToMutable();
				await OrbCmd.Channel(choiceContext, duplicate, Owner);
			}
		}
		finally
		{
			_duplicatingOrb = false;
		}
	}
}
