using MegaCrit.Sts2.Core.Combat.History;

namespace HextechRunes;

/// <summary>
/// 升级:打击/防御 共用:战斗结束后按打出历史升级牌库本体的基础打击/防御。
/// 必须在 AfterCombatEnd 收集并结算——原版胜利流程在 Hook.AfterCombatVictory 之前就
/// History.Clear()(CombatManager 反编译取证),victory 钩子里已拿不到打出记录。
/// AfterCombatEnd 在两端确定性触发,升级结果经存档原生等级字段持久,联机安全。
/// </summary>
internal static class HextechStarterUpgradeHelper
{
	public static Task UpgradePlayedBasicCards(HextechRelicBase rune, Player? owner, CombatRoom room, Func<CardModel, bool> isTargetCard)
	{
		if (owner == null || owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		// 只在胜利时升级:战斗结束且场上没有存活敌人。
		if (room.CombatState == null || HextechCombatCreatureHelper.GetAliveEnemies(room.CombatState).Count > 0)
		{
			return Task.CompletedTask;
		}

		// 按牌库本体去重:同一张卡本场打出多次也只升 1 级;战斗克隆映射回 DeckVersion。
		HashSet<CardModel> deckCards = new();
		foreach (CardPlayFinishedEntry entry in CombatManager.Instance.History.Entries.OfType<CardPlayFinishedEntry>())
		{
			CardModel card = entry.CardPlay.Card;
			if (card.Owner?.NetId != owner.NetId || !isTargetCard(card))
			{
				continue;
			}

			CardModel deckCard = card.DeckVersion ?? card;
			// 只升仍在牌组里的(本场被转化/移除的不算);等级由存档原生字段持久。
			if (owner.RunState.ContainsCard(deckCard))
			{
				deckCards.Add(deckCard);
			}
		}

		if (deckCards.Count == 0)
		{
			return Task.CompletedTask;
		}

		rune.Flash();
		foreach (CardModel deckCard in deckCards)
		{
			CardCmd.Upgrade(deckCard);
		}

		return Task.CompletedTask;
	}
}
