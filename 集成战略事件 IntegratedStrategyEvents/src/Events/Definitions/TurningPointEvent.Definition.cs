
namespace IntegratedStrategyEvents.Events;

public sealed partial class TurningPointEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"turning_point.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftCompact);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"转机",
				new EventPageLoc(
					InitialPage,
					"[aqua]懦弱者[/aqua]在[red]仇恨的时代[/red]中注定会被毁灭。血亲们被一个个[jitter][red]屠戮[/red][/jitter]，保护他的人也都已被杀死。敌人没有夺取他的生命，也只是想从那[purple]扭曲的戏谑[/purple]中得到快乐。\n\n而他所能做的，也只有紧紧怀抱与家人们的[pink]合照[/pink]。\n\n可在那之后呢，他还能做些什么吗？",
					new EventOptionLoc("STAND_UP", "放下合照，站起身来", "随机获得一张稀有攻击牌。"),
					new EventOptionLoc("LOOK_AGAIN", "再看一眼合照", "随机获得一张稀有技能牌。"),
					new EventOptionLoc("SURRENDER", "认命", "多一事不如少一事。")),
				new EventPageLoc(
					"STAND_UP",
					"有多少人因时代而[sine][aqua]屈服[/aqua][/sine]，又有多少人因时代而[jitter][red]改变[/red][/jitter]。他只是缺少[b]决心[/b]，而非欠缺力量与技巧。\n\n他接受了心中的[red]仇恨[/red]。如果只有[jitter][red]杀戮[/red][/jitter]才能为自己带来安宁，那就这样做吧。"),
				new EventPageLoc(
					"LOOK_AGAIN",
					"他本来只是想再看一眼自己的[pink]亲人[/pink]，却从相框中看到了其他的东西——那是他求而不得的，跨越种族的[sine][green]希望与安宁[/green][/sine]。\n\n当看到杀死自己血亲的敌人也在相框中时，他意识到了一件事：只要不以[red]仇恨[/red]为目的滥用暴力，或许，他可以通过争斗将这相框中的[green]希望[/green]变成现实。"),
				new EventPageLoc(
					"SURRENDER",
					"他只是个[aqua]普通人[/aqua]，终究没有勇气去反抗那些[jitter][red]暴力的化身[/red][/jitter]。\n\n不久之后，他便迎来了自己[b]悲惨的结局[/b]。但在那样一个[red]文明即暴力[/red]的时代，没人会为此哀悼。")),
			new EventLoc(
				"Turning Point",
				new EventPageLoc(
					InitialPage,
					"[aqua]Cowards[/aqua] are doomed to be destroyed in an age of [red]hatred[/red]. His blood relatives were [jitter][red]slaughtered[/red][/jitter] one by one, and everyone who protected him has been killed. His enemies spared his life only to savor their [purple]twisted mockery[/purple].\n\nAll he can do is clutch a [pink]family photo[/pink] tightly.\n\nBut after that, what can he still do?",
					new EventOptionLoc("STAND_UP", "Put down the photo and stand", "Gain a random Rare Attack card."),
					new EventOptionLoc("LOOK_AGAIN", "Look at the photo again", "Gain a random Rare Skill card."),
					new EventOptionLoc("SURRENDER", "Accept fate", "The fewer problems, the better.")),
				new EventPageLoc(
					"STAND_UP",
					"How many people [sine][aqua]submit[/aqua][/sine] to their era, and how many are [jitter][red]changed[/red][/jitter] by it? He lacked only [b]resolve[/b], not strength or skill.\n\nHe accepts the [red]hatred[/red] in his heart. If only [jitter][red]killing[/red][/jitter] can bring him peace, then so be it."),
				new EventPageLoc(
					"LOOK_AGAIN",
					"He only meant to look once more at his [pink]family[/pink], but he sees something else in the frame: the [sine][green]hope and peace[/green][/sine] across races that he had longed for.\n\nWhen he sees that the enemies who killed his family are also in the photo, he realizes something. As long as violence is not abused for [red]hatred[/red], perhaps he can turn the [green]hope[/green] in that frame into reality through struggle."),
				new EventPageLoc(
					"SURRENDER",
					"He is only an [aqua]ordinary person[/aqua], and in the end he has no courage to resist those [jitter][red]embodiments of violence[/red][/jitter].\n\nBefore long, he meets his [b]tragic end[/b]. In an age where [red]civilization is violence[/red], no one mourns him."))
		);
	}
}
