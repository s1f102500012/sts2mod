
namespace IntegratedStrategyEvents.Events;

public sealed partial class LostMountainsEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"lost_mountains.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardMediumNarrowSlightlyShiftedRight);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"不见群山",
				new EventPageLoc(
					InitialPage,
					"在你将要[red]踏错[/red]之际，有人拉住了你。[aqua]群山的阴影[/aqua]交叠，[jitter][purple]扭曲的黑暗[/purple][/jitter]从你们眼前爬过。面对你的惶惑，[pink]占卜者[/pink]模样的女性请你抽一张[gold]牌[/gold]。\n\n——[purple]奇怪[/purple]。据你所知，[orange]萨米人[/orange]与[red]萨卡兹独眼巨人[/red]，都不以[pink]塔罗[/pink]作为预知未来的途径。",
					new EventOptionLoc("LEFT_CARD", "选择左边的塔罗牌", "获得[green]15[/green]点最大生命。获得[gold]王室猛毒[/gold]。"),
					new EventOptionLoc("RIGHT_CARD", "选择右边的塔罗牌", "失去[red]6[/red]点最大生命。获得[gold]小血瓶[/gold]。"),
					new EventOptionLoc("RIGHT_CARD_LOCKED", "选择右边的塔罗牌", "需要至少[red]7[/red]点最大生命。"),
					new EventOptionLoc("DECLINE", "不做选择", "也许这一切还是留给她自己更好。")),
				new EventPageLoc(
					"LEFT_CARD",
					"“别害怕自己的[pink]命运[/pink]......也别害怕背离自己的[pink]命运[/pink]。\n\n“[sine][pink]预言[/pink][/sine]啊，你与它重逢时，就会恍然大悟，明白它始终在未来等待着你。”\n\n你看向她赠予的[gold]水晶球[/gold]。可是，里面只有一片[aqua]黑色的群山[/aqua]，清晰地凝固下来，就好像......[sine][purple]不是在诉说你的命运[/purple][/sine]。"),
				new EventPageLoc(
					"RIGHT_CARD",
					"“放心吧，他留下的这件东西[green]没有被污染[/green]。\n\n“他是个带着许多人出生入死的[orange]萨满[/orange]，直到最后他自己也‘[purple]起死回生[/purple]’......那真是让人[jitter][red]恐惧[/red][/jitter]的回忆啊。\n\n“为了从[purple]巨大的阴影[/purple]中夺回他，我从此在[aqua]群山[/aqua]间奔走流浪。”"),
				new EventPageLoc(
					"DECLINE",
					"“愿我们的[pink]命运[/pink]还能再次交汇......”\n\n她与你告别，走入了[sine][aqua]风雪[/aqua][/sine]之中。")),
			new EventLoc(
				"Lost Mountains",
				new EventPageLoc(
					InitialPage,
					"Just before you step wrong, someone catches you. [aqua]The shadows of mountains[/aqua] overlap, and [jitter][purple]twisted darkness[/purple][/jitter] crawls before your eyes. Facing your confusion, a woman dressed as a [pink]fortune teller[/pink] asks you to draw a [gold]card[/gold].\n\nHow [purple]strange[/purple]. As far as you know, neither the [orange]Sami[/orange] nor the [red]Sarkaz Cyclopes[/red] use [pink]tarot[/pink] to foretell the future.",
					new EventOptionLoc("LEFT_CARD", "Choose the left tarot card", "Gain [green]15[/green] Max HP. Gain [gold]Royal Poison[/gold]."),
					new EventOptionLoc("RIGHT_CARD", "Choose the right tarot card", "Lose [red]6[/red] Max HP. Gain [gold]Blood Vial[/gold]."),
					new EventOptionLoc("RIGHT_CARD_LOCKED", "Choose the right tarot card", "Requires at least [red]7[/red] Max HP."),
					new EventOptionLoc("DECLINE", "Make no choice", "Perhaps this should be left to her.")),
				new EventPageLoc(
					"LEFT_CARD",
					"\"Do not fear your [pink]fate[/pink]... and do not fear turning away from your [pink]fate[/pink].\n\n\"When you reunite with [sine][pink]prophecy[/pink][/sine], realization will come. You will understand that it has always been waiting for you in the future.\"\n\nYou look into the [gold]crystal ball[/gold] she gave you. Yet inside, there is only a stretch of [aqua]black mountains[/aqua], frozen in perfect clarity, as if... [sine][purple]it is not speaking of your fate[/purple][/sine]."),
				new EventPageLoc(
					"RIGHT_CARD",
					"\"Do not worry. The thing he left behind [green]was not contaminated[/green].\n\n\"He was a [orange]shaman[/orange] who led many through life and death, until even he finally '[purple]came back to life[/purple]'... What a [jitter][red]terrifying[/red][/jitter] memory.\n\n\"To take him back from the [purple]enormous shadow[/purple], I have wandered among the [aqua]mountains[/aqua] ever since.\""),
				new EventPageLoc(
					"DECLINE",
					"\"May our [pink]fates[/pink] cross again...\"\n\nShe bids you farewell and walks into the [sine][aqua]snowstorm[/aqua][/sine]."))
		);
	}
}
