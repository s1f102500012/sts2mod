using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HextechRunesSponsorPack;

// 0.9.2 起附魔大师不再实现「多重附魔」机制(整套自建引擎已删除),改为战斗结束时给牌组里的随机牌
// 加一个随机的合法附魔。「合法」的定义只来自 enchantment.CanEnchant(card):没装多重附魔类模组时
// 只有空槽位与原版可叠层的同类会被放行,装了这类模组时由它们放宽——拓展包既不解释也不探测。
public sealed class EnchantmentMasterRune : HextechRelicBase
{
	private static readonly HashSet<Type> GoldEnchantmentForgeTypes =
	[
		typeof(GlamForge),
		typeof(EnchantmentForge),
		typeof(EntropyForge)
	];

	private static readonly HashSet<Type> PrismaticEnchantmentForgeTypes =
	[
		typeof(SpiralForge),
		typeof(ArcaneForge),
		typeof(EvolutionForge),
		typeof(MysticForge)
	];

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("PrismaticForgeCount", 1m),
		new DynamicVar("GoldForgeCount", 2m)
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Flash();
		await HextechRunesApi.ObtainRandomForges(
			Owner,
			HextechRarityTier.Prismatic,
			DynamicVars["PrismaticForgeCount"].IntValue,
			IsPrismaticEnchantmentForge,
			"enchantment-master-prismatic");
		await HextechRunesApi.ObtainRandomForges(
			Owner,
			HextechRarityTier.Gold,
			DynamicVars["GoldForgeCount"].IntValue,
			IsGoldEnchantmentForge,
			"enchantment-master-gold");
	}

	public override bool IsAvailableForPlayer(Player player)
	{
		return true;
	}

	// 联机:本方法在所有客户端上对称执行(Hook.AfterCombatVictory 对每个玩家的遗物都分发),
	// 不能加 IsLocalPlayer / IsMine 之类的本地分支。随机源是运行种子稳定哈希(盐里含层数,
	// 所以每场战斗不同),池按 Id.Entry 有序,两端结论一致。表现层(Flash/Preview)在状态写入之后。
	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (Owner == null || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		List<CardModel> candidates = [];
		List<IReadOnlyList<EnchantmentModel>> candidateOptions = [];
		foreach (CardModel card in Owner.Deck.Cards)
		{
			IReadOnlyList<EnchantmentModel> legal = RandomEnchantmentPool.GetLegalEnchantments(card);
			if (legal.Count > 0)
			{
				candidates.Add(card);
				candidateOptions.Add(legal);
			}
		}

		if (candidates.Count == 0)
		{
			return Task.CompletedTask;
		}

		string netId = Owner.NetId.ToString();
		int cardIndex = SponsorStableRandom.Roll(Owner, candidates.Count, "enchantment-master", netId, "card");
		CardModel target = candidates[cardIndex];
		IReadOnlyList<EnchantmentModel> options = candidateOptions[cardIndex];
		EnchantmentModel canonical = options[SponsorStableRandom.Roll(Owner, options.Count, "enchantment-master", netId, "enchant")];

		// 选完再核一次:CardCmd.Enchant 对不合法的组合会抛,这里宁可放弃本次触发也不打断战斗结算。
		if (!canonical.CanEnchant(target))
		{
			Log.Warn($"[{ModInfo.Id}] EnchantmentMaster: {canonical.Id.Entry} is no longer legal for {target.Id.Entry}; skipping this combat.", 2);
			return Task.CompletedTask;
		}

		CardCmd.Enchant(canonical.ToMutable(), target, 1m);
		Log.Info($"[{ModInfo.Id}] EnchantmentMaster enchanted {target.Id.Entry} with {canonical.Id.Entry}");
		Flash();
		CardCmd.Preview(target);
		return Task.CompletedTask;
	}

	private static bool IsGoldEnchantmentForge(Type forgeType)
	{
		return GoldEnchantmentForgeTypes.Contains(forgeType);
	}

	private static bool IsPrismaticEnchantmentForge(Type forgeType)
	{
		return PrismaticEnchantmentForgeTypes.Contains(forgeType);
	}
}
