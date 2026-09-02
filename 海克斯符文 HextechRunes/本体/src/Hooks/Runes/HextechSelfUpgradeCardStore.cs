using System.Runtime.CompilerServices;
using HarmonyLib;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 「升级：播种/收割/铁斩波」系列符文的共用持久化层:让某一张卡每次被打出后,在本局游戏里永久提升
/// 自己的伤害(以及铁斩波的格挡),且是**逐张卡实例**累加——同名的另一张卡各自独立计数。
///
/// 实现要点(对齐原版 <c>GeneticAlgorithm</c> 的"打出后永久自增"做法):
///  - 累加值挂在**牌库本体**(<see cref="CardModel.DeckVersion"/>)上,因为战斗里手牌是它的克隆副本、战斗结束即弃,
///    只有牌库本体会随存档持久。打出时同时给战斗副本和牌库本体的 <c>DynamicVars</c> 基础值加成,所以本场立刻可见、
///    之后每场新克隆也继承。
///  - 原版卡的 <c>DynamicVars</c> 数值**不进存档**(<see cref="SerializableCard"/> 只存 id/升级等级/Props 等),
///    且这几张是 sealed 原版卡、无法加 <c>[SavedProperty]</c> 属性。于是仿照本模组 ThoughtOverwrite 的做法,在
///    <see cref="CardModel.ToSerializable"/> 后缀把每张卡累计的加成写进它的 <see cref="SerializableCard.Props"/>,
///    在 <see cref="CardModel.FromSerializable"/> 后缀读回并重新加到 <c>BaseValue</c> 上,实现存档持久。
///  - Props 里用到的两个键名通过 <see cref="SelfUpgradeOnPlayRuneBase{TCard}"/> 上的哑 <c>[SavedProperty]</c>
///    属性注册进 <c>SavedPropertiesTypeCache</c>(符文是 AbstractModel,经 InjectModelType 自动注册、被 BaseLib 支持),
///    保证联机下属性名能正确编号。
/// </summary>
internal static class HextechSelfUpgradeCardStore
{
	internal const string DamageBonusSavedPropertyName = "HextechSelfUpgradeDamageBonus";
	internal const string BlockBonusSavedPropertyName = "HextechSelfUpgradeBlockBonus";

	private const string DamageVarName = "Damage";
	private const string BlockVarName = "Block";

	private sealed class Counters
	{
		public int Damage;
		public int Block;
	}

	// 以牌库本体(持久卡)为键累加;战斗克隆副本不计入,弃用后随 GC 回收。
	private static readonly ConditionalWeakTable<CardModel, Counters> BonusByCard = new();

	// 原版 DowngradeInternal(魔法骑士降级等)会用 canonical 克隆整体替换 _dynamicVars,
	// 把本 store 加在 BaseValue 上的累计值清零(玩家实报:升级铁斩波被降级后还原成 5/5,
	// 下场战斗从牌库本体重新克隆才恢复)。降级只应回退"升级",不应吞掉打出累计,这里把记账值补回去。

	/// <summary>打出后给这张卡永久增加 <paramref name="amount"/> 点伤害白值(本局持久、逐实例)。</summary>
	public static void AddDamageOnPlay(CardModel? playedCard, int amount)
	{
		if (playedCard == null || amount == 0)
		{
			return;
		}

		CardModel persistent = playedCard.DeckVersion ?? playedCard;
		BonusByCard.GetValue(persistent, static _ => new Counters()).Damage += amount;
		ApplyDamage(persistent, amount);
		if (!ReferenceEquals(persistent, playedCard))
		{
			ApplyDamage(playedCard, amount);
		}
	}

	/// <summary>打出后给这张卡永久增加 <paramref name="amount"/> 点格挡白值(本局持久、逐实例)。</summary>
	public static void AddBlockOnPlay(CardModel? playedCard, int amount)
	{
		if (playedCard == null || amount == 0)
		{
			return;
		}

		CardModel persistent = playedCard.DeckVersion ?? playedCard;
		BonusByCard.GetValue(persistent, static _ => new Counters()).Block += amount;
		ApplyBlock(persistent, amount);
		if (!ReferenceEquals(persistent, playedCard))
		{
			ApplyBlock(playedCard, amount);
		}
	}

	private static void ApplyDamage(CardModel card, int amount)
	{
		if (card.DynamicVars.ContainsKey(DamageVarName))
		{
			card.DynamicVars.Damage.BaseValue += amount;
		}
	}

	private static void ApplyBlock(CardModel card, int amount)
	{
		if (card.DynamicVars.ContainsKey(BlockVarName))
		{
			card.DynamicVars.Block.BaseValue += amount;
		}
	}


	private static void SetInt(SerializableCard card, string name, int value)
	{
		card.Props ??= new SavedProperties();
		card.Props.ints ??= new List<SavedProperties.SavedProperty<int>>();
		card.Props.ints.RemoveAll(property => property.name == name);
		card.Props.ints.Add(new SavedProperties.SavedProperty<int>(name, value));
	}

	private static int GetInt(SavedProperties? props, string name)
	{
		if (props?.ints == null)
		{
			return 0;
		}

		foreach (SavedProperties.SavedProperty<int> property in props.ints)
		{
			if (property.name == name)
			{
				return property.value;
			}
		}

		return 0;
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.ToSerializable), new Type[0])]
	[HextechPatch("card.self-upgrade.save", "自升级卡牌存储")]
	private static class ToSerializablePatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel __instance, SerializableCard __result)
		{
			if (!BonusByCard.TryGetValue(__instance, out Counters? counters))
			{
				return;
			}

			if (counters.Damage != 0)
			{
				SetInt(__result, DamageBonusSavedPropertyName, counters.Damage);
			}

			if (counters.Block != 0)
			{
				SetInt(__result, BlockBonusSavedPropertyName, counters.Block);
			}
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.FromSerializable), typeof(SerializableCard))]
	[HextechPatch("card.self-upgrade.load", "自升级卡牌存储")]
	private static class FromSerializablePatch
	{
		[HarmonyPostfix]
		private static void Postfix(SerializableCard save, CardModel __result)
		{
			int damage = GetInt(save.Props, DamageBonusSavedPropertyName);
			int block = GetInt(save.Props, BlockBonusSavedPropertyName);
			if (damage == 0 && block == 0)
			{
				return;
			}

			Counters counters = BonusByCard.GetValue(__result, static _ => new Counters());
			counters.Damage = damage;
			counters.Block = block;
			if (damage != 0)
			{
				ApplyDamage(__result, damage);
			}

			if (block != 0)
			{
				ApplyBlock(__result, block);
			}
		}
	}

	[HarmonyPatch(typeof(CardModel), nameof(CardModel.DowngradeInternal), new Type[0])]
	[HextechPatch("card.self-upgrade.downgrade", "自升级卡牌存储")]
	private static class DowngradePatch
	{
		[HarmonyPostfix]
		private static void Postfix(CardModel __instance)
		{
			CardModel persistent = __instance.DeckVersion ?? __instance;
			if (!BonusByCard.TryGetValue(persistent, out Counters? counters))
			{
				return;
			}

			if (counters.Damage != 0)
			{
				ApplyDamage(__instance, counters.Damage);
			}

			if (counters.Block != 0)
			{
				ApplyBlock(__instance, counters.Block);
			}
		}
	}
}
