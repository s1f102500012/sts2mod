using System.Collections;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Relics;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechUiSafetyHooks
{
	private static readonly FieldInfo? IntentCardInPlayAwaitingPlayerChoiceField = TryGetField(typeof(NMultiplayerPlayerIntentHandler), "_cardInPlayAwaitingPlayerChoice");
	private static readonly FieldInfo? IntentCardThinkyDotsField = TryGetField(typeof(NMultiplayerPlayerIntentHandler), "_cardThinkyDots");
	private static readonly FieldInfo? IntentHitboxField = TryGetField(typeof(NMultiplayerPlayerIntentHandler), "_hitbox");
	private static readonly FieldInfo? IntentCardIntentField = TryGetField(typeof(NMultiplayerPlayerIntentHandler), "_cardIntent");
	private static readonly FieldInfo? IntentIsInPlayerChoiceField = TryGetField(typeof(NMultiplayerPlayerIntentHandler), "_isInPlayerChoice");
	private static readonly FieldInfo? PlayQueueField = TryGetField(typeof(NCardPlayQueue), "_playQueue");
	private static readonly Type? QueueItemType = typeof(NCardPlayQueue).GetNestedType("QueueItem", BindingFlags.NonPublic);
	private static readonly FieldInfo? QueueItemCardField = QueueItemType?.GetField("card", BindingFlags.Instance | BindingFlags.Public);
	private static readonly FieldInfo? QueueItemActionField = QueueItemType?.GetField("action", BindingFlags.Instance | BindingFlags.Public);
	private static readonly FieldInfo? QueueItemTweenField = QueueItemType?.GetField("currentTween", BindingFlags.Instance | BindingFlags.Public);
	private static readonly MethodInfo? BeforeRemoteCardPlayResumedMethod = TryGetMethod(
		typeof(NCardPlayQueue),
		"BeforeRemoteCardPlayResumedAfterPlayerChoice",
		BindingFlags.Instance | BindingFlags.NonPublic,
		typeof(GameAction));
	private static readonly MethodInfo? TweenAllToQueuePositionMethod = TryGetMethod(
		typeof(NCardPlayQueue),
		"TweenAllToQueuePosition",
		BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	private static readonly FieldInfo? BeforeResumedAfterPlayerChoiceEventField = TryGetField(
		typeof(GameAction),
		"BeforeResumedAfterPlayerChoice",
		BindingFlags.Instance | BindingFlags.NonPublic);


	private static async Task PlayNewlyAcquiredAnimationSafely(Task original, NRelicInventoryHolder self)
	{
		try
		{
			await original;
		}
		catch (NullReferenceException) when (!IsNodeUsable(self))
		{
			LogRelicAnimationSkipped("holder-left-tree");
		}
		catch (ObjectDisposedException) when (!GodotObject.IsInstanceValid(self))
		{
			LogRelicAnimationSkipped("holder-disposed");
		}
	}

	private static bool IsNodeUsable(Node node)
	{
		return GodotObject.IsInstanceValid(node) && node.IsInsideTree();
	}


	private static bool TryFindQueueItem(NCardPlayQueue queue, GameAction action, out IList? playQueue, out int index, out object? item, out NCard? card)
	{
		playQueue = PlayQueueField?.GetValue(queue) as IList;
		index = -1;
		item = null;
		card = null;
		if (playQueue == null || QueueItemActionField == null || QueueItemCardField == null)
		{
			return false;
		}

		for (int i = 0; i < playQueue.Count; i++)
		{
			object? candidate = playQueue[i];
			if (candidate == null || !ReferenceEquals(QueueItemActionField.GetValue(candidate), action))
			{
				continue;
			}

			index = i;
			item = candidate;
			card = QueueItemCardField.GetValue(candidate) as NCard;
			return true;
		}

		return false;
	}

	private static bool HasUsableParent(Node node)
	{
		if (!GodotObject.IsInstanceValid(node))
		{
			return false;
		}

		Node? parent = node.GetParent();
		return parent != null && GodotObject.IsInstanceValid(parent);
	}

	private static void RestoreRemoteIntentUi(NMultiplayerPlayerIntentHandler intent)
	{
		IntentIsInPlayerChoiceField?.SetValue(intent, false);
		IntentCardInPlayAwaitingPlayerChoiceField?.SetValue(intent, null);

		Node? cardThinkyDots = IntentCardThinkyDotsField?.GetValue(intent) as Node;
		Node? hitbox = IntentHitboxField?.GetValue(intent) as Node;
		Node? cardIntent = IntentCardIntentField?.GetValue(intent) as Node;
		SafeMoveNode(cardThinkyDots, cardIntent);
		SafeMoveNode(hitbox, intent);
		SetUiNodeHidden(cardThinkyDots);
		SetUiNodeHidden(hitbox);
	}

	private static void SafeMoveNode(Node? node, Node? targetParent)
	{
		if (node == null || targetParent == null || !GodotObject.IsInstanceValid(node) || !GodotObject.IsInstanceValid(targetParent))
		{
			return;
		}

		Node? currentParent = node.GetParent();
		if (currentParent == targetParent)
		{
			return;
		}

		if (currentParent == null)
		{
			targetParent.AddChildSafely(node);
		}
		else
		{
			node.Reparent(targetParent);
		}
	}

	private static void SetUiNodeHidden(Node? node)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			return;
		}

		if (node is CanvasItem canvasItem)
		{
			canvasItem.Visible = false;
		}

		node.ProcessMode = Node.ProcessModeEnum.Disabled;
	}

	private static void KillQueueItemTween(object? item)
	{
		if (item != null && QueueItemTweenField?.GetValue(item) is Tween tween && GodotObject.IsInstanceValid(tween))
		{
			tween.Kill();
		}
	}

	private static void UnsubscribeCardPlayQueueResume(NCardPlayQueue queue, GameAction action)
	{
		if (BeforeRemoteCardPlayResumedMethod == null || BeforeResumedAfterPlayerChoiceEventField == null)
		{
			return;
		}

		if (BeforeResumedAfterPlayerChoiceEventField.GetValue(action) is not Action<GameAction> current)
		{
			return;
		}

		Delegate? updated = current;
		foreach (Delegate handler in current.GetInvocationList())
		{
			if (ReferenceEquals(handler.Target, queue) && handler.Method == BeforeRemoteCardPlayResumedMethod)
			{
				updated = Delegate.Remove(updated, handler);
			}
		}

		BeforeResumedAfterPlayerChoiceEventField.SetValue(action, updated);
	}

	private static void LogRelicAnimationSkipped(string reason)
	{
		if (HextechRunLogBudget.TryConsume("ui.relic-animation-skip", 5))
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic acquired animation skipped: {reason}");
		}
	}

	private static void LogRemoteIntentSkipped(GameAction action, string reason)
	{
		if (HextechRunLogBudget.TryConsume("ui.remote-intent-skip", 10))
		{
			Log.Warn($"[{ModInfo.Id}][UI] Remote intent card resume UI skipped: {reason}; action={action}");
		}
	}

	private static void LogRemotePlayQueueSkipped(GameAction action, string reason)
	{
		if (HextechRunLogBudget.TryConsume("ui.remote-play-queue-skip", 10))
		{
			Log.Warn($"[{ModInfo.Id}][UI] Remote play queue card resume UI skipped: {reason}; action={action}");
		}
	}


	[HarmonyPatch(typeof(NRelicInventoryHolder), nameof(NRelicInventoryHolder.PlayNewlyAcquiredAnimation), typeof(Vector2?), typeof(Vector2?))]
	[HextechPatch("ui.safety.newly-acquired-animation", "遗物获取动画安全")]
	private static class NewlyAcquiredAnimationPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(NRelicInventoryHolder __instance, ref Task __result, out bool __state)
		{
			__state = false;
			if (!IsNodeUsable(__instance))
			{
				LogRelicAnimationSkipped("holder-not-in-tree");
				__result = Task.CompletedTask;
				return false;
			}

			__state = true;
			return true;
		}

		[HarmonyPostfix]
		private static void Postfix(NRelicInventoryHolder __instance, bool __state, ref Task __result)
		{
			if (__state)
			{
				__result = PlayNewlyAcquiredAnimationSafely(__result, __instance);
			}
		}
	}

	[HarmonyPatch(typeof(NMultiplayerPlayerIntentHandler), "BeforeActionReadyToResumeAfterPlayerChoice")]
	[HextechPatch("ui.safety.multiplayer-intent", "联机意图恢复安全", Optional = true)]
	private static class MultiplayerIntentPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(NMultiplayerPlayerIntentHandler __instance, GameAction action)
		{
			NCard? card = IntentCardInPlayAwaitingPlayerChoiceField?.GetValue(__instance) as NCard;
			if (card == null || HasUsableParent(card))
			{
				return true;
			}

			RestoreRemoteIntentUi(__instance);
			LogRemoteIntentSkipped(action, "awaiting-card-detached");
			return false;
		}
	}

	[HarmonyPatch(typeof(NCardPlayQueue), "BeforeRemoteCardPlayResumedAfterPlayerChoice")]
	[HextechPatch("ui.safety.card-play-queue", "联机出牌队列恢复安全", Optional = true)]
	private static class CardPlayQueuePatch
	{
		[HarmonyPrefix]
		private static bool Prefix(NCardPlayQueue __instance, GameAction action)
		{
			if (!TryFindQueueItem(__instance, action, out IList? playQueue, out int index, out object? item, out NCard? card))
			{
				return true;
			}

			if (card != null && HasUsableParent(card))
			{
				return true;
			}

			UnsubscribeCardPlayQueueResume(__instance, action);
			KillQueueItemTween(item);
			if (playQueue != null && index >= 0 && index < playQueue.Count)
			{
				playQueue.RemoveAt(index);
				TweenAllToQueuePositionMethod?.Invoke(__instance, null);
			}

			LogRemotePlayQueueSkipped(action, card == null ? "missing-card-node" : "card-node-detached");
			return false;
		}
	}
}
