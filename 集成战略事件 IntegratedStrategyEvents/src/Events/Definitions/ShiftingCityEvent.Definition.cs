
namespace IntegratedStrategyEvents.Events;

public sealed partial class ShiftingCityEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"shifting_city.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"变幻之城",
				new EventPageLoc(
					InitialPage,
					"卡兹戴尔的[orange]城市结构[/orange]并不遵循常理，道路随着[sine][purple]直觉[/purple][/sine]延伸，景色伴随潜意识转换，行走这一动作在[aqua]时空[/aqua]中更像是一种[gold]思考[/gold]。\n\n因此，身处其间的人时常会[jitter][purple]迷失[/purple][/jitter]，而这座[orange]城市本身[/orange]，正[sine][orange]兴致盎然[/orange][/sine]地看着无数茫然的旅人。",
					new EventOptionLoc("UNDERSTAND_CITY", "尝试理解城市", "随机获得[blue]2[/blue]张牌。"),
					new EventOptionLoc("PLEASE_CITY", "尝试讨好城市", "随机获得[blue]1[/blue]张牌，获得[gold]100[/gold]金币。"),
					new EventOptionLoc("ASK_EARTHSPIRIT", "求助土石之子", "离开。")),
				new EventPageLoc(
					"UNDERSTAND_CITY",
					"你用自己的构想拆解了这座城市的[sine][purple]循环逻辑[/purple][/sine]——在可认知的空间中，创造性被压缩凝结成具象的[orange]砖瓦[/orange]铺满城市。\n\n每一丝念想都会催动它的变化，因此任何改变都会[jitter][purple]彻底重塑[/purple][/jitter]城市的面貌。当你将这份理解运用于眼前的道路时，卡兹戴尔将你带去了一个[gold]熟悉的地方[/gold]——",
					new EventOptionLoc("ENTER_FRAGMENT", "我回去了？", "进入[sine][purple]诡谲断章[/purple][/sine]。")),
				new EventPageLoc(
					"PLEASE_CITY",
					"在将自己的[gold]构想[/gold]献给城市后没多久，你便找到了一些资源。\n\n这究竟是前人探索时留下的[gold]遗产[/gold]，还是这座城市本身的......“[orange]赏赐[/orange]”？"),
				new EventPageLoc(
					"ASK_EARTHSPIRIT",
					"[orange]土石之子[/orange]建造了这座城市，自然也对城市的结构一清二楚。\n\n在她的带领下，你很快就从这里脱身了。")),
			new EventLoc(
				"Shifting City",
				new EventPageLoc(
					InitialPage,
					"The [orange]urban structure[/orange] of Kazdel does not obey common sense. Roads extend with [sine][purple]intuitions[/purple][/sine], scenery changes with the subconscious, and the act of walking feels less like movement through [aqua]spacetime[/aqua] than a form of [gold]thought[/gold].\n\nThose who find themselves within it often become [jitter][purple]lost[/purple][/jitter], while the [orange]city itself[/orange] watches countless bewildered travelers with [sine][orange]keen interest[/orange][/sine].",
					new EventOptionLoc("UNDERSTAND_CITY", "Try to understand the city", "Gain [blue]2[/blue] random cards."),
					new EventOptionLoc("PLEASE_CITY", "Try to please the city", "Gain [blue]1[/blue] random card. Gain [gold]100[/gold] Gold."),
					new EventOptionLoc("ASK_EARTHSPIRIT", "Ask Earthspirit for help", "Leave.")),
				new EventPageLoc(
					"UNDERSTAND_CITY",
					"You break down the city's [sine][purple]cyclical logic[/purple][/sine] with your own conception: within knowable space, creativity is compressed and condensed into tangible [orange]bricks and tiles[/orange] that pave the city.\n\nEvery thought spurs its transformation, so any change will [jitter][purple]completely reshape[/purple][/jitter] the city's appearance. When you apply this understanding to the road before you, Kazdel carries you to a [gold]familiar place[/gold]...",
					new EventOptionLoc("ENTER_FRAGMENT", "I went back?", "Enter the [sine][purple]Eerie Fragment[/purple][/sine].")),
				new EventPageLoc(
					"PLEASE_CITY",
					"Not long after offering your [gold]conception[/gold] to the city, you find a cache of resources.\n\nIs this [gold]inheritance[/gold] left behind by previous explorers, or a... \"[orange]reward[/orange]\" from the city itself?"),
				new EventPageLoc(
					"ASK_EARTHSPIRIT",
					"[orange]Earthspirit[/orange] built this city, so naturally she knows its structure inside and out.\n\nWith her guidance, you soon escape from this place."))
		);
	}
}
