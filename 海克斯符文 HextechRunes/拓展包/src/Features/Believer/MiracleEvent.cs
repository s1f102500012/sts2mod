using HextechRunes;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunesSponsorPack;

// 「神迹」事件:信徒海克斯触发。三级分支(大类→献祭/索取→强度1/2/3)。游戏原生 EventModel + SetEventState 多屏导航,
// 自动被 ModelDb 发现、不进随机事件池;立绘/锻造器售价由对应 Harmony 补丁处理。纯拓展包,不动主 mod。
//
// 金币消耗用 PlayerCmd.LoseGold(GainGold 负数不扣);金币不足/无对应稀有度卡时把选项锁定(onChosen=null)。
public sealed class MiracleEvent : EventModel
{
	private const string InitialPage = "INITIAL";
	private const string EventLocTable = "events";
	private const string PortraitPath = "res://images/events/doors_of_light_and_dark.png";

	public override IEnumerable<string> GetAssetPaths(IRunState runState)
	{
		yield return PortraitPath;
	}

	protected override IReadOnlyList<EventOption> GenerateInitialOptions()
	{
		return
		[
			Choice(InitialPage, "FORGE", () => Show("FORGE_ROOT", ActionOptions("FORGE"))),
			Choice(InitialPage, "GOLD", () => Show("GOLD_ROOT", ActionOptions("GOLD"))),
			Choice(InitialPage, "CARD", () => Show("CARD_ROOT", ActionOptions("CARD"))),
			Choice(InitialPage, "GIFT", () => Show("GIFT_ROOT", GiftRootOptions())),
		];
	}

	private IReadOnlyList<EventOption> ActionOptions(string category)
	{
		string root = $"{category}_ROOT";
		return
		[
			Choice(root, "SACRIFICE", () => Show($"{category}_SACRIFICE", StrengthOptions(category, sacrifice: true))),
			Choice(root, "TAKE", () => Show($"{category}_TAKE", StrengthOptions(category, sacrifice: false))),
			Back(ShowInitial),
		];
	}

	private IReadOnlyList<EventOption> GiftRootOptions()
	{
		return
		[
			Choice("GIFT_ROOT", "SACRIFICE", () => Show("GIFT_SACRIFICE", StrengthOptions("GIFT", sacrifice: true))),
			Choice("GIFT_ROOT", "TAKE", () => ApplyAndFinish("GIFT_TAKE", _ => GainGold(50))),
			Back(ShowInitial),
		];
	}

	// 三档强度选项;不满足条件(金币不足 / 无对应稀有度卡)的档位锁定(onChosen=null → IsLocked)。
	private IReadOnlyList<EventOption> StrengthOptions(string category, bool sacrifice)
	{
		string page = $"{category}_{(sacrifice ? "SACRIFICE" : "TAKE")}";
		List<EventOption> options = [];
		for (int tier = 1; tier <= 3; tier++)
		{
			int t = tier;
			Func<Task>? action = IsLeafAvailable(category, sacrifice, t)
				? () => ApplyAndFinish(page, owner => ApplyLeaf(category, sacrifice, t, owner))
				: null;
			options.Add(new EventOption(this, action, $"{Id.Entry}.pages.{page}.options.T{t}"));
		}

		options.Add(Back(() => ShowCategoryRoot(category)));
		return options;
	}

	private bool IsLeafAvailable(string category, bool sacrifice, int tier)
	{
		int gold = Owner?.Gold ?? 0;
		return category switch
		{
			"FORGE" => !sacrifice || gold >= 100 * tier,
			"GOLD" => !sacrifice || gold >= GoldAmount(tier),
			"CARD" => sacrifice ? DeckHasSacrificeCard(tier) : gold >= CardPackCost(tier),
			"GIFT" => !sacrifice || gold >= 25 * tier,
			_ => true,
		};
	}

	private async Task ApplyLeaf(string category, bool sacrifice, int tier, Player owner)
	{
		switch (category)
		{
			case "FORGE":
				await ApplyForge(owner, sacrifice, tier);
				break;
			case "GOLD":
				await ApplyGold(owner, sacrifice, tier);
				break;
			case "CARD":
				await ApplyCard(owner, sacrifice, tier);
				break;
			case "GIFT":
				await ApplyGiftLottery(owner, tier);
				break;
		}
	}

