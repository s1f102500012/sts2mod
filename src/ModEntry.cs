using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Collections.Generic;
using Godot;
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
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace RemoveHandLimit;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
	private const int HandLimit = 20;

	private const int RowLimit = 10;

	private const float UpperRowYOffset = -72f;

	private const float LowerRowYOffset = 28f;

	private const float UpperRowAngleFactor = 0.82f;

	private const float UpperRowHitboxHeightFactor = 0.56f;

	private const int HoverTipZIndex = 1000;

	private static ILHook? _cardPileAddIlHook;

	private static ILHook? _cardPileDrawIlHook;

	private static ILHook? _cardPileCanDrawIlHook;

	private static Hook? _handPosGetPositionHook;

	private static Hook? _handPosGetAngleHook;

	private static Hook? _handPosGetScaleHook;

	private static Hook? _refreshLayoutHook;

	private static Hook? _handCardHoverHook;

	private static Hook? _onHolderFocusedHook;

	private static Hook? _onHolderUnfocusedHook;

	private static Hook? _hoverTipAlignmentHook;

	private static Hook? _startCardPlayHook;

	private static readonly Dictionary<ulong, Rect2> OriginalHitboxRects = new();

	private static readonly HashSet<ulong> HoveredHolderIds = new();

	private static readonly FieldInfo SelectCardShortcutsField = RequireField(typeof(NPlayerHand), "_selectCardShortcuts", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo DraggedHolderIndexField = RequireField(typeof(NPlayerHand), "_draggedHolderIndex", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo HoldersAwaitingQueueField = RequireField(typeof(NPlayerHand), "_holdersAwaitingQueue", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo CurrentCardPlayField = RequireField(typeof(NPlayerHand), "_currentCardPlay", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo LastFocusedHolderIndexField = RequireField(typeof(NPlayerHand), "_lastFocusedHolderIdx", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo RefreshLayoutMethod = RequireMethod(typeof(NPlayerHand), "RefreshLayout", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo ReturnHolderToHandMethod = RequireMethod(typeof(NPlayerHand), "ReturnHolderToHand", BindingFlags.Instance | BindingFlags.NonPublic, typeof(NHandCardHolder));

	private static readonly PropertyInfo FocusedHolderProperty = RequireProperty(typeof(NPlayerHand), nameof(NPlayerHand.FocusedHolder), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private delegate Vector2 OrigGetPosition(int handSize, int cardIndex);

	private delegate float OrigGetAngle(int handSize, int cardIndex);

	private delegate Vector2 OrigGetScale(int handSize);

	private delegate void OrigRefreshLayout(NPlayerHand self);

	private delegate void OrigDoCardHoverEffects(NHandCardHolder self, bool isHovered);

	private delegate void OrigOnHolderFocused(NPlayerHand self, NHandCardHolder holder);

	private delegate void OrigOnHolderUnfocused(NPlayerHand self, NHandCardHolder holder);

	private delegate void OrigSetAlignmentForCardHolder(NHoverTipSet self, NCardHolder holder);

	private delegate void OrigStartCardPlay(NPlayerHand self, NHandCardHolder holder, bool startedViaShortcut);

	public static void Initialize()
	{
		PreloadDependencyAssemblies();
		InstallHooks();
		Log.Info($"[RemoveHandLimit] Loaded. Hand limit patched to {HandLimit} with two 10-card rows.");
	}

	private static void PreloadDependencyAssemblies()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		string? modDirectory = Path.GetDirectoryName(assembly.Location);
		if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
		{
			return;
		}

		string selfPath = assembly.Location;
		AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(assembly) ?? AssemblyLoadContext.Default;
		foreach (string dllPath in Directory.GetFiles(modDirectory, "*.dll"))
		{
			if (string.Equals(dllPath, selfPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			loadContext.LoadFromAssemblyPath(dllPath);
		}
	}

	private static void InstallHooks()
	{
		MethodInfo addCardsMethod = RequireMethod(
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
		MethodInfo handCardHoverMethod = RequireMethod(
			typeof(NHandCardHolder),
			"DoCardHoverEffects",
			BindingFlags.Instance | BindingFlags.NonPublic,
			typeof(bool));
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
		MethodInfo hoverTipAlignmentMethod = RequireMethod(
			typeof(NHoverTipSet),
			nameof(NHoverTipSet.SetAlignmentForCardHolder),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(NCardHolder));
		MethodInfo startCardPlayMethod = RequireMethod(
			typeof(NPlayerHand),
			"StartCardPlay",
			BindingFlags.Instance | BindingFlags.NonPublic,
			typeof(NHandCardHolder),
			typeof(bool));

		_cardPileAddIlHook = new ILHook(GetAsyncStateMachineTarget(addCardsMethod), PatchAddCardsIl);
		_cardPileDrawIlHook = new ILHook(GetAsyncStateMachineTarget(drawMethod), PatchDrawIl);
		_cardPileCanDrawIlHook = new ILHook(checkCanDrawMethod, PatchCheckCanDrawIl);
		_handPosGetPositionHook = new Hook(handPosGetPosition, GetPositionDetour);
		_handPosGetAngleHook = new Hook(handPosGetAngle, GetAngleDetour);
		_handPosGetScaleHook = new Hook(handPosGetScale, GetScaleDetour);
		_refreshLayoutHook = new Hook(refreshLayoutMethod, RefreshLayoutDetour);
		_handCardHoverHook = new Hook(handCardHoverMethod, DoCardHoverEffectsDetour);
		_onHolderFocusedHook = new Hook(onHolderFocusedMethod, OnHolderFocusedDetour);
		_onHolderUnfocusedHook = new Hook(onHolderUnfocusedMethod, OnHolderUnfocusedDetour);
		_hoverTipAlignmentHook = new Hook(hoverTipAlignmentMethod, SetAlignmentForCardHolderDetour);
		_startCardPlayHook = new Hook(startCardPlayMethod, StartCardPlayDetour);
	}

	private static void PatchAddCardsIl(ILContext il)
	{
		ReplaceIntLoads(il, expectedCount: 1);
	}

	private static void PatchDrawIl(ILContext il)
	{
		ReplaceIntLoads(il, expectedCount: 3);
	}

	private static void PatchCheckCanDrawIl(ILContext il)
	{
		ReplaceIntLoads(il, expectedCount: 1);
	}

	private static void ReplaceIntLoads(ILContext il, int expectedCount)
	{
		int patched = 0;
		foreach (Instruction instruction in il.Method.Body.Instructions)
		{
				if (!TryGetLoadedInt32(instruction, out int value) || value != 10)
				{
					continue;
				}

				instruction.OpCode = OpCodes.Ldc_I4;
				instruction.Operand = HandLimit;
				patched++;
			}

		if (patched != expectedCount)
		{
			throw new InvalidOperationException($"[RemoveHandLimit] Expected to patch {expectedCount} int loads in {il.Method.FullName}, but patched {patched}.");
		}
	}

	private static bool TryGetLoadedInt32(Instruction instruction, out int value)
	{
		switch (instruction.OpCode.Code)
		{
		case Code.Ldc_I4_M1:
			value = -1;
			return true;
		case Code.Ldc_I4_0:
			value = 0;
			return true;
		case Code.Ldc_I4_1:
			value = 1;
			return true;
		case Code.Ldc_I4_2:
			value = 2;
			return true;
		case Code.Ldc_I4_3:
			value = 3;
			return true;
		case Code.Ldc_I4_4:
			value = 4;
			return true;
		case Code.Ldc_I4_5:
			value = 5;
			return true;
		case Code.Ldc_I4_6:
			value = 6;
			return true;
		case Code.Ldc_I4_7:
			value = 7;
			return true;
		case Code.Ldc_I4_8:
			value = 8;
			return true;
		case Code.Ldc_I4_S:
			value = (sbyte)instruction.Operand;
			return true;
		case Code.Ldc_I4:
			value = (int)instruction.Operand;
			return true;
		default:
			value = 0;
			return false;
		}
	}

	private static Vector2 GetPositionDetour(OrigGetPosition orig, int handSize, int cardIndex)
	{
		if (handSize <= RowLimit)
		{
			return orig(handSize, cardIndex);
		}

		ValidateHandIndex(handSize, cardIndex);
		if (cardIndex < RowLimit)
		{
			Vector2 basePosition = orig(RowLimit, cardIndex);
			return new Vector2(basePosition.X, basePosition.Y + LowerRowYOffset);
		}

		int upperRowCount = handSize - RowLimit;
		int upperRowIndex = cardIndex - RowLimit;
		Vector2 upperBasePosition = orig(upperRowCount, upperRowIndex);
		return new Vector2(upperBasePosition.X, upperBasePosition.Y + UpperRowYOffset);
	}

	private static float GetAngleDetour(OrigGetAngle orig, int handSize, int cardIndex)
	{
		if (handSize <= RowLimit)
		{
			return orig(handSize, cardIndex);
		}

		ValidateHandIndex(handSize, cardIndex);
		if (cardIndex < RowLimit)
		{
			return orig(RowLimit, cardIndex);
		}

		int upperRowCount = handSize - RowLimit;
		int upperRowIndex = cardIndex - RowLimit;
		return orig(upperRowCount, upperRowIndex) * UpperRowAngleFactor;
	}

	private static Vector2 GetScaleDetour(OrigGetScale orig, int handSize)
	{
		if (handSize <= RowLimit)
		{
			return orig(handSize);
		}

		return orig(RowLimit);
	}

	private static void RefreshLayoutDetour(OrigRefreshLayout orig, NPlayerHand self)
	{
		if (self.ActiveHolders.Count > RowLimit)
		{
			SetFocusedHolder(self, null);
		}

		orig(self);
		UpdateHolderZIndices(self);
	}

	private static void DoCardHoverEffectsDetour(OrigDoCardHoverEffects orig, NHandCardHolder self, bool isHovered)
	{
		SetHolderHoverState(self, isHovered);
		orig(self, isHovered);
		ApplyHolderZIndex(self, isHovered);
	}

	private static void OnHolderFocusedDetour(OrigOnHolderFocused orig, NPlayerHand self, NHandCardHolder holder)
	{
		if (self.ActiveHolders.Count <= RowLimit)
		{
			orig(self, holder);
			return;
		}

		SetHolderHoverState(holder, isHovered: true);
		SetLastFocusedHolderIndex(self, holder.GetIndex());
		if (holder.CardModel != null)
		{
			RunManager.Instance.HoveredModelTracker.OnLocalCardHovered(holder.CardModel);
		}
		UpdateHolderZIndices(self);
	}

	private static void OnHolderUnfocusedDetour(OrigOnHolderUnfocused orig, NPlayerHand self, NHandCardHolder holder)
	{
		if (self.ActiveHolders.Count <= RowLimit)
		{
			orig(self, holder);
			return;
		}

		SetHolderHoverState(holder, isHovered: false);
		RunManager.Instance.HoveredModelTracker.OnLocalCardUnhovered();
		UpdateHolderZIndices(self);
	}

	private static void SetAlignmentForCardHolderDetour(OrigSetAlignmentForCardHolder orig, NHoverTipSet self, NCardHolder holder)
	{
		orig(self, holder);
		BringHoverTipToFront(self);
	}

	private static void StartCardPlayDetour(OrigStartCardPlay orig, NPlayerHand self, NHandCardHolder holder, bool startedViaShortcut)
	{
		StringName[] shortcuts = GetSelectCardShortcuts(self);
		int holderIndex = holder.GetIndex();
		if ((NControllerManager.Instance?.IsUsingController ?? false) || holderIndex < shortcuts.Length)
		{
			orig(self, holder, startedViaShortcut);
			return;
		}

		SetDraggedHolderIndex(self, holderIndex);
		GetHoldersAwaitingQueue(self).Add(holder, holderIndex);
		holder.Reparent(self);
		holder.BeginDrag();

		NCardPlay currentCardPlay = NMouseCardPlay.Create(holder, MegaInput.releaseCard, startedViaShortcut);
		SetCurrentCardPlay(self, currentCardPlay);
		self.AddChildSafely(currentCardPlay);
		currentCardPlay.Connect(NCardPlay.SignalName.Finished, Callable.From(delegate(bool success)
		{
			RunManager.Instance.HoveredModelTracker.OnLocalCardDeselected();
			if (!success)
			{
				InvokeReturnHolderToHand(self, holder);
			}

			SetDraggedHolderIndex(self, -1);
			InvokeRefreshLayout(self);
		}));

		CardModel selectedCard = holder.CardNode?.Model ?? throw new InvalidOperationException("[RemoveHandLimit] Tried to start card play without a card node.");
		RunManager.Instance.HoveredModelTracker.OnLocalCardSelected(selectedCard);
		currentCardPlay.Start();
		InvokeRefreshLayout(self);
		holder.SetIndexLabel(holderIndex + 1);
	}

	private static StringName[] GetSelectCardShortcuts(NPlayerHand hand)
	{
		return (StringName[])(SelectCardShortcutsField.GetValue(hand) ?? Array.Empty<StringName>());
	}

	private static Dictionary<NHandCardHolder, int> GetHoldersAwaitingQueue(NPlayerHand hand)
	{
		return (Dictionary<NHandCardHolder, int>)(HoldersAwaitingQueueField.GetValue(hand)
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

		if (cardIndex < 0 || cardIndex >= handSize)
		{
			throw new ArgumentOutOfRangeException(nameof(cardIndex), cardIndex, $"Card index {cardIndex} is invalid for hand size {handSize}.");
		}
	}

	private static void UpdateHolderZIndices(NPlayerHand hand)
	{
		foreach (NHandCardHolder holder in hand.ActiveHolders)
		{
			ApplyHolderZIndex(holder, IsHolderHovered(holder));
			UpdateHolderHitbox(holder);
		}
	}

	private static void ApplyHolderZIndex(NHandCardHolder holder, bool isHovered)
	{
		if (holder.GetParent() is not Control parent || parent.GetParent() is not NPlayerHand hand)
		{
			return;
		}

		int activeCount = hand.ActiveHolders.Count;
		int holderIndex = holder.GetIndex();
		bool useTwoRows = activeCount > RowLimit;
		bool isLowerRow = !useTwoRows || holderIndex < RowLimit;
		int baseZ = isLowerRow ? 10 : 0;
		int visualZ = baseZ + (isHovered ? 100 : 0);
		holder.ZIndex = visualZ;
		holder.Hitbox.ZIndex = baseZ;
	}

	private static void SetHolderHoverState(NHandCardHolder holder, bool isHovered)
	{
		ulong holderId = holder.GetInstanceId();
		if (isHovered)
		{
			HoveredHolderIds.Add(holderId);
		}
		else
		{
			HoveredHolderIds.Remove(holderId);
		}
	}

	private static bool IsHolderHovered(NHandCardHolder holder)
	{
		return HoveredHolderIds.Contains(holder.GetInstanceId());
	}

	private static void BringHoverTipToFront(NHoverTipSet hoverTipSet)
	{
		if (NGame.Instance?.HoverTipsContainer is CanvasItem hoverTipsContainer)
		{
			hoverTipsContainer.ZIndex = HoverTipZIndex;
			hoverTipsContainer.MoveToFront();
		}

		hoverTipSet.ZIndex = HoverTipZIndex;
		hoverTipSet.MoveToFront();
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
