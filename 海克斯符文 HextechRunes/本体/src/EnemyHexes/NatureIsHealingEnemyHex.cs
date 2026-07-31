using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace HextechRunes;

internal sealed class NatureIsHealingEnemyHex : HextechEnemyHexEffect
{
	private const string TimerNodeName = "HextechEnemyNatureIsHealingTimer";

	private Godot.Timer? _timer;
	private Action? _timerTimeoutHandler;
	private HextechMayhemModifier? _modifier;
	private bool _healing;
	private int _timerGeneration;

	internal override MonsterHexKind Kind => MonsterHexKind.NatureIsHealing;

	internal override void ResetRunScopedState()
	{
		StopTimer();
	}

	internal override Task ApplyCombatStartToEnemy(HextechEnemyHexContext context, Creature enemy, CombatRoom room)
	{
		_modifier = context.Modifier;
		if (!HextechPlayerContextHelper.IsNetworkMultiplayerRun())
		{
			StartTimer(context);
		}

		return Task.CompletedTask;
	}

	internal override async Task BeforeEnemySideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players, IReadOnlyList<Creature> enemies)
	{
		if (!HextechPlayerContextHelper.IsNetworkMultiplayerRun() || enemies.Count == 0 || combatState.RunState != context.RunState)
		{
			return;
		}

		foreach (Creature enemy in enemies)
		{
			await CreatureCmd.Heal(enemy, 1m);
		}
	}

	internal override Task AfterCombatEnd(HextechEnemyHexContext context, CombatRoom room)
	{
		StopTimer();
		return Task.CompletedTask;
	}

	private void StartTimer(HextechEnemyHexContext context)
	{
		if (_timer != null)
		{
			return;
		}

		Node? root = NGame.Instance?.GetTree()?.Root;
		if (root == null)
		{
			Log.Warn($"[{ModInfo.Id}][EnemyNatureIsHealing] Timer skipped: scene tree root unavailable.", 2);
			return;
		}

		Godot.Timer timer = new()
		{
			Name = TimerNodeName,
			WaitTime = (double)context.TierValue(Kind, 15.0m, 10.0m, 5.0m),
			OneShot = false,
			Autostart = true
		};
		int generation = unchecked(++_timerGeneration);
		Action timeoutHandler = () => OnTimerTimeout(generation);
		timer.Timeout += timeoutHandler;
		root.AddChild(timer);
		_timer = timer;
		_timerTimeoutHandler = timeoutHandler;
	}

	private void StopTimer()
	{
		Godot.Timer? timer = _timer;
		Action? timeoutHandler = _timerTimeoutHandler;
		unchecked
		{
			_timerGeneration++;
		}
		_timer = null;
		_timerTimeoutHandler = null;
		_modifier = null;
		_healing = false;
		if (timer == null)
		{
			return;
		}

		if (GodotObject.IsInstanceValid(timer))
		{
			if (timeoutHandler != null)
			{
				timer.Timeout -= timeoutHandler;
			}
			timer.QueueFree();
		}
	}

	private async void OnTimerTimeout(int generation)
	{
		if (generation != _timerGeneration || _healing)
		{
			return;
		}

		_healing = true;
		try
		{
			if (!TryGetAliveEnemies(out IReadOnlyList<Creature> enemies))
			{
				StopTimer();
				return;
			}

			foreach (Creature enemy in enemies)
			{
				await CreatureCmd.Heal(enemy, 1m);
				if (generation != _timerGeneration)
				{
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][EnemyNatureIsHealing] Timer heal failed: {ex.Message}", 2);
		}
		finally
		{
			if (generation == _timerGeneration)
			{
				_healing = false;
			}
		}
	}

	private bool TryGetAliveEnemies(out IReadOnlyList<Creature> enemies)
	{
		enemies = [];
		if (_modifier == null
			|| CombatManager.Instance?.IsInProgress != true
			|| _modifier.ActiveRunState.CurrentRoom is not CombatRoom room
			|| room.CombatState.RunState != _modifier.ActiveRunState)
		{
			return false;
		}

		enemies = HextechCombatCreatureHelper.GetAliveEnemies(room.CombatState);
		return enemies.Count > 0;
	}
}
