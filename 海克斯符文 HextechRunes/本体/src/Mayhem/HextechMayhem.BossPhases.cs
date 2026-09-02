using MegaCrit.Sts2.Core.Models.Monsters;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private static readonly FieldInfo? TestSubjectRespawnsField = TryGetField(typeof(TestSubject), "_respawns");

	public override async Task AfterOstyRevived(Creature osty)
	{
		if (osty.Side != CombatSide.Enemy
			|| !osty.IsAlive
			|| osty.CombatState?.RunState != RunState
			|| osty.Monster is not TestSubject testSubject
			|| osty.CombatId == null
			|| RunState.CurrentRoom is not CombatRoom room)
		{
			return;
		}

		int respawns = GetTestSubjectRespawns(testSubject);
		if (respawns <= 0)
		{
			return;
		}

		uint combatId = osty.CombatId.Value;
		int lastAppliedPhase = _combatTracking.TestSubjectPhaseStartApplied.GetValueOrDefault(combatId, 0);
		if (lastAppliedPhase >= respawns)
		{
			return;
		}

		_combatTracking.TestSubjectPhaseStartApplied[combatId] = respawns;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Reapplying boss start hexes after TestSubject revive: combatId={combatId} respawns={respawns}");
		await ApplyBossStartHexesToEnemy(osty, room);
		HextechEnemyUi.Refresh(this);
	}

	private async Task ApplyDeferredBossStartHexes(HextechCombatState combatState)
	{
		if (RunState.CurrentRoom is not CombatRoom room)
		{
			return;
		}

		foreach (Creature enemy in HextechCombatCreatureHelper.GetAliveEnemies(combatState))
		{
			await TryApplyDeferredBossStartHexes(enemy, room);
		}
	}

	private async Task<bool> TryApplyDeferredBossStartHexes(Creature creature, CombatRoom room)
	{
		if (creature.Side != CombatSide.Enemy
			|| !creature.IsAlive
			|| creature.CombatState?.RunState != RunState
			|| !IsDoormakerReadyForDeferredStart(creature)
			|| creature.CombatId == null)
		{
			return false;
		}

		uint combatId = creature.CombatId.Value;
		if (!_combatTracking.DoormakerRealStartApplied.Add(combatId))
		{
			return false;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Applying deferred boss start hexes to Doormaker: combatId={combatId} maxHp={creature.MaxHp}");
		await ApplyBossStartHexesToEnemy(creature, room);
		return true;
	}

	private async Task ApplyBossStartHexesToEnemy(Creature creature, CombatRoom room)
	{
		// 转阶段补发的是"开局类"海克斯增益,薄暮法衣不应镜像(等同战斗开始时的增益)。
		using (TwilightVeilRune.BeginMirrorSuppression())
		{
			await ApplyPersistentMonsterHexes(creature, replayOneShotPowers: true);
			await HextechEnemyHexDispatcher.ForEachActive(
				this,
				(effect, context) => effect.ApplyOpeningCombatStartToEnemy(
					context,
					creature,
					room,
					replayOneShotPowers: true));
			await ApplyMonsterCombatStartHexesToEnemy(creature, room);
		}
	}

	private static bool ShouldDeferInitialBossStartHexes(Creature creature)
	{
		return false;
	}

	private static bool IsDoormakerReadyForDeferredStart(Creature creature)
	{
		return false;
	}

	private static int GetTestSubjectRespawns(TestSubject testSubject)
	{
		return NormalizeTestSubjectRespawns(TestSubjectRespawnsField?.GetValue(testSubject));
	}

	internal static int NormalizeTestSubjectRespawns(object? fieldValue)
	{
		return fieldValue is int respawns ? Math.Max(0, respawns) : 0;
	}
}
