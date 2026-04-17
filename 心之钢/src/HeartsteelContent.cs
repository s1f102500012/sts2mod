using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Heartsteel;

public sealed class HeartsteelRelic : RelicModel
{
	private const int CooldownTurns = 3;

	private const decimal BaseBonusDamage = 5m;

	private const decimal OwnerMaxHpDamageRatio = 0.10m;

	private const decimal BonusDamageMaxHpGainRatio = 0.10m;

	private static readonly string[] TriggerSfxPaths =
	[
		"res://Heartsteel/audio/Heartsteel_trigger_SFX_1.mp3",
		"res://Heartsteel/audio/Heartsteel_trigger_SFX_2.mp3",
		"res://Heartsteel/audio/Heartsteel_trigger_SFX_3.mp3"
	];

	private static readonly Dictionary<string, AudioStreamMP3> TriggerSfxCache = new();

	private static AudioStreamPlayer? _triggerSfxPlayer;

	private Dictionary<Creature, int>? _enemyCooldowns;

	private HashSet<Creature>? _markedEnemies;

	public override RelicRarity Rarity => RelicRarity.Rare;

	public override string PackedIconPath => ModInfo.RelicIconPath;

	protected override string PackedIconOutlinePath => PackedIconPath;

	protected override string BigIconPath => PackedIconPath;

	private Dictionary<Creature, int> EnemyCooldowns => _enemyCooldowns ??= new Dictionary<Creature, int>();

	private HashSet<Creature> MarkedEnemies => _markedEnemies ??= new HashSet<Creature>();

	protected override void DeepCloneFields()
	{
		base.DeepCloneFields();
		_enemyCooldowns = new Dictionary<Creature, int>();
		_markedEnemies = new HashSet<Creature>();
	}

	public override Task BeforeCombatStart()
	{
		ResetCombatTracking();
		return Task.CompletedTask;
	}

	public override Task AfterCreatureAddedToCombat(Creature creature)
	{
		if (creature.Side == CombatSide.Enemy && creature.IsAlive)
		{
			EnemyCooldowns.TryAdd(creature, 0);
		}

		return Task.CompletedTask;
	}

	public override async Task AfterSideTurnStart(CombatSide side, CombatState combatState)
	{
		if (side != CombatSide.Player)
		{
			return;
		}

		IReadOnlyList<Creature> aliveEnemies = combatState.Enemies.Where(static enemy => enemy.IsAlive).ToList();
		PruneTrackedEnemies(aliveEnemies);
		foreach (Creature enemy in aliveEnemies)
		{
			EnemyCooldowns.TryAdd(enemy, 0);
			if (MarkedEnemies.Contains(enemy))
			{
				continue;
			}

			int updatedTurns = EnemyCooldowns[enemy] + 1;
			if (updatedTurns >= CooldownTurns)
			{
				EnemyCooldowns[enemy] = CooldownTurns;
				await ApplyDevourMark(enemy);
			}
			else
			{
				EnemyCooldowns[enemy] = updatedTurns;
			}
		}

		RefreshStatus();
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (!CanTriggerHeartsteel(dealer, target, cardSource))
		{
			return;
		}

		int bonusDamage = GetCurrentBonusDamage();
		int maxHpGain = RoundToInt(bonusDamage * BonusDamageMaxHpGainRatio);

		EnemyCooldowns[target] = 0;
		MarkedEnemies.Remove(target);
		await PowerCmd.Remove<HeartsteelDevourPower>(target);
		Flash(new[] { target });
		PlayRandomTriggerSfx();
		RefreshStatus();

		if (target.IsAlive && bonusDamage > 0)
		{
			await CreatureCmd.Damage(
				choiceContext,
				target,
				bonusDamage,
				ValueProp.Unpowered | ValueProp.SkipHurtAnim,
				Owner.Creature,
				cardSource: null);
		}

		if (maxHpGain > 0)
		{
			await CreatureCmd.GainMaxHp(Owner.Creature, maxHpGain);
			await RefreshMarkedEnemyPowerAmounts();
		}
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		ResetCombatTracking();
		return Task.CompletedTask;
	}

