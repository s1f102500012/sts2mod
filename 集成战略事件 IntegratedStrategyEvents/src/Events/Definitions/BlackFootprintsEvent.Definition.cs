
namespace IntegratedStrategyEvents.Events;

public sealed partial class BlackFootprintsEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } = IntegratedStrategyEventDefinition.ForEventPortrait("black_footprints.png", CreateLocalization);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"黑色足迹",
				new EventPageLoc(
					InitialPage,
					"你的牙齿在[jitter][aqua]打架[/aqua][/jitter]。死去的兽，无数死去的兽，[purple]污浊的雪[/purple]盖在它们身上。直觉告诉你，绝不能再往这个方向前进，可你无法从[purple]恐惧的泥沼[/purple]中拔出双腿。\n\n你不知道自己站了多久，直到[orange]魁梧的当地战士[/orange]围住了你。",
					new EventOptionLoc("STAND_OFF", "沉默对峙", "恐慌时言多必失。"),
					new EventOptionLoc("ESCAPE", "逃离", "失去[red]8[/red]点生命。"),
					new EventOptionLoc("ESCAPE_LOCKED", "逃离", "需要至少[red]9[/red]点生命。")),
				new EventPageLoc(
					"STAND_OFF",
					"你被当成了[red]盗猎者[/red]，你携带的[aqua]设备[/aqua]和[aqua]仪器[/aqua]在这些[orange]萨米人[/orange]眼中无比可疑。\n\n他们究竟经历了什么，才会以[red]暴力[/red]对待一切外来者？此时你已经无暇思考。",
					new EventOptionLoc("FIGHT", "保护自己", "遭遇一场艰难的战斗。")),
				new EventPageLoc(
					"ESCAPE",
					"[jitter][red]逃跑，逃跑，逃跑。[/red][/jitter]\n\n萨米人的[red]箭矢[/red]擦过你的身体，而你脑中的想法只有远离那片[aqua]雪地[/aqua]，逃往与那条[purple]污浊的痕迹[/purple]相反的方向。")),
			new EventLoc(
				"Black Footprints",
				new EventPageLoc(
					InitialPage,
					"Your teeth [jitter][aqua]chatter[/aqua][/jitter]. Dead beasts, countless dead beasts, covered by [purple]filthy snow[/purple]. Instinct tells you that you must not go farther in this direction, yet you cannot pull your legs free from the [purple]mire of fear[/purple].\n\nYou do not know how long you stand there before [orange]burly local warriors[/orange] surround you.",
					new EventOptionLoc("STAND_OFF", "Stand off in silence", "Too many words invite mistakes when panic takes hold."),
					new EventOptionLoc("ESCAPE", "Escape", "Lose [red]8[/red] HP."),
					new EventOptionLoc("ESCAPE_LOCKED", "Escape", "Requires at least [red]9[/red] HP.")),
				new EventPageLoc(
					"STAND_OFF",
					"You are mistaken for a [red]poacher[/red]. The [aqua]devices[/aqua] and [aqua]instruments[/aqua] you carry look deeply suspicious to these [orange]Sami[/orange].\n\nWhat have they endured, to answer every outsider with [red]violence[/red]? You have no time left to think.",
					new EventOptionLoc("FIGHT", "Protect yourself", "Encounter a difficult fight.")),
				new EventPageLoc(
					"ESCAPE",
					"[jitter][red]Run, run, run.[/red][/jitter]\n\nSami [red]arrows[/red] graze your body, and the only thought in your mind is to leave that [aqua]snowfield[/aqua] behind, fleeing in the opposite direction from that [purple]filthy trail[/purple]."))
		);
	}
}
