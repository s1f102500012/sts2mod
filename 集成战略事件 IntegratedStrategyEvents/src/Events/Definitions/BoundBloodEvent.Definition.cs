
namespace IntegratedStrategyEvents.Events;

public sealed partial class BoundBloodEvent
{
	protected override IntegratedStrategyEventDefinition Definition { get; } =
		IntegratedStrategyEventDefinition.ForEventPortrait(
			"bound_blood.png",
			CreateLocalization,
			IntegratedStrategyEventLayoutProfile.LeftWide);

	internal static List<(string, string)>? CreateLocalization()
	{
		return IntegratedStrategyEventLocalization.ForCurrentLanguage(
			new EventLoc(
				"受缚之血",
				new EventPageLoc(
					InitialPage,
					"在一个[purple]阴暗的监牢[/purple]中，你看到几根[b]巨大的锁链[/b]将一个身材瘦小的少女束缚在石柱上。看到你的靠近，少女自称是[red]萨卡兹血魔王庭[/red]的成员。她向你祈求一点[red][jitter]血液[/jitter][/red]，这样就能让自己恢复力量，挣脱监牢的束缚。",
					new EventOptionLoc("HELP", "尝试去帮帮她", "这是我该做的！"),
					new EventOptionLoc("LEAVE", "别找麻烦！", "这个人被锁链拴住肯定是有原因的。")),
				new EventPageLoc(
					"HELP",
					"在一阵[jitter][red]轰鸣声[/red][/jitter]中，你看到少女扯断了粗壮的锁链，肆意狂笑着。她大声辱骂将她束缚在此处的[red]“剧团成员”[/red]，并且扬言要向每个人复仇。\n\n你看到她精致的五官突然[jitter][red]崩解成三瓣[/red][/jitter]，露出了形态可怖的尖牙利齿。随后监牢的阴影[sine][purple]凝聚成人型[/purple][/sine]，数个体态扭曲的生物向你袭来。",
					new EventOptionLoc("FIGHT", "进入战斗", "遭遇一场艰难的战斗。")),
				new EventPageLoc(
					"LEAVE",
					"看到你毫不关心地离去，锁链中的少女发出了[jitter][red]恐怖骇人[/red][/jitter]的尖啸。你庆幸自己没有多管闲事。")),
			new EventLoc(
				"Bound Blood",
				new EventPageLoc(
					InitialPage,
					"In a [purple]dark prison[/purple], massive [b]chains[/b] bind a slight girl to a stone pillar. As you approach, she claims to be a member of the [red]Sarkaz Blood Court[/red]. She begs for a little [red][jitter]blood[/jitter][/red], enough to restore her strength and break free.",
					new EventOptionLoc("HELP", "Try to help her", "This is the right thing to do!"),
					new EventOptionLoc("LEAVE", "Avoid trouble!", "Someone bound by chains must be here for a reason.")),
				new EventPageLoc(
					"HELP",
					"With a [jitter][red]thunderous crash[/red][/jitter], the girl tears through the heavy chains and laughs wildly. She curses the [red]\"troupe members\"[/red] who imprisoned her and vows revenge on everyone.\n\nHer delicate features suddenly [jitter][red]split into three[/red][/jitter], revealing terrible fangs. Then the prison shadows [sine][purple]condense into figures[/purple][/sine], and several twisted creatures rush toward you.",
					new EventOptionLoc("FIGHT", "Enter combat", "Encounter a difficult fight.")),
				new EventPageLoc(
					"LEAVE",
					"As you leave without concern, the chained girl releases a [jitter][red]horrifying[/red][/jitter] scream. You are glad you did not get involved."))
		);
	}
}
