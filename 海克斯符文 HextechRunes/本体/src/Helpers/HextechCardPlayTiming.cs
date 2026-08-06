using Godot;

namespace HextechRunes;

internal static class HextechCardPlayTiming
{
	internal static async Task<bool> WaitForCardPlayFinishedAsync(
		Creature source,
		HextechCombatState combatState,
		CardModel triggeringCard)
	{
		bool playFinished = false;
		Action playedHandler = () => playFinished = true;
		triggeringCard.Played += playedHandler;
		try
		{
			while (!playFinished)
			{
				if (source.IsDead || !ReferenceEquals(source.CombatState, combatState))
				{
					return false;
				}

				if (Engine.GetMainLoop() is not SceneTree tree)
				{
					return triggeringCard.Pile?.Type != PileType.Play;
				}

				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}

			if (Engine.GetMainLoop() is SceneTree settledTree)
			{
				await settledTree.ToSignal(settledTree, SceneTree.SignalName.ProcessFrame);
			}

			return !source.IsDead && ReferenceEquals(source.CombatState, combatState);
		}
		finally
		{
			triggeringCard.Played -= playedHandler;
		}
	}
}
