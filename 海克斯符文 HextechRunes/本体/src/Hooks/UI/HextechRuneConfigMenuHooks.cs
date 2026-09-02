using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechRuneConfigMenuHooks
{
	private const string LocTable = "relic_collection";
	private const string ButtonName = "HextechRuneConfigButton";
	private const string OverlayName = "HextechRuneConfigOverlay";
	private const int NativeDuplicateFlags = 14;
	private const int OverlayZIndex = 1000;
	private const int HoverTipZIndex = 2000;
	private const int RuneConfigColumns = 8;
	private const string BaseConfigSourceKey = "0:HextechRunes";
	private const string ExternalConfigSourcePrefix = "1:";
	private const string SponsorPackModId = "HextechRunesSponsorPack";
	private const float ConfigRuneHolderScale = 1.3f;
	private const float RuneConfigCellWidth = 108f;
	private const float RuneConfigCellHeight = 136f;
	private const float RuneConfigIconLayerHeight = 96f;
	private const float RuneConfigDragThreshold = 12f;
	private const float RuneConfigLongPressSeconds = 0.35f;
	private const float StepRepeatInitialDelaySeconds = 0.35f;
	private const float StepRepeatIntervalSeconds = 0.075f;
	private const float StepRepeatFastIntervalSeconds = 0.035f;
	private const int StepRepeatFastAfterTicks = 10;
	private const int RuneConfigIconsPerFrame = 12;
	private const float CompactConfigHeightThreshold = 820f;
	private const float OverlayOpenSeconds = 0.16f;
	private const float OverlayCloseSeconds = 0.12f;
	private const float OverlayOpenScale = 0.965f;
	private const float PageTransitionSeconds = 0.13f;
	private const float TabIndicatorSlideSeconds = 0.16f;
	private const float RuneStateFadeSeconds = 0.12f;
	private const float ToggleKnobSlideSeconds = 0.17f;
	private const string ConfigPanelName = "HextechRuneConfigPanel";
	private const string TabIndicatorName = "HextechRuneConfigTabIndicator";
	private const string MainMenuLocTable = "main_menu_ui";

	/// <summary>
	/// 在 NMainMenu._Ready 之前把按钮插进 %MainMenuTextButtons:原版随后自己的
	/// ConnectMainMenuTextButtonFocusLogic 会把焦点光标动画连到它身上,文案走公开的
	/// SetLocalization(main_menu_ui 表由本模组的 loc 文件合并),不再触碰任何私有成员。
	/// </summary>
	private static void TryAttachButton(NMainMenu host)
	{
		if (host.FindChild(ButtonName, recursive: true, owned: false) is NMainMenuTextButton existing
			&& GodotObject.IsInstanceValid(existing))
		{
			return;
		}

		if ((host.GetNodeOrNull<Control>("%MainMenuTextButtons") ?? host.GetNodeOrNull<Control>("MainMenuTextButtons")) is not { } buttonHost
			|| buttonHost.GetNodeOrNull<NMainMenuTextButton>("SettingsButton") is not { } settingsButton)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Main menu config button skipped: native menu buttons were not available.", 2);
			return;
		}

		NMainMenuTextButton configButton = (NMainMenuTextButton)((Node)settingsButton).Duplicate(NativeDuplicateFlags);
		((Node)configButton).Name = ButtonName;
		((Node)configButton).UniqueNameInOwner = true;
		buttonHost.AddChild(configButton);
		buttonHost.MoveChild(configButton, Math.Min(settingsButton.GetIndex() + 1, buttonHost.GetChildCount() - 1));
		configButton.SetLocalization("HEXTECH_CONFIG_BUTTON");
		((Control)configButton).TooltipText = new LocString(MainMenuLocTable, "HEXTECH_CONFIG_BUTTON_TOOLTIP").GetRawText();
		ConfigureNativeMenuButton(configButton, settingsButton);
		ConfigureNativeMenuNeighbors(buttonHost, configButton, settingsButton);
		((GodotObject)configButton).Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(_ => OpenOverlay(configButton)));
		HextechLog.Info($"[{ModInfo.Id}][RuneConfig] Main menu config button attached.");
	}

	private static void ConfigureNativeMenuNeighbors(Control buttonHost, NMainMenuTextButton configButton, NMainMenuTextButton settingsButton)
	{
		Control configControl = configButton;
		Control settingsControl = settingsButton;
		configControl.FocusNeighborTop = settingsControl.GetPath();
		settingsControl.FocusNeighborBottom = configControl.GetPath();

		int nextIndex = configButton.GetIndex() + 1;
		if (nextIndex < buttonHost.GetChildCount() && buttonHost.GetChild(nextIndex) is Control nextControl)
		{
			configControl.FocusNeighborBottom = nextControl.GetPath();
			nextControl.FocusNeighborTop = configControl.GetPath();
		}
		else
		{
			configControl.FocusNeighborBottom = configControl.GetPath();
		}
	}

	private static void ConfigureNativeMenuButton(NMainMenuTextButton configButton, NMainMenuTextButton template)
	{
		Control control = configButton;
		control.MouseFilter = Control.MouseFilterEnum.Stop;
		control.FocusMode = Control.FocusModeEnum.All;
		control.MouseDefaultCursorShape = ((Control)template).MouseDefaultCursorShape;
		control.SizeFlagsHorizontal = template.SizeFlagsHorizontal;
		control.SizeFlagsVertical = template.SizeFlagsVertical;
		control.CustomMinimumSize = template.CustomMinimumSize;
		control.ZIndex = ((Control)template).ZIndex;
		control.ZAsRelative = ((Control)template).ZAsRelative;
	}

	[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready), new Type[0])]
	[HextechPatch("ui.rune-config-menu", "海克斯配置菜单")]
	private static class MainMenuReadyPatch
	{
		[HarmonyPrefix]
		private static void Prefix(NMainMenu __instance)
		{
			try
			{
				TryAttachButton(__instance);
			}
			catch (Exception ex)
			{
				Log.Warn($"[{ModInfo.Id}][RuneConfig] Main menu button install failed: {ex.Message}", 2);
			}
		}
	}
}
