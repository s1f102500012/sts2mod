using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KeystoneRunes;

internal sealed class KeystoneRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private const string SkipButtonScenePath = "res://scenes/ui/choice_selection_skip_button.tscn";

	private const string LocTable = "relic_collection";

	private readonly TaskCompletionSource<IEnumerable<RelicModel>> _completionSource = new();

	private readonly IReadOnlyList<ModInfo.RuneSeriesGroup> _groups;

	private readonly List<Control> _holders = new();

	private NChoiceSelectionSkipButton? _skipButton;

	public NetScreenType ScreenType => NetScreenType.Rewards;

	public bool UseSharedBackstop => true;

	public Control? DefaultFocusedControl => _holders.FirstOrDefault();

	private KeystoneRuneSelectionScreen(IReadOnlyList<ModInfo.RuneSeriesGroup> groups)
	{
		_groups = groups;
		Name = nameof(KeystoneRuneSelectionScreen);
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.All;
		Visible = true;
		BuildUi();
	}

	public static KeystoneRuneSelectionScreen Create(IReadOnlyList<RelicModel> relics)
	{
		return new KeystoneRuneSelectionScreen(ModInfo.GetRuneSeriesGroups(relics));
	}

	private void BuildUi()
	{
		VBoxContainer root = new()
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 20);
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(root);

		root.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		MegaLabel title = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MaxFontSize = 56,
			MinFontSize = 38,
			Position = new Vector2(0f, -34f)
		};
		ApplyDefaultMegaLabelTheme(title);
		title.Modulate = Colors.White;
		title.SetTextAutoSize(new LocString(LocTable, "KEYSTONE_SELECTION_TITLE").GetRawText());
		root.AddChild(title);

		HBoxContainer columns = new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Position = new Vector2(0f, -20f)
		};
		columns.AddThemeConstantOverride("separation", 28);
		root.AddChild(columns);

		foreach (ModInfo.RuneSeriesGroup group in _groups)
		{
			VBoxContainer column = new()
			{
				CustomMinimumSize = new Vector2(180f, 0f)
			};
			column.AddThemeConstantOverride("separation", 18);
			columns.AddChild(column);

			MegaLabel label = new()
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				MaxFontSize = 36,
				MinFontSize = 24,
				Position = new Vector2(0f, -12f)
			};
			ApplyDefaultMegaLabelTheme(label);
			label.Modulate = Colors.White;
			label.SetTextAutoSize(new LocString(LocTable, "KEYSTONE_SERIES." + group.LocalizationKey).GetRawText());
			column.AddChild(label);

			foreach (RelicModel relic in group.Relics)
			{
				VBoxContainer option = new()
				{
					CustomMinimumSize = new Vector2(170f, 140f),
					SizeFlagsHorizontal = SizeFlags.ExpandFill
				};
				option.AddThemeConstantOverride("separation", 6);
				column.AddChild(option);

				CenterContainer buttonCenter = new()
				{
					SizeFlagsHorizontal = SizeFlags.ExpandFill
				};
				option.AddChild(buttonCenter);

				Button button = new()
				{
					CustomMinimumSize = new Vector2(120f, 96f),
					SizeFlagsHorizontal = SizeFlags.ExpandFill
				};
				button.Text = "";
				button.Flat = true;
				StyleBoxEmpty empty = new();
				button.AddThemeStyleboxOverride("normal", empty);
				button.AddThemeStyleboxOverride("hover", empty);
				button.AddThemeStyleboxOverride("pressed", empty);
				button.AddThemeStyleboxOverride("focus", empty);
				buttonCenter.AddChild(button);

				NRelic relicNode = NRelic.Create(relic, NRelic.IconSize.Small)
					?? throw new InvalidOperationException("Failed to create relic node.");
				relicNode.MouseFilter = MouseFilterEnum.Ignore;
				relicNode.Scale = Vector2.One * 1.8f;
				relicNode.Position = new Vector2(26f, 6f);
				button.AddChild(relicNode);

				MegaLabel relicLabel = new()
				{
					HorizontalAlignment = HorizontalAlignment.Center,
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					MaxFontSize = 22,
					MinFontSize = 14
				};
				ApplyDefaultMegaLabelTheme(relicLabel);
				relicLabel.Modulate = Colors.White;
				relicLabel.SetTextAutoSize(relic.Title.GetFormattedText());
				option.AddChild(relicLabel);

				button.Pressed += () => OnHolderSelected(relic);
				button.MouseEntered += () => ShowHoverTip(button, relicNode, relic);
				button.MouseExited += () => HideHoverTip(button, relicNode);
				_holders.Add(button);
			}
		}

		root.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

		PackedScene? skipScene = ResourceLoader.Load<PackedScene>(SkipButtonScenePath, cacheMode: ResourceLoader.CacheMode.Reuse);
		if (skipScene == null)
		{
			throw new InvalidOperationException($"Could not load skip button scene: {SkipButtonScenePath}");
		}

		NChoiceSelectionSkipButton skipButton = skipScene.Instantiate<NChoiceSelectionSkipButton>();
		skipButton.Name = "KeystoneSkipButton";
		if (skipButton.GetNodeOrNull("Label") is GodotObject labelNode)
		{
			labelNode.Call("SetTextAutoSize", new LocString(LocTable, "KEYSTONE_SKIP").GetRawText());
		}

		skipButton.Connect("Released", Callable.From(OnSkipPressed));
		AddChild(skipButton);
		_skipButton = skipButton;
		CallDeferred(nameof(UpdateSkipButtonLayout));
	}

	private void OnHolderSelected(RelicModel relic)
	{
		_completionSource.TrySetResult([relic]);
	}

	private void OnSkipPressed()
	{
		_completionSource.TrySetResult(Array.Empty<RelicModel>());
	}

	public async Task<IEnumerable<RelicModel>> RelicsSelected()
	{
		IEnumerable<RelicModel> result = await _completionSource.Task;
		NOverlayStack.Instance?.Remove(this);
		return result;
	}

	public void AfterOverlayOpened()
	{
		Modulate = Colors.White;
		Visible = true;
		CallDeferred(nameof(UpdateSkipButtonLayout));
	}

	public void AfterOverlayClosed()
	{
		QueueFree();
	}

	public void AfterOverlayShown()
	{
		Visible = true;
		CallDeferred(nameof(UpdateSkipButtonLayout));
	}

	public void AfterOverlayHidden()
	{
		Visible = false;
	}

	private void UpdateSkipButtonLayout()
	{
		if (!IsInstanceValid(_skipButton))
		{
			return;
		}

		if (!IsInsideTree())
		{
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		Vector2 size = _skipButton!.Size == Vector2.Zero ? _skipButton.GetCombinedMinimumSize() : _skipButton.Size;
		_skipButton.GlobalPosition = GlobalPosition + new Vector2((viewportSize.X - size.X) * 0.5f, viewportSize.Y - size.Y - 56f);
	}

	private static void ShowHoverTip(Control owner, NRelic relicNode, RelicModel relic)
	{
		relicNode.Icon.Scale = Vector2.One * 1.25f;
		NHoverTipSet tipSet = NHoverTipSet.CreateAndShow(owner, relic.HoverTips, HoverTip.GetHoverTipAlignment(owner));
		tipSet.SetAlignmentForRelic(relicNode);
	}

	private static void HideHoverTip(Control owner, NRelic relicNode)
	{
		relicNode.Icon.Scale = Vector2.One;
		NHoverTipSet.Remove(owner);
	}

	private static void ApplyDefaultMegaLabelTheme(MegaLabel label)
	{
		Font font = label.GetThemeDefaultFont();
		if (font != null)
		{
			label.AddThemeFontOverride("font", font);
		}

		int fontSize = label.GetThemeDefaultFontSize();
		if (fontSize > 0)
		{
			label.AddThemeFontSizeOverride("font_size", fontSize);
		}
	}
}
