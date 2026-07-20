using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace RemoveHandLimit;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const string HarmonyId = "Natsuki.RemoveHandLimit";

	private const int HandLimit = 20;

	private const int RowLimit = 10;

	private const float UpperRowYOffset = -72f;

	private const float LowerRowYOffset = 28f;

	private const float UpperRowAngleFactor = 0.82f;

	private const float UpperRowHitboxHeightFactor = 0.56f;

	private static Harmony? _harmony;

	private static readonly Dictionary<ulong, Rect2> OriginalHitboxRects = new();

	private static readonly FieldInfo SelectCardShortcutsField = RequireField(typeof(NPlayerHand), "_selectCardShortcuts", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo DraggedHolderIndexField = RequireField(typeof(NPlayerHand), "_draggedHolderIndex", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo HoldersAwaitingQueueField = RequireField(typeof(NPlayerHand), "_holdersAwaitingQueue", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo CurrentCardPlayField = RequireField(typeof(NPlayerHand), "_currentCardPlay", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo LastFocusedHolderIndexField = RequireField(typeof(NPlayerHand), "_lastFocusedHolderIdx", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo MaxCardsInHandGetterMethod = RequireProperty(typeof(CardPile), nameof(CardPile.MaxCardsInHand), BindingFlags.Public | BindingFlags.Static).GetMethod
		?? throw new InvalidOperationException("[RemoveHandLimit] Could not find CardPile.MaxCardsInHand getter.");

	private static readonly FieldInfo HandPosCardPositionDataField = RequireField(typeof(HandPosHelper), "_cardPositionData", BindingFlags.Static | BindingFlags.NonPublic);

	private static readonly FieldInfo HandPosCardAngleDataField = RequireField(typeof(HandPosHelper), "_cardAngleData", BindingFlags.Static | BindingFlags.NonPublic);

	private static readonly FieldInfo HandPosBaseScaleField = RequireField(typeof(HandPosHelper), "_baseScale", BindingFlags.Static | BindingFlags.NonPublic);

	private static readonly Vector2[][] VanillaCardPositionData = GetStaticFieldValue<Vector2[][]>(HandPosCardPositionDataField);

	private static readonly float[][] VanillaCardAngleData = GetStaticFieldValue<float[][]>(HandPosCardAngleDataField);

	private static readonly Vector2 VanillaBaseScale = GetStaticFieldValue<Vector2>(HandPosBaseScaleField);

	private static readonly MethodInfo RefreshLayoutMethod = RequireMethod(typeof(NPlayerHand), "RefreshLayout", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo ReturnHolderToHandMethod = RequireMethod(typeof(NPlayerHand), "ReturnHolderToHand", BindingFlags.Instance | BindingFlags.NonPublic, typeof(NHandCardHolder));

	private static readonly PropertyInfo FocusedHolderProperty = RequireProperty(typeof(NPlayerHand), nameof(NPlayerHand.FocusedHolder), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	public static void Initialize()
	{
		InstallHooks();
		Log.Info($"[RemoveHandLimit] Loaded. Hand limit patched to {HandLimit} with two 10-card rows.");
	}

	private static void InstallHooks()
	{
		Harmony harmony = _harmony ??= new Harmony(HarmonyId);
		// 0.108.0 给该重载追加了 bool isChangingOwners 参数；先按 0.108 签名找，退回 0.107 签名，兼容双版本。
		MethodInfo addCardsMethod =
			typeof(CardPileCmd).GetMethod(
				nameof(CardPileCmd.Add),
				BindingFlags.Public | BindingFlags.Static,
				null,
				new[]
				{
					typeof(IEnumerable<CardModel>),
					typeof(CardPile),
					typeof(CardPilePosition),
					typeof(AbstractModel),
					typeof(bool),
					typeof(bool),
				},
				null)
			?? RequireMethod(
				typeof(CardPileCmd),
				nameof(CardPileCmd.Add),
				BindingFlags.Public | BindingFlags.Static,
				typeof(IEnumerable<CardModel>),
				typeof(CardPile),
				typeof(CardPilePosition),
				typeof(AbstractModel),
				typeof(bool));
		MethodInfo drawMethod = RequireMethod(
			typeof(CardPileCmd),
			nameof(CardPileCmd.Draw),
			BindingFlags.Public | BindingFlags.Static,
			typeof(PlayerChoiceContext),
			typeof(decimal),
			typeof(Player),
			typeof(bool));
		MethodInfo checkCanDrawMethod = RequireMethod(
			typeof(CardPileCmd),
			"CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot",
			BindingFlags.NonPublic | BindingFlags.Static,
			typeof(Player));
		MethodInfo handPosGetPosition = RequireMethod(
			typeof(HandPosHelper),
			nameof(HandPosHelper.GetPosition),
			BindingFlags.Public | BindingFlags.Static,
			typeof(int),
			typeof(int));
		MethodInfo handPosGetAngle = RequireMethod(
			typeof(HandPosHelper),
			nameof(HandPosHelper.GetAngle),
			BindingFlags.Public | BindingFlags.Static,
			typeof(int),
			typeof(int));
		MethodInfo handPosGetScale = RequireMethod(
			typeof(HandPosHelper),
			nameof(HandPosHelper.GetScale),
			BindingFlags.Public | BindingFlags.Static,
			typeof(int));
		MethodInfo refreshLayoutMethod = RequireMethod(
			typeof(NPlayerHand),
			"RefreshLayout",
			BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo onHolderFocusedMethod = RequireMethod(
			typeof(NPlayerHand),
			"OnHolderFocused",
			BindingFlags.Instance | BindingFlags.NonPublic,
			typeof(NHandCardHolder));
		MethodInfo onHolderUnfocusedMethod = RequireMethod(
			typeof(NPlayerHand),
			"OnHolderUnfocused",
			BindingFlags.Instance | BindingFlags.NonPublic,
			typeof(NHandCardHolder));
		MethodInfo handCardHoverMethod = RequireMethod(
			typeof(NHandCardHolder),
			"DoCardHoverEffects",
			BindingFlags.Instance | BindingFlags.NonPublic,
			typeof(bool));
		MethodInfo startCardPlayMethod = RequireMethod(
			typeof(NPlayerHand),
			"StartCardPlay",
			BindingFlags.Instance | BindingFlags.NonPublic,
			typeof(NHandCardHolder),
			typeof(bool));

		harmony.Patch(MaxCardsInHandGetterMethod, prefix: new HarmonyMethod(typeof(ModEntry), nameof(MaxCardsInHandPrefix)));
		harmony.Patch(GetAsyncStateMachineTarget(addCardsMethod), transpiler: new HarmonyMethod(typeof(ModEntry), nameof(PatchAddCardsTranspiler)));
		harmony.Patch(GetAsyncStateMachineTarget(drawMethod), transpiler: new HarmonyMethod(typeof(ModEntry), nameof(PatchDrawTranspiler)));
		harmony.Patch(checkCanDrawMethod, transpiler: new HarmonyMethod(typeof(ModEntry), nameof(PatchCheckCanDrawTranspiler)));
		// RitsuLib 也会给这几个方法打"超过10张单排压缩"前缀，默认优先级下会盖掉本模组的双排布局。
		// Priority.High 让本模组的前缀先执行；bool 前缀返回 false 后，RitsuLib 的同类前缀会被跳过。
		harmony.Patch(handPosGetPosition, prefix: new HarmonyMethod(typeof(ModEntry), nameof(GetPositionPrefix)) { priority = Priority.High });
		harmony.Patch(handPosGetAngle, prefix: new HarmonyMethod(typeof(ModEntry), nameof(GetAnglePrefix)) { priority = Priority.High });
		harmony.Patch(handPosGetScale, prefix: new HarmonyMethod(typeof(ModEntry), nameof(GetScalePrefix)) { priority = Priority.High });
		harmony.Patch(
			refreshLayoutMethod,
			prefix: new HarmonyMethod(typeof(ModEntry), nameof(RefreshLayoutPrefix)),
			postfix: new HarmonyMethod(typeof(ModEntry), nameof(RefreshLayoutPostfix)));
		harmony.Patch(onHolderFocusedMethod, prefix: new HarmonyMethod(typeof(ModEntry), nameof(OnHolderFocusedPrefix)));
		harmony.Patch(onHolderUnfocusedMethod, prefix: new HarmonyMethod(typeof(ModEntry), nameof(OnHolderUnfocusedPrefix)));
		harmony.Patch(handCardHoverMethod, postfix: new HarmonyMethod(typeof(ModEntry), nameof(DoCardHoverEffectsPostfix)));
		// StartCardPlay 同样与 RitsuLib 撞车，一并提高优先级
		harmony.Patch(startCardPlayMethod, prefix: new HarmonyMethod(typeof(ModEntry), nameof(StartCardPlayPrefix)) { priority = Priority.High });
	}

	private static bool MaxCardsInHandPrefix(ref int __result)
	{
		__result = HandLimit;
		return false;
	}

	private static IEnumerable<CodeInstruction> PatchAddCardsTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		return ReplaceMaxCardsInHandCalls(instructions, expectedCount: 1, __originalMethod);
	}

	private static IEnumerable<CodeInstruction> PatchDrawTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		return ReplaceMaxCardsInHandCalls(instructions, expectedCount: 3, __originalMethod);
	}

	private static IEnumerable<CodeInstruction> PatchCheckCanDrawTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		return ReplaceMaxCardsInHandCalls(instructions, expectedCount: 1, __originalMethod);
	}

	private static IEnumerable<CodeInstruction> ReplaceMaxCardsInHandCalls(IEnumerable<CodeInstruction> instructions, int expectedCount, MethodBase originalMethod)
	{
		List<CodeInstruction> patchedInstructions = instructions.ToList();
		int patched = 0;
		foreach (CodeInstruction instruction in patchedInstructions)
		{
			if (!instruction.Calls(MaxCardsInHandGetterMethod))
			{
				continue;
			}

			instruction.opcode = OpCodes.Ldc_I4;
			instruction.operand = HandLimit;
			patched++;
		}

		if (patched != expectedCount)
		{
			throw new InvalidOperationException($"[RemoveHandLimit] Expected to patch {expectedCount} MaxCardsInHand calls in {originalMethod.FullDescription()}, but patched {patched}.");
		}

		return patchedInstructions;
	}

	private static bool GetPositionPrefix(int handSize, int cardIndex, ref Vector2 __result)
	{
		if (handSize <= RowLimit)
		{
			return true;
		}

		ValidateHandIndex(handSize, cardIndex);
		if (cardIndex < RowLimit)
		{
			Vector2 basePosition = GetVanillaPosition(RowLimit, cardIndex);
			__result = new Vector2(basePosition.X, basePosition.Y + LowerRowYOffset);
			return false;
		}

		int upperRowCount = handSize - RowLimit;
		int upperRowIndex = cardIndex - RowLimit;
		Vector2 upperBasePosition = GetVanillaPosition(upperRowCount, upperRowIndex);
		__result = new Vector2(upperBasePosition.X, upperBasePosition.Y + UpperRowYOffset);
		return false;
	}

	private static bool GetAnglePrefix(int handSize, int cardIndex, ref float __result)
	{
		if (handSize <= RowLimit)
		{
			return true;
		}

		ValidateHandIndex(handSize, cardIndex);
		if (cardIndex < RowLimit)
		{
			__result = GetVanillaAngle(RowLimit, cardIndex);
			return false;
		}

		int upperRowCount = handSize - RowLimit;
		int upperRowIndex = cardIndex - RowLimit;
		__result = GetVanillaAngle(upperRowCount, upperRowIndex) * UpperRowAngleFactor;
		return false;
	}

	private static bool GetScalePrefix(int handSize, ref Vector2 __result)
	{
		if (handSize <= RowLimit)
		{
			return true;
		}

		__result = GetVanillaScale(RowLimit);
		return false;
	}

	private static Vector2 GetVanillaPosition(int handSize, int cardIndex)
	{
		return VanillaCardPositionData[handSize - 1][cardIndex];
	}

	private static float GetVanillaAngle(int handSize, int cardIndex)
	{
		return VanillaCardAngleData[handSize - 1][cardIndex];
	}

	private static Vector2 GetVanillaScale(int handSize)
	{
		float multiplier = handSize switch
		{
			8 => 0.95f,
			9 => 0.9f,
			10 => 0.85f,
			11 => 0.8f,
			12 => 0.75f,
			_ => 1f
		};
		return VanillaBaseScale * multiplier;
	}

	private static bool RefreshLayoutPrefix(NPlayerHand __instance)
	{
		if (__instance.ActiveHolders.Count > RowLimit)
		{
			SetFocusedHolder(__instance, null);
		}

		return true;
	}

	private static void RefreshLayoutPostfix(NPlayerHand __instance)
	{
		UpdateHolderLayering(__instance);
		UpdateHolderHitboxes(__instance);
	}

	private static bool OnHolderFocusedPrefix(NPlayerHand __instance, NHandCardHolder holder)
	{
		if (__instance.ActiveHolders.Count <= RowLimit)
		{
			return true;
		}

		SetLastFocusedHolderIndex(__instance, holder.GetIndex());
		if (holder.CardModel != null)
		{
			RunManager.Instance.HoveredModelTracker.OnLocalCardHovered(holder.CardModel);
		}
		UpdateHolderHitboxes(__instance);
		return false;
	}

	private static bool OnHolderUnfocusedPrefix(NPlayerHand __instance, NHandCardHolder holder)
	{
		if (__instance.ActiveHolders.Count <= RowLimit)
		{
			return true;
		}

		RunManager.Instance.HoveredModelTracker.OnLocalCardUnhovered();
		UpdateHolderHitboxes(__instance);
		return false;
	}

	private static void DoCardHoverEffectsPostfix(NHandCardHolder __instance)
	{
		if (__instance.GetParent() is Control parent && parent.GetParent() is NPlayerHand hand)
		{
			UpdateHolderLayering(hand);
		}
	}

	private static bool StartCardPlayPrefix(NPlayerHand __instance, NHandCardHolder holder, bool startedViaShortcut)
	{
		StringName[] shortcuts = GetSelectCardShortcuts(__instance);
		int holderIndex = holder.GetIndex();
		if ((NControllerManager.Instance?.IsUsingController ?? false) || holderIndex < shortcuts.Length)
		{
			return true;
		}

		SetDraggedHolderIndex(__instance, holderIndex);
		GetHoldersAwaitingQueue(__instance).Add(holder);
		holder.Reparent(__instance);
		holder.BeginDrag();

		NCardPlay currentCardPlay = NMouseCardPlay.Create(holder, MegaInput.releaseCard, startedViaShortcut);
		SetCurrentCardPlay(__instance, currentCardPlay);
		__instance.AddChildSafely(currentCardPlay);
		currentCardPlay.Connect(NCardPlay.SignalName.Finished, Callable.From(delegate(bool success)
		{
			RunManager.Instance.HoveredModelTracker.OnLocalCardDeselected();
			if (!success)
			{
				InvokeReturnHolderToHand(__instance, holder);
			}

			SetDraggedHolderIndex(__instance, -1);
			InvokeRefreshLayout(__instance);
		}));

		CardModel selectedCard = holder.CardNode?.Model ?? throw new InvalidOperationException("[RemoveHandLimit] Tried to start card play without a card node.");
		RunManager.Instance.HoveredModelTracker.OnLocalCardSelected(selectedCard);
		currentCardPlay.Start();
		InvokeRefreshLayout(__instance);
		holder.SetIndexLabel(holderIndex + 1);
		return false;
	}

	private static T GetStaticFieldValue<T>(FieldInfo fieldInfo)
	{
		return fieldInfo.GetValue(null) is T value
			? value
			: throw new InvalidOperationException($"[RemoveHandLimit] Could not read field {fieldInfo.DeclaringType?.FullName}.{fieldInfo.Name}.");
	}

	private static StringName[] GetSelectCardShortcuts(NPlayerHand hand)
	{
		return (StringName[])(SelectCardShortcutsField.GetValue(hand) ?? Array.Empty<StringName>());
	}

	private static HashSet<NHandCardHolder> GetHoldersAwaitingQueue(NPlayerHand hand)
	{
		return (HashSet<NHandCardHolder>)(HoldersAwaitingQueueField.GetValue(hand)
			?? throw new InvalidOperationException("[RemoveHandLimit] Could not access _holdersAwaitingQueue."));
	}

	private static void SetDraggedHolderIndex(NPlayerHand hand, int value)
	{
		DraggedHolderIndexField.SetValue(hand, value);
	}

	private static void SetCurrentCardPlay(NPlayerHand hand, NCardPlay cardPlay)
	{
		CurrentCardPlayField.SetValue(hand, cardPlay);
	}

	private static void SetLastFocusedHolderIndex(NPlayerHand hand, int value)
	{
		LastFocusedHolderIndexField.SetValue(hand, value);
	}

	private static void SetFocusedHolder(NPlayerHand hand, NHandCardHolder? holder)
	{
		FocusedHolderProperty.SetValue(hand, holder);
	}

	private static void InvokeRefreshLayout(NPlayerHand hand)
	{
		RefreshLayoutMethod.Invoke(hand, null);
	}

	private static void InvokeReturnHolderToHand(NPlayerHand hand, NHandCardHolder holder)
	{
		ReturnHolderToHandMethod.Invoke(hand, new object[] { holder });
	}

	private static void ValidateHandIndex(int handSize, int cardIndex)
	{
		if (handSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(handSize), handSize, "Hand size must be positive.");
		}

		if (handSize > HandLimit)
		{
			throw new ArgumentOutOfRangeException(nameof(handSize), handSize, $"Hand size {handSize} is greater than {HandLimit}.");
		}

		if (cardIndex < 0 || cardIndex >= handSize)
		{
			throw new ArgumentOutOfRangeException(nameof(cardIndex), cardIndex, $"Card index {cardIndex} is invalid for hand size {handSize}.");
		}
	}

	private static void UpdateHolderHitboxes(NPlayerHand hand)
	{
		foreach (NHandCardHolder holder in hand.ActiveHolders)
		{
			UpdateHolderHitbox(holder);
		}
	}

	private static void UpdateHolderLayering(NPlayerHand hand)
	{
		hand.CardHolderContainer.YSortEnabled = false;
		IReadOnlyList<NHandCardHolder> holders = hand.ActiveHolders;
		if (holders.Count <= RowLimit)
		{
			foreach (NHandCardHolder holder in holders)
			{
				holder.ZIndex = 0;
			}
			return;
		}

		for (int i = 0; i < holders.Count; i++)
		{
			NHandCardHolder holder = holders[i];
			int rowIndex = i % RowLimit;
			int rowBase = i < RowLimit ? RowLimit : 0;
			holder.ZIndex = rowBase + rowIndex;
		}
	}

	private static void UpdateHolderHitbox(NHandCardHolder holder)
	{
		NClickableControl hitbox = holder.Hitbox;
		ulong hitboxId = hitbox.GetInstanceId();
		if (!OriginalHitboxRects.TryGetValue(hitboxId, out Rect2 originalRect))
		{
			originalRect = new Rect2(hitbox.Position, hitbox.Size);
			OriginalHitboxRects[hitboxId] = originalRect;
		}

		hitbox.Position = originalRect.Position;
		hitbox.Size = originalRect.Size;

		if (holder.GetParent() is not Control parent || parent.GetParent() is not NPlayerHand hand)
		{
			return;
		}

		int activeCount = hand.ActiveHolders.Count;
		if (activeCount <= RowLimit)
		{
			return;
		}

		int holderIndex = holder.GetIndex();
		bool isUpperRow = holderIndex >= RowLimit;
		if (!isUpperRow)
		{
			return;
		}

		hitbox.Size = new Vector2(originalRect.Size.X, originalRect.Size.Y * UpperRowHitboxHeightFactor);
	}

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags bindingFlags, params Type[] parameterTypes)
	{
		MethodInfo? method = type.GetMethod(name, bindingFlags, null, parameterTypes, null);
		if (method == null)
		{
			throw new InvalidOperationException($"Could not find method {type.FullName}.{name}.");
		}

		return method;
	}

	private static PropertyInfo RequireProperty(Type type, string name, BindingFlags bindingFlags)
	{
		PropertyInfo? property = type.GetProperty(name, bindingFlags);
		if (property == null)
		{
			throw new InvalidOperationException($"Could not find property {type.FullName}.{name}.");
		}

		return property;
	}

	private static MethodBase GetAsyncStateMachineTarget(MethodInfo method)
	{
		AsyncStateMachineAttribute? attribute = method.GetCustomAttribute<AsyncStateMachineAttribute>();
		if (attribute?.StateMachineType == null)
		{
			return method;
		}

		MethodInfo? moveNext = attribute.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		if (moveNext == null)
		{
			throw new InvalidOperationException($"Could not find MoveNext on async state machine {attribute.StateMachineType.FullName}.");
		}

		return moveNext;
	}

	private static FieldInfo RequireField(Type type, string name, BindingFlags bindingFlags)
	{
		FieldInfo? field = type.GetField(name, bindingFlags);
		if (field == null)
		{
			throw new InvalidOperationException($"Could not find field {type.FullName}.{name}.");
		}

		return field;
	}
}