	// 锻造器:献祭 花 100*tier 金币换 tier 个锻造器;索取 直取 tier 个 + 本局售价 +50*tier。
	private async Task ApplyForge(Player owner, bool sacrifice, int tier)
	{
		if (sacrifice)
		{
			await SpendGold(100 * tier);
		}
		else
		{
			AddForgePriceDelta(50 * tier);
		}

		// 逐个发放,每个锻造器稀有度按 65/25/10(银/金/棱彩)加权随机,而非清一色白银。
		for (int i = 0; i < tier; i++)
		{
			await HextechRunesApi.ObtainRandomForges(
				owner, RandomForgeRarity(owner, $"forge:{tier}:{i}"), 1, static _ => true, $"{ModInfo.Id}.miracle.forge");
		}
	}

	// 金币:献祭 花 100/200/400 金币、本局售价 -25/-50/-100;索取 得 100/200/400 金币、本局售价 +25/+25/+100。
	private async Task ApplyGold(Player owner, bool sacrifice, int tier)
	{
		int amount = GoldAmount(tier);
		if (sacrifice)
		{
			AddForgePriceDelta(new[] { -25, -50, -100 }[tier - 1]);
			await SpendGold(amount);
		}
		else
		{
			AddForgePriceDelta(new[] { 25, 25, 100 }[tier - 1]);
			await GainGold(amount);
		}
	}

	// 卡牌:献祭 移除 1 张对应稀有度的牌(普通/罕见/稀有)+ 25/50/75 金币;索取 花 25/50/50 金币开 1 只卡包。
	private async Task ApplyCard(Player owner, bool sacrifice, int tier)
	{
		if (sacrifice)
		{
			List<CardModel> ofTier = owner.Deck.Cards.Where(card => MatchesSacrificeTier(card, tier)).ToList();
			if (ofTier.Count == 0)
			{
				return;
			}

			CardModel target = ofTier[StableRoll(owner, ofTier.Count, "miracle.sacrifice", tier.ToString())];
			await CardPileCmd.RemoveFromDeck(target);
			await GainGold(tier == 1 ? 25 : tier == 2 ? 50 : 75);
		}
		else
		{
			await SpendGold(CardPackCost(tier));
			await GrantCardPack(owner, tier);
		}
	}

	// 卡包(ScrollBoxes 式):把 3 张对应稀有度的牌装进 1 只卡包,玩家点开即获得这 3 张。
	private async Task GrantCardPack(Player owner, int tier)
	{
		CardRarity rarity = RarityForTier(tier);
		List<CardModel> candidates = owner.Character.CardPool.AllCards
			.Where(card => card.Rarity == rarity)
			.ToList();

		List<CardModel> pack = [];
		for (int i = 0; i < 3 && candidates.Count > 0; i++)
		{
			int idx = StableRoll(owner, candidates.Count, "miracle.cardpack", tier.ToString(), i.ToString());
			// 必须 CreateCard 从卡池模板实例化(canonical 不能直接用,否则 CanonicalModelException)。
			pack.Add(owner.RunState.CreateCard(candidates[idx], owner));
			candidates.RemoveAt(idx);
		}

		if (pack.Count == 0)
		{
			return;
		}

		List<IReadOnlyList<CardModel>> bundles = [ pack ];
		foreach (CardModel chosen in await CardSelectCmd.FromChooseABundleScreen(owner, bundles))
		{
			await CardPileCmd.Add(chosen, PileType.Deck);
		}
	}