	private async Task ApplyDevourMark(Creature enemy)
	{
		HeartsteelDevourPower? power = await PowerCmd.Apply<HeartsteelDevourPower>(enemy, GetCurrentBonusDamage(), Owner.Creature, null);
		if (power == null)
		{
			return;
		}

		MarkedEnemies.Add(enemy);
		Flash(new[] { enemy });
		RefreshStatus();
	}

	private bool CanTriggerHeartsteel(Creature? dealer, Creature target, CardModel? cardSource)
	{
		if (dealer != Owner.Creature)
		{
			return false;
		}

		if (target.Side != CombatSide.Enemy || !MarkedEnemies.Contains(target))
		{
			return false;
		}

		if (cardSource == null || cardSource.Owner != Owner || cardSource.Type != CardType.Attack)
		{
			return false;
		}

		return true;
	}

	private int GetCurrentBonusDamage()
	{
		return RoundToInt(BaseBonusDamage + Owner.Creature.MaxHp * OwnerMaxHpDamageRatio);
	}

	private async Task RefreshMarkedEnemyPowerAmounts()
	{
		int bonusDamage = GetCurrentBonusDamage();
		foreach (Creature enemy in MarkedEnemies.Where(static enemy => enemy.IsAlive).ToList())
		{
			await PowerCmd.SetAmount<HeartsteelDevourPower>(enemy, bonusDamage, Owner.Creature, null);
		}
	}

	private void ResetCombatTracking()
	{
		EnemyCooldowns.Clear();
		MarkedEnemies.Clear();
		Status = RelicStatus.Normal;
	}

	private void PruneTrackedEnemies(IEnumerable<Creature> aliveEnemies)
	{
		HashSet<Creature> aliveSet = aliveEnemies.ToHashSet();
		foreach (Creature enemy in EnemyCooldowns.Keys.Where(enemy => !aliveSet.Contains(enemy)).ToList())
		{
			EnemyCooldowns.Remove(enemy);
		}

		MarkedEnemies.RemoveWhere(enemy => !aliveSet.Contains(enemy));
	}

	private void RefreshStatus()
	{
		Status = MarkedEnemies.Count > 0 ? RelicStatus.Active : RelicStatus.Normal;
	}

	private static int RoundToInt(decimal value)
	{
		return (int)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
	}

	private static void PlayRandomTriggerSfx()
	{
		if (NGame.Instance == null)
		{
			return;
		}

		EnsureTriggerSfxPlayer();
		if (!GodotObject.IsInstanceValid(_triggerSfxPlayer))
		{
			return;
		}

		string path = TriggerSfxPaths[Random.Shared.Next(TriggerSfxPaths.Length)];
		if (!TriggerSfxCache.TryGetValue(path, out AudioStreamMP3? stream))
		{
			stream = AudioStreamMP3.LoadFromFile(path);
			TriggerSfxCache[path] = stream;
		}

		_triggerSfxPlayer!.Stop();
		_triggerSfxPlayer.Stream = stream;
		_triggerSfxPlayer.Play();
	}

	private static void EnsureTriggerSfxPlayer()
	{
		if (GodotObject.IsInstanceValid(_triggerSfxPlayer))
		{
			return;
		}

		_triggerSfxPlayer = new AudioStreamPlayer
		{
			Name = "HeartsteelTriggerSfx",
			Bus = "Master"
		};
		NGame.Instance?.AddChild(_triggerSfxPlayer);
	}
}

public sealed class HeartsteelDevourPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		StartPulsing();
		return Task.CompletedTask;
	}

	public override Task AfterRemoved(Creature oldOwner)
	{
		StopPulsing();
		return Task.CompletedTask;
	}
}
