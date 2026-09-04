namespace IntegratedStrategyEvents.Events;

public sealed partial class LiberationEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"liberation.png",
			CreateLocalization,
			Layout: IntegratedStrategyEventLayoutProfile.LeftWide,
			AlignHoverTipsRight: true);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"解脱？",
				new EventPageLoc(
					InitialPage,
					"你来到一个大厅，大厅最显眼的位置挂着一幅[gold]油画[/gold]，上面描绘着某个[purple]悲剧故事[/purple]的[red]结局一幕[/red]。\n\n主人公在故事的最后，才发现自己一直活在[jitter][purple]他人的阴谋[/purple][/jitter]之中，他在[red]悲痛[/red]之中[jitter][red]跳下高塔[/red][/jitter]，试图摆脱一切[sine][purple]操纵与掌控[/purple][/sine]。",
					new EventOptionLoc("OBSERVE", "太可怜了，仔细观赏", "走上前去。"),
					new EventOptionLoc("STOP_READING", "少看点悲剧故事吧！", "获得[green]8[/green]点最大生命。")),
				new EventPageLoc(
					"OBSERVE",
					"你走上前去仔细查看[gold]油画[/gold]，一个[gold]玩偶[/gold]忽然从上方掉落，在你面前[jitter][red]摔得四分五裂[/red][/jitter]。",
					new EventOptionLoc("PICK_UP", "捡起玩偶", "获得[gold]残破的玩偶[/gold]。")),
				new EventPageLoc(
					"PICK_UP",
					"看起来真惨！带上它吧。"),
				new EventPageLoc(
					"STOP_READING",
					"现实已经这么[purple]难过[/purple]，故事里为什么还不让自己[green]高兴[/green]点呢？")),
			new EventLoc(
				"Liberation?",
				new EventPageLoc(
					InitialPage,
					"You arrive in a grand hall. In its most prominent spot hangs an [gold]oil painting[/gold], depicting the [red]final scene[/red] of some [purple]tragic story[/purple].\n\nOnly at the very end did the protagonist realize he had been living inside [jitter][purple]someone else's scheme[/purple][/jitter] all along. In his [red]grief[/red] he [jitter][red]leapt from the high tower[/red][/jitter], trying to break free of all [sine][purple]manipulation and control[/purple][/sine].",
					new EventOptionLoc("OBSERVE", "How pitiful. Take a closer look", "Step forward."),
					new EventOptionLoc("STOP_READING", "Read fewer tragedies!", "Gain [green]8[/green] Max HP.")),
				new EventPageLoc(
					"OBSERVE",
					"You step forward to examine the [gold]painting[/gold]. A [gold]doll[/gold] suddenly drops from above and [jitter][red]shatters to pieces[/red][/jitter] before you.",
					new EventOptionLoc("PICK_UP", "Pick up the doll", "Gain the [gold]Tattered Doll[/gold].")),
				new EventPageLoc(
					"PICK_UP",
					"What a sorry sight! Take it with you."),
				new EventPageLoc(
					"STOP_READING",
					"Reality is [purple]hard enough[/purple] already. Why not let yourself be [green]happy[/green] in stories?"))
		);
	}
}
