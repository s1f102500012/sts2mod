using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Relics;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechRelicVisibilityHooks
{
	private const string ConfigFileName = "ui_config.json";
	private const string ToggleRootNodeName = "HextechHideRelicsToggleRoot";
	private const string ToggleColumnNodeName = "HextechHideRelicsToggleColumn";
	private const string ToggleBoxNodeName = "HextechHideRelicsToggleBox";
	private const string ToggleVisualsNodeName = "TickboxVisuals";
	private const string ToggleButtonNodeName = "HextechHideRelicsToggleButton";
	private const string ToggleLabelNodeName = "HextechHideRelicsToggleLabel";
	private const string PositionTimerNodeName = "HextechHideRelicsTogglePositionTimer";
	private const string TickboxVisualScenePath = "res://scenes/ui/tickbox.tscn";
	private const BindingFlags CombatUiFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
	private static readonly Vector2 ToggleRootSize = new(72f, 80f);
	private static readonly Vector2 ToggleBoxSize = new(64f, 64f);
	private const float DrawPileGap = 10f;
	private const float BottomFallbackPadding = 34f;
	private const float LeftFallbackPadding = 150f;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private static bool _installed;
	private static bool _multiplayerStateHiddenByToggle;
	private static NDrawPileButton? _drawPileAnchor;
	private static ModUiConfig _config = new();


	private static void NGlobalUiInitializePostfix(NGlobalUi __instance)
	{
		try
		{
			InstallToggle(__instance);
			ApplyHiddenState(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic visibility toggle install failed: {ex.Message}", 2);
		}
	}

	private static void NCombatUiVisiblePostfix(NCombatUi __instance)
	{
		try
		{
			_drawPileAnchor = __instance.DrawPile;
			NGlobalUi? globalUi = NRun.Instance?.GlobalUi;
			if (globalUi == null || !GodotObject.IsInstanceValid(globalUi))
			{
				return;
			}

			InstallToggle(globalUi);
			ApplyHiddenState(globalUi);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic visibility toggle refresh failed: {ex.Message}", 2);
		}
	}

	private static void NCombatUiHiddenPostfix()
	{
		_drawPileAnchor = null;
		RefreshToggleRootPosition();
	}

	private static void NRelicInventoryRefreshPostfix(NRelicInventory __instance)
	{
		try
		{
			ApplyHiddenState(__instance);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Relic visibility refresh failed: {ex.Message}", 2);
		}
	}

	private static bool NRelicInventoryHolderDoFlashPrefix()
	{
		return !ShouldHideUi();
	}

	private static void InstallToggle(NGlobalUi globalUi)
	{
		if (!_config.ShowHiddenRelicsToggle)
		{
			RemoveToggleRoot(globalUi);
			return;
		}

		Control? root = FindToggleRoot(globalUi);
		Button? button = root?.GetNodeOrNull<Button>($"{ToggleColumnNodeName}/{ToggleBoxNodeName}/{ToggleButtonNodeName}");
		if (root == null || !GodotObject.IsInstanceValid(root) || button == null || !GodotObject.IsInstanceValid(button))
		{
			if (root != null && GodotObject.IsInstanceValid(root))
			{
				root.GetParent()?.RemoveChild(root);
				root.QueueFree();
			}

			root = CreateToggleRoot();
			globalUi.AddChild(root);
			globalUi.MoveChild(root, globalUi.GetChildCount() - 1);
			button = root.GetNode<Button>($"{ToggleColumnNodeName}/{ToggleBoxNodeName}/{ToggleButtonNodeName}");
		}

		button.SetPressedNoSignal(_config.HideRelics);
		UpdateToggleVisualState(root, _config.HideRelics);
		root.Visible = true;
		EnsurePositionTimer(globalUi, root);
		PositionToggleRoot(root);
		Callable.From(() => PositionToggleRoot(root)).CallDeferred();
	}

	private static void OnToggleChanged(bool hideUi)
	{
		_config.HideRelics = hideUi;
		SaveConfig(_config);
		NGlobalUi? globalUi = NRun.Instance?.GlobalUi;
		if (globalUi?.GetNodeOrNull<Control>(ToggleRootNodeName) is { } root && GodotObject.IsInstanceValid(root))
		{
			UpdateToggleVisualState(root, hideUi);
		}

		ApplyHiddenState(globalUi);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] hide_ui={hideUi}.");
	}

	private static void RemoveToggleRoot(NGlobalUi globalUi)
	{
		if (FindToggleRoot(globalUi) is { } root && GodotObject.IsInstanceValid(root))
		{
			root.GetParent()?.RemoveChild(root);
			root.QueueFree();
		}
	}

	private static void ApplyHiddenState(NRelicInventory? inventory)
	{
		if (inventory == null || !GodotObject.IsInstanceValid(inventory))
		{
			return;
		}

		bool showRelics = !ShouldHideUi();
		foreach (NRelicInventoryHolder holder in inventory.RelicNodes)
		{
			if (holder == null || !GodotObject.IsInstanceValid(holder))
			{
				continue;
			}

			holder.Visible = showRelics;
		}
	}

	private static void ApplyHiddenState(NGlobalUi? globalUi)
	{
		if (globalUi == null || !GodotObject.IsInstanceValid(globalUi))
		{
			return;
		}

		ApplyHiddenState(globalUi.RelicInventory);
		ApplyMultiplayerStateVisibility(globalUi);
	}

	private static void ApplyMultiplayerStateVisibility(NGlobalUi globalUi)
	{
		if (globalUi.MultiplayerPlayerContainer == null
			|| !GodotObject.IsInstanceValid(globalUi.MultiplayerPlayerContainer))
		{
			return;
		}

		if (ShouldHideUi())
		{
			globalUi.MultiplayerPlayerContainer.HideImmediately();
			_multiplayerStateHiddenByToggle = true;
		}
		else if (_multiplayerStateHiddenByToggle)
		{
			globalUi.MultiplayerPlayerContainer.ShowImmediately();
			_multiplayerStateHiddenByToggle = false;
		}
	}

	private static bool ShouldHideUi()
	{
		return _config.ShowHiddenRelicsToggle && _config.HideRelics;
	}

	[HextechPatch("ui.relic-visibility", "遗物栏隐藏开关")]
	private static class VisibilityPatches
	{
		public static void Apply(Harmony harmony)
		{
			if (_installed)
			{
				return;
			}

			_config = LoadOrCreateConfig();
			harmony.Patch(
				RequireMethod(typeof(NGlobalUi), nameof(NGlobalUi.Initialize), BindingFlags.Instance | BindingFlags.Public, typeof(RunState)),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NGlobalUiInitializePostfix)));
			harmony.Patch(
				RequireMethod(typeof(NCombatUi), nameof(NCombatUi._Ready), CombatUiFlags),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NCombatUiVisiblePostfix)));
			harmony.Patch(
				RequireMethod(typeof(NCombatUi), "AnimIn", CombatUiFlags),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NCombatUiVisiblePostfix)));
			harmony.Patch(
				RequireMethod(typeof(NCombatUi), nameof(NCombatUi.Enable), CombatUiFlags),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NCombatUiVisiblePostfix)));
			harmony.Patch(
				RequireMethod(typeof(NCombatUi), "AnimOut", CombatUiFlags),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NCombatUiHiddenPostfix)));
			harmony.Patch(
				RequireMethod(typeof(NCombatUi), nameof(NCombatUi.Disable), CombatUiFlags),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NCombatUiHiddenPostfix)));
			harmony.Patch(
				RequireMethod(typeof(NCombatUi), nameof(NCombatUi._ExitTree), CombatUiFlags),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NCombatUiHiddenPostfix)));
			harmony.Patch(
				RequireMethod(typeof(NRelicInventory), nameof(NRelicInventory.Initialize), BindingFlags.Instance | BindingFlags.Public, typeof(RunState)),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NRelicInventoryRefreshPostfix)));
			harmony.Patch(
				RequireMethod(typeof(NRelicInventory), "Add", BindingFlags.Instance | BindingFlags.NonPublic, typeof(RelicModel), typeof(bool), typeof(int)),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NRelicInventoryRefreshPostfix)));
			harmony.Patch(
				RequireMethod(typeof(NRelicInventory), nameof(NRelicInventory.AnimShow), BindingFlags.Instance | BindingFlags.Public),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NRelicInventoryRefreshPostfix)));
			harmony.Patch(
				RequireMethod(typeof(NRelicInventory), nameof(NRelicInventory.ShowImmediately), BindingFlags.Instance | BindingFlags.Public),
				postfix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NRelicInventoryRefreshPostfix)));
			harmony.Patch(
				RequireMethod(typeof(NRelicInventoryHolder), "DoFlash", BindingFlags.Instance | BindingFlags.NonPublic),
				prefix: new HarmonyMethod(typeof(HextechRelicVisibilityHooks), nameof(NRelicInventoryHolderDoFlashPrefix)));

			_installed = true;
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] UI visibility toggle loaded: hide_ui={_config.HideRelics}.");
		}
	}
}
