
namespace IntegratedStrategyEvents.Events;

public sealed partial class ReconstructionEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"reconstruction.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.StandardNarrow);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"重构",
				new EventPageLoc(
					InitialPage,
					"当你的队伍到达[orange]营地[/orange]时，其他相关准备都已就绪了，你将[gold]材料[/gold]交付给[green]凯尔希[/green]，她把材料[sine][aqua]解离[/aqua][/sine]后分发至各个部门。随着仪器发出[jitter][gold]轰鸣[/gold][/jitter]，科学家们各就各位，浩大的[gold]巨构修复工程[/gold]正式开启。\n\n在第一阶段，探索者们需要清理[purple]坍缩体[/purple]，彻底隔绝它们对[gold]环形巨构[/gold]的影响。",
					new EventOptionLoc("IGNORE_COLLAPSE_POLLUTION", "无视坍缩污染", "从你的牌组中选择[blue]2[/blue]张牌[purple]变化[/purple]。"),
					new EventOptionLoc("IGNORE_COLLAPSE_POLLUTION_LOCKED", "无视坍缩污染", "你没有足够可[purple]变化[/purple]的牌。"),
					new EventOptionLoc("OVERLOAD_DETECTORS", "过载使用探测仪器", "从你的牌组中选择[blue]2[/blue]张牌[red]移除[/red]。"),
					new EventOptionLoc("OVERLOAD_DETECTORS_LOCKED", "过载使用探测仪器", "你没有足够可[red]移除[/red]的牌。"),
					new EventOptionLoc("ENDURE", "忍耐", "用随机[green]药水[/green]将你的空药水栏位填满。")),
				new EventPageLoc(
					CollapseClearedPage,
					"消除[purple]坍缩体[/purple]的威胁后，我们需要调查[gold]巨构[/gold]与其附属建筑，用[green]泰拉[/green]的材料尽可能修复一切破损部位，同时启动当初用以应对[red]克雷松[/red]的[aqua]空间稳定装置[/aqua]，在[green]萨米雪祀[/green]的协助，以及对[orange]精灵[/orange]所拥有的[gold]先史知识[/gold]的解析应用下，应当能够彻底稳定此处[sine][aqua]失序的空间洪流[/aqua][/sine]，保证调查与修复工作不受干扰。",
					new EventOptionLoc("FORCE_REPAIR", "强行修复，无视空间撕裂", "获得[gold]律动残余[/gold]和[gold]钨合金棍[/gold]。"),
					new EventOptionLoc("BUY_MATERIALS", "斥资购买高精尖材料", "支付所有[gold]金币[/gold]，升级你牌组内的所有牌。")),
				new EventPageLoc(
					RepairedPage,
					"现场清理与建筑修复工作皆已完成。最后，我们需要破解建筑中各类[gold]操作装置[/gold]与[gold]巨构[/gold]本身的联系。[blue]哥伦比亚[/blue]的科研人员、[purple]莱塔尼亚[/purple]的术师、[green]萨米[/green]的萨满和几支精通巫术的[orange]萨卡兹[/orange]一起攻克这最后的难题。而工程人员需要解决的，则是整个巨构的[gold]供能问题[/gold]。",
					new EventOptionLoc("BRUTE_FORCE_POWER", "以最粗暴的方式解决供能", "获得[green]30[/green]点最大生命值。"),
					new EventOptionLoc("CAREFUL_HOPE", "心怀希望，步步为营", "获得[gold]蜥蜴尾巴[/gold]。")),
				new EventPageLoc(
					RestartedPage,
					"整个设施被修复一新，巨构[jitter][gold]重新启动[/gold][/jitter]。无根花园圃泛起点点[blue]星光[/blue]，圆环现出[sine][purple]幽深空洞[/purple][/sine]。门扉已然打开，静静等待着访客的到来。进入大门的物资、人员、预案都已准备完毕，而你将是第一个跨进它的人。")),
			new EventLoc(
				"Reconstruction",
				new EventPageLoc(
					InitialPage,
					"When your team reaches the [orange]camp[/orange], every other preparation is already complete. You hand the [gold]materials[/gold] to [green]Kal'tsit[/green], and she [sine][aqua]dissociates[/aqua][/sine] them before distributing them to each department. As instruments begin to [jitter][gold]roar[/gold][/jitter], the scientists take their stations, and the vast [gold]megastructure restoration project[/gold] begins.\n\nIn the first phase, the explorers must clear out the [purple]collapsed entities[/purple] and fully sever their influence on the [gold]ring-shaped megastructure[/gold].",
					new EventOptionLoc("IGNORE_COLLAPSE_POLLUTION", "Ignore collapse contamination", "Choose [blue]2[/blue] cards from your deck to [purple]Transform[/purple]."),
					new EventOptionLoc("IGNORE_COLLAPSE_POLLUTION_LOCKED", "Ignore collapse contamination", "You do not have enough cards that can be [purple]Transformed[/purple]."),
					new EventOptionLoc("OVERLOAD_DETECTORS", "Overload the detection instruments", "Choose [blue]2[/blue] cards from your deck to [red]Remove[/red]."),
					new EventOptionLoc("OVERLOAD_DETECTORS_LOCKED", "Overload the detection instruments", "You do not have enough cards that can be [red]Removed[/red]."),
					new EventOptionLoc("ENDURE", "Endure", "Fill your empty potion slots with random [green]Potions[/green].")),
				new EventPageLoc(
					CollapseClearedPage,
					"After eliminating the threat of the [purple]collapsed entities[/purple], we must survey the [gold]megastructure[/gold] and its attached buildings, repair every damaged section with [green]Terran[/green] materials where possible, and activate the [aqua]spatial stabilization device[/aqua] once meant to counter [red]Cresson[/red]. With help from the [green]Sami Snowpriests[/green] and the applied analysis of the [gold]prehistoric knowledge[/gold] held by the [orange]Elves[/orange], we should be able to fully stabilize the [sine][aqua]disordered spatial torrent[/aqua][/sine] here, ensuring that investigation and restoration can proceed without interference.",
					new EventOptionLoc("FORCE_REPAIR", "Force the repair and ignore spatial tears", "Gain [gold]Beating Remnant[/gold] and [gold]Tungsten Rod[/gold]."),
					new EventOptionLoc("BUY_MATERIALS", "Spend lavishly on advanced materials", "Spend all [gold]Gold[/gold]. Upgrade every card in your deck.")),
				new EventPageLoc(
					RepairedPage,
					"Site clearance and architectural restoration are complete. Finally, we must decipher the connection between the building's many [gold]control devices[/gold] and the [gold]megastructure[/gold] itself. Researchers from [blue]Columbia[/blue], casters from [purple]Leithanien[/purple], shamans from [green]Sami[/green], and several [orange]Sarkaz[/orange] groups skilled in witchcraft work together to solve this last problem. The engineers, meanwhile, must solve the [gold]power supply[/gold] for the entire megastructure.",
					new EventOptionLoc("BRUTE_FORCE_POWER", "Solve power supply the crudest way", "Gain [green]30[/green] Max HP."),
					new EventOptionLoc("CAREFUL_HOPE", "Hold hope and proceed step by step", "Gain [gold]Lizard Tail[/gold].")),
				new EventPageLoc(
					RestartedPage,
					"The entire facility has been restored. The megastructure [jitter][gold]restarts[/gold][/jitter]. Starlight glimmers across the rootless garden plot, and a [sine][purple]deep hollow[/purple][/sine] appears within the ring. The [gold]door[/gold] is open, waiting quietly for visitors. Supplies, personnel, and contingency plans for entering have all been prepared, and you will be the first to cross its threshold."))
		);
	}
}
