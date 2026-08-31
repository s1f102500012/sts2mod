using Godot;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private static void AttachRelicHoverTips(Control holder, RelicModel relic, MonsterHexKind? monsterHex = null)
	{
		holder.MouseFilter = MouseFilterEnum.Pass;
		holder.MouseDefaultCursorShape = CursorShape.Help;
		holder.MouseEntered += () => ShowRelicHoverTips(holder, relic, monsterHex);
		holder.MouseExited += () => NHoverTipSet.Remove(holder);
		holder.FocusEntered += () => ShowRelicHoverTips(holder, relic, monsterHex);
		holder.FocusExited += () => NHoverTipSet.Remove(holder);
		holder.TreeExiting += () => NHoverTipSet.Remove(holder);
	}

	private static void ShowRelicHoverTips(Control holder, RelicModel relic, MonsterHexKind? monsterHex = null)
	{
		NHoverTipSet.Remove(holder);
		IEnumerable<IHoverTip> hoverTips = monsterHex.HasValue
			? MonsterHexCatalog.GetEnemyHexHoverTips(monsterHex.Value)
			: relic.HoverTips;
		NHoverTipSet? hoverTipSet = NHoverTipSet.CreateAndShow(holder, hoverTips, HoverTip.GetHoverTipAlignment(holder));
		hoverTipSet?.SetAlignment(holder, HoverTip.GetHoverTipAlignment(holder));
	}
}
