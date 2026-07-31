using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal static class HextechUiTheme
{
	internal static void ApplyDefaultMegaLabelTheme(MegaLabel label)
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
