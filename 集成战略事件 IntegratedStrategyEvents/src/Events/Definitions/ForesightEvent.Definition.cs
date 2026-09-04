
namespace IntegratedStrategyEvents.Events;

public sealed partial class ForesightEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"foresight.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"前瞻",
				new EventPageLoc(
					InitialPage,
					"[gold]故事[/gold]的内容总在变化，它的[purple]不确定性[/purple]既是[green]甘露[/green]，也是[red]毒药[/red]。\n\n因此当我们沉浸于当下时，总得有人走在前面，去看那[pink]陌生的未来[/pink]。",
					new EventOptionLoc("DISPATCH", "派遣人员", "从你的[gold]牌组[/gold]中选择[blue]1[/blue]张牌移除。"),
					new EventOptionLoc("PRESENT", "留意当下", "获得[blue]75[/blue][gold]金币[/gold]。")),
				new EventPageLoc(
					"DISPATCH",
					"你选定的[orange]人员[/orange]很快就收拾完毕，消失在了[sine][aqua]视野[/aqua][/sine]中。"),
				new EventPageLoc(
					"PRESENT",
					"你相信自己已经能够从当下的[purple]变化[/purple]中辨认出未来的[pink]蛛丝马迹[/pink]。")),
			new EventLoc(
				"Foresight",
				new EventPageLoc(
					InitialPage,
					"The contents of a [gold]story[/gold] are always changing. Its [purple]uncertainty[/purple] is both [green]nectar[/green] and [red]poison[/red].\n\nSo when we immerse ourselves in the present, someone must walk ahead and look toward that [pink]unfamiliar future[/pink].",
					new EventOptionLoc("DISPATCH", "Dispatch personnel", "Choose [blue]1[/blue] card from your deck to remove."),
					new EventOptionLoc("PRESENT", "Mind the present", "Gain [blue]75[/blue] [gold]Gold[/gold].")),
				new EventPageLoc(
					"DISPATCH",
					"The [orange]personnel[/orange] you selected quickly finishes packing, then disappears from [sine][aqua]view[/aqua][/sine]."),
				new EventPageLoc(
					"PRESENT",
					"You believe you can already discern traces of the future from the [purple]changes[/purple] taking place now."))
		);
	}
}
