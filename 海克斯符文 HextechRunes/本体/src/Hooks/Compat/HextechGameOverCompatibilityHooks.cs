using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechGameOverCompatibilityHooks
{


	private static NScoreLine CreateFallbackScoreLine(string label, string score, Texture2D? icon)
	{
		NScoreLine line = new()
		{
			Name = "HextechFallbackScoreLine",
			CustomMinimumSize = new Vector2(720f, 44f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0f)
		};

		HBoxContainer row = new()
		{
			Name = "HextechFallbackScoreLineRow",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 14);
		line.AddChild(row);

		if (icon != null)
		{
			row.AddChild(new TextureRect
			{
				Name = "Icon",
				Texture = icon,
				CustomMinimumSize = new Vector2(32f, 32f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
		}

		MegaLabel labelNode = CreateLabel("Label", label, HorizontalAlignment.Left, expand: true);
		MegaLabel scoreNode = CreateLabel("Score", score, HorizontalAlignment.Right, expand: false);
		row.AddChild(labelNode);
		row.AddChild(scoreNode);
		return line;
	}

	private static MegaLabel CreateLabel(string name, string text, HorizontalAlignment alignment, bool expand)
	{
		MegaLabel label = new()
		{
			Name = name,
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkEnd,
			MinFontSize = 16,
			MaxFontSize = 28,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(label);
		label.SetTextAutoSize(text);
		return label;
	}

	[HarmonyPatch(typeof(NScoreLine), nameof(NScoreLine.Create), typeof(string), typeof(string), typeof(Texture2D))]
	[HextechPatch("compat.game-over-score-line", "结算记分行兼容", Optional = true)]
	private static class ScoreLineCreatePatch
	{
		[HarmonyFinalizer]
		private static Exception? Finalizer(
			string label,
			string score,
			Texture2D? icon,
			ref NScoreLine __result,
			Exception? __exception)
		{
			if (__exception == null)
			{
				return null;
			}

			if (__exception is not InvalidCastException)
			{
				return __exception;
			}

			__result = CreateFallbackScoreLine(label, score, icon);
			if (HextechRunLogBudget.TryConsume("compat.game-over-score-line-fallback", 5))
			{
				Log.Warn($"[{ModInfo.Id}][Mayhem] Game over score line fallback used: {__exception.GetType().Name}: {__exception.Message}");
			}

			return null;
		}
	}
}
