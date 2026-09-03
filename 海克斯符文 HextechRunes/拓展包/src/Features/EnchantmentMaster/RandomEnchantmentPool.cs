using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace HextechRunesSponsorPack;

// 附魔大师的候选池:ModelDb 里的全部附魔——原版的、本包的(进化 / 熵增)、其他模组的——只按下面的规则排除
// 弃用、负面与「模组内部伴随附魔」。拓展包不实现任何附魔机制,只是 CardCmd.Enchant 的一个普通调用方:
// 「合法」的定义完全由 enchantment.CanEnchant(card) 给出——没装多重附魔类模组时它只放行空槽位与
// 原版 IsStackable 的同类叠层;装了这类模组时由它们放宽规则。本文件不引用、也不探测任何第三方程序集。
//
// 池按 Id.Entry 的 Ordinal 排序后缓存一次(只读缓存,不是可变静态状态),保证两个联机客户端遍历顺序一致。
internal static class RandomEnchantmentPool
{
	// MultiEnchantmentMod 的标记附魔基类。按 FullName 字符串比对,不引用该程序集、也不判断它是否在场。
	private const string MultiEnchantmentMarkerTypeName = "MultiEnchantmentMod.Api.MarkerEnchantmentModel";

	// PengoTarot 等模组的伴随附魔命名约定(它们自己也按名字判定),这类附魔不该被随机抽到。
	private const string SubEnchantmentTypeNameSuffix = "SubEnchantment";

	private static readonly Lazy<IReadOnlyList<EnchantmentModel>> LazyPool =
		new(BuildPool, LazyThreadSafetyMode.ExecutionAndPublication);

	// 第三方附魔的 CanEnchant 覆写在 canonical 实例上抛异常时,把它当作「这张牌不合法」,每种类型只报一次。
	// 抛不抛只取决于代码与卡面状态,两个联机客户端的模组集合一致时结论相同,不引入分叉。
	private static readonly HashSet<Type> LoggedCanEnchantFailures = [];

	/// <summary>
	/// 这张牌当前可以合法附上的全部附魔(canonical 实例),保持池的 Id.Entry 顺序。
	/// </summary>
	internal static IReadOnlyList<EnchantmentModel> GetLegalEnchantments(CardModel card)
	{
		return GetLegalEnchantments(card, LazyPool.Value);
	}

	// 纯函数版本:池由调用方给出,便于单元测试(构建真实池需要 ModelDb / Godot 资源层)。
	internal static IReadOnlyList<EnchantmentModel> GetLegalEnchantments(CardModel card, IReadOnlyList<EnchantmentModel> pool)
	{
		List<EnchantmentModel> legal = [];
		foreach (EnchantmentModel enchantment in pool)
		{
			if (CanEnchantSafely(enchantment, card))
			{
				legal.Add(enchantment);
			}
		}

		return legal;
	}

	private static bool CanEnchantSafely(EnchantmentModel enchantment, CardModel card)
	{
		try
		{
			return enchantment.CanEnchant(card);
		}
		catch (Exception ex)
		{
			Type type = enchantment.GetType();
			lock (LoggedCanEnchantFailures)
			{
				if (LoggedCanEnchantFailures.Add(type))
				{
					Log.Warn($"[{ModInfo.Id}] EnchantmentMaster: {type.FullName}.CanEnchant threw on a canonical instance; treating it as illegal: {ex.GetType().Name}: {ex.Message}", 2);
				}
			}

			return false;
		}
	}

	internal static List<T> SortByEntryOrdinal<T>(IEnumerable<T> items, Func<T, string?> entrySelector)
	{
		return items.OrderBy(entrySelector, StringComparer.Ordinal).ToList();
	}

	/// <summary>
	/// 类型层面的排除规则(纯函数,不触碰 Godot 资源层)。
	/// </summary>
	internal static bool IsExcludedType(Type type)
	{
		// 负面附魔:熵减打出后会在战斗结束时把牌移出牌组,随机塞给玩家是惩罚,与原版 Corrupted 同类。
		if (type == typeof(DeprecatedEnchantment)
			|| type == typeof(Corrupted)
			|| type == typeof(Clone)
			|| type == typeof(EntropyDecrease)
			|| type == typeof(SponsorCompositeEnchantment))
		{
			return true;
		}

		if (type.Name.EndsWith(SubEnchantmentTypeNameSuffix, StringComparison.Ordinal))
		{
			return true;
		}

		for (Type? current = type; current != null; current = current.BaseType)
		{
			if (string.Equals(current.FullName, MultiEnchantmentMarkerTypeName, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 实例层面的排除规则 = 类型规则 + 「非本包附魔且没有图标」。
	/// 没有 res://images/enchantments/&lt;id&gt;.png 的第三方附魔多半是模组内部用的伴随附魔;
	/// 但本体 HextechRunesApi.RegisterEnchantmentIcon 只改 EnchantmentModel.Icon 不改 IconPath,
	/// 本包自己的附魔(进化 / 熵增)都走那条 API,IconPath 仍是 MissingIconPath——
	/// 所以图标规则不适用于本程序集,它们只受类型规则约束。
	/// </summary>
	internal static bool IsExcluded(EnchantmentModel enchantment)
	{
		Type type = enchantment.GetType();
		if (IsExcludedType(type))
		{
			return true;
		}

		if (type.Assembly == typeof(RandomEnchantmentPool).Assembly)
		{
			return false;
		}

		return string.Equals(enchantment.IconPath, EnchantmentModel.MissingIconPath, StringComparison.Ordinal);
	}

	private static IReadOnlyList<EnchantmentModel> BuildPool()
	{
		List<EnchantmentModel> eligible = [];
		List<string> excluded = [];
		foreach (EnchantmentModel enchantment in ModelDb.DebugEnchantments)
		{
			if (IsExcluded(enchantment))
			{
				excluded.Add(enchantment.Id.Entry);
			}
			else
			{
				eligible.Add(enchantment);
			}
		}

		List<EnchantmentModel> sorted = SortByEntryOrdinal(eligible, static enchantment => enchantment.Id.Entry);
		excluded.Sort(StringComparer.Ordinal);
		Log.Info($"[{ModInfo.Id}] EnchantmentMaster pool built: {sorted.Count} eligible, {excluded.Count} excluded ({string.Join(", ", excluded)}).");
		return sorted;
	}
}
