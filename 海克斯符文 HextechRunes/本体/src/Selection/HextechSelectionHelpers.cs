using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

internal static class HextechSelectionHelpers
{
	public static int IndexOfRelicInstance(IReadOnlyList<RelicModel> relics, RelicModel? selected)
	{
		if (selected == null)
		{
			return -1;
		}

		for (int i = 0; i < relics.Count; i++)
		{
			if (ReferenceEquals(relics[i], selected))
			{
				return i;
			}
		}

		return -1;
	}

	public static int IndexOfRelicById(IReadOnlyList<RelicModel> relics, RelicModel? selected)
	{
		if (selected == null)
		{
			return -1;
		}

		ModelId selectedId = selected.CanonicalInstance?.Id ?? selected.Id;
		for (int i = 0; i < relics.Count; i++)
		{
			ModelId optionId = relics[i].CanonicalInstance?.Id ?? relics[i].Id;
			if (optionId == selectedId)
			{
				return i;
			}
		}

		return -1;
	}

	public static RelicModel? CreateMonsterHexRelic(MonsterHexKind? monsterHex)
	{
		return monsterHex.HasValue
			? MonsterHexCatalog.GetIconRelicForMonsterHex(monsterHex.Value).ToMutable()
			: null;
	}

	public static MonsterHexKind? GetMonsterHexSlot(IReadOnlyList<MonsterHexKind?> monsterHexes, int slotIndex)
	{
		return slotIndex >= 0 && slotIndex < monsterHexes.Count
			? monsterHexes[slotIndex]
			: null;
	}

	public static void MarkRelicsSeen(IEnumerable<RelicModel> relics)
	{
		foreach (RelicModel relic in relics)
		{
			SaveManager.Instance.MarkRelicAsSeen(relic);
		}
	}

	public static async Task<T?> WaitForSingletonAsync<T>(
		Func<T?> getInstance,
		int attempts = 60,
		CancellationToken cancellationToken = default)
		where T : class
	{
		for (int i = 0; i < attempts; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			T? instance = getInstance();
			if (instance != null)
			{
				return instance;
			}

			await WaitForProcessFrameOrDelayAsync(cancellationToken);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return getInstance();
	}

	public static async Task WaitForProcessFrameOrDelayAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		NGame? game = NGame.Instance;
		if (game?.IsInsideTree() != true)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(16), cancellationToken);
			return;
		}

		SceneTree tree = game.GetTree();
		TaskCompletionSource<bool> processFrame = new(TaskCreationOptions.RunContinuationsAsynchronously);
		void OnProcessFrame()
		{
			processFrame.TrySetResult(true);
		}

		tree.ProcessFrame += OnProcessFrame;
		try
		{
			Task delay = Task.Delay(TimeSpan.FromMilliseconds(16), cancellationToken);
			Task completed = await Task.WhenAny(processFrame.Task, delay);
			await completed;
		}
		finally
		{
			if (GodotObject.IsInstanceValid(tree))
			{
				tree.ProcessFrame -= OnProcessFrame;
			}
		}
	}
}