	// 礼盒抽奖:花 25*tier 金币,抽 tier 次。奖池 10%锻造器 / 10%锻造器售价-25 / 10%删 1 张牌 / 70%金币。
	private async Task ApplyGiftLottery(Player owner, int tier)
	{
		await SpendGold(25 * tier);
		for (int i = 0; i < tier; i++)
		{
			int roll = StableRoll(owner, 100, "miracle.gift", tier.ToString(), i.ToString());
			if (roll < 10)
			{
				await HextechRunesApi.ObtainRandomForges(owner, RandomForgeRarity(owner, $"gift:{tier}:{i}"), 1, static _ => true, $"{ModInfo.Id}.miracle.gift.forge");
			}
			else if (roll < 20)
			{
				AddForgePriceDelta(-25);
			}
			else if (roll < 30)
			{
				List<CardModel> removed = (await CardSelectCmd.FromDeckForRemoval(owner, new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1))).ToList();
				if (removed.Count > 0)
				{
					await CardPileCmd.RemoveFromDeck(removed);
				}
			}
			else
			{
				await GainGold(1 + StableRoll(owner, 100, "miracle.gift.gold", tier.ToString(), i.ToString()));
			}
		}
	}

	// ---- 小工具 ----

	// 锻造器稀有度加权随机:65% 白银 / 25% 黄金 / 10% 棱彩(同默认锻造器权重)。
	private static HextechRarityTier RandomForgeRarity(Player owner, string salt)
	{
		int r = StableRoll(owner, 100, "miracle.forge.rarity", salt);
		return r < 65 ? HextechRarityTier.Silver : r < 90 ? HextechRarityTier.Gold : HextechRarityTier.Prismatic;
	}

	// 事件目前仅单机开放,但写游戏状态的随机一律走运行种子哈希而非 GD.Randi():
	// 避免日后放开联机或二创移植时留下双端分叉隐患(与主模组 HextechStableRandom 同款算法)。
	// 算法本体已抽到 SponsorStableRandom(附魔大师共用);盐格式与哈希逐位不变,历史结果不受影响。
	private static int StableRoll(Player owner, int count, params string?[] saltParts)
	{
		return SponsorStableRandom.Roll(owner, count, saltParts);
	}

	private static int GoldAmount(int tier) => tier == 3 ? 400 : 100 * tier;

	private static int CardPackCost(int tier) => tier == 1 ? 25 : 50;

	private static CardRarity RarityForTier(int tier) => tier == 1 ? CardRarity.Common : tier == 2 ? CardRarity.Uncommon : CardRarity.Rare;

	// 卡牌献祭按档匹配:普通档(T1)把初始/基础卡(打击、防御)也算进去;罕见(T2)、稀有(T3)。
	private static bool MatchesSacrificeTier(CardModel card, int tier) => tier switch
	{
		1 => card.Rarity is CardRarity.Common or CardRarity.Basic,
		2 => card.Rarity == CardRarity.Uncommon,
		_ => card.Rarity == CardRarity.Rare,
	};

	private bool DeckHasSacrificeCard(int tier) => Owner?.Deck.Cards.Any(card => MatchesSacrificeTier(card, tier)) ?? false;

	private Task GainGold(int amount) => Owner != null ? PlayerCmd.GainGold(amount, Owner) : Task.CompletedTask;

	private Task SpendGold(int amount) => Owner != null ? PlayerCmd.LoseGold(amount, Owner) : Task.CompletedTask;

	private void AddForgePriceDelta(int delta) => Owner?.Relics.OfType<BelieverRune>().FirstOrDefault()?.AddForgePriceDelta(delta);

	// ---- 多屏导航薄封装 ----

	private EventOption Choice(string pageKey, string optionKey, Func<Task> onChosen)
	{
		return new EventOption(this, onChosen, $"{Id.Entry}.pages.{pageKey}.options.{optionKey}");
	}

	// 「返回上一级」选项(每个子页都有,始终可选)。防止进入某分支后所有档位都因条件不满足被锁 → 无可选项 → 死档。
	// 用共享本地化键 common.BACK(EventOption 自动补 .title/.description)。
	private EventOption Back(Func<Task> onBack)
	{
		return new EventOption(this, onBack, $"{Id.Entry}.common.BACK");
	}

	private Task ShowInitial() => Show(InitialPage, GenerateInitialOptions());

	private Task ShowCategoryRoot(string category)
	{
		return category == "GIFT"
			? Show("GIFT_ROOT", GiftRootOptions())
			: Show($"{category}_ROOT", ActionOptions(category));
	}

	private Task Show(string pageKey, IReadOnlyList<EventOption> options)
	{
		SetEventState(new LocString(EventLocTable, $"{Id.Entry}.pages.{pageKey}.description"), options);
		return Task.CompletedTask;
	}

	private async Task ApplyAndFinish(string pageKey, Func<Player, Task> effect)
	{
		if (Owner != null)
		{
			await effect(Owner);
		}

		SetEventFinished(new LocString(EventLocTable, $"{Id.Entry}.pages.{pageKey}.resolution"));
	}
}
