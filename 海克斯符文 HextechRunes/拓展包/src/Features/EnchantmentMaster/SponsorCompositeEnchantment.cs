using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HextechRunesSponsorPack;

// 只读迁移壳:0.9.2 起附魔大师不再实现「多重附魔」,复合附魔不会再被创建。
// 本类仅为读取 0.9.1 及更早版本的存档保留一个版本周期(下一个版本删除),没有 Harmony、不注册图标、不进随机附魔池。
// 类名与 [SavedProperty] 属性名 SavedEnchantmentsJson 都不能改:它们决定 ModelId 与 net-id 布局。
public sealed class SponsorCompositeEnchantment : EnchantmentModel
{
	private string? _savedEnchantmentsJson;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private string? SavedEnchantmentsJson
	{
		get => _savedEnchantmentsJson;
		set => _savedEnchantmentsJson = value;
	}

	public override bool HasExtraCardText => false;

	public override bool CanEnchant(CardModel card)
	{
		return false;
	}

	// 旧存档里复合附魔占着 card.Enchantment 槽位。CardModel.FromSerializable 在 EnchantInternal 之后
	// 调 Enchantment.ModifyCard()(0.111 CardModel 第 2264-2266 行),ModifyCard 会走到这里,
	// 是把槽位换回单个原版附魔的唯一时机。
	protected override void OnEnchant()
	{
		if (string.IsNullOrWhiteSpace(_savedEnchantmentsJson) || !HasCard)
		{
			return;
		}

		CardModel card = Card;
		string payload = _savedEnchantmentsJson;
		_savedEnchantmentsJson = null;
		try
		{
			SerializableEnchantment[]? saved = JsonSerializer.Deserialize<SerializableEnchantment[]>(payload);
			if (saved is not { Length: > 0 })
			{
				return;
			}

			EnchantmentModel inner = EnchantmentModel.FromSerializable(saved[0]);
			card.ClearEnchantmentInternal();
			card.EnchantInternal(inner, inner.Amount);
			inner.ModifyCard();
			card.FinalizeUpgradeInternal();
			Log.Warn($"[{ModInfo.Id}] 旧版复合附魔已迁移为 {inner.Id.Entry},其余 {saved.Length - 1} 个附魔已丢失。", 2);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] 旧版复合附魔迁移失败,该牌的附魔已丢弃:{ex.GetType().Name}: {ex.Message}", 2);
		}
		finally
		{
			// ClearEnchantmentInternal 把本壳的 Card 置空,但外层 EnchantmentModel.ModifyCard 在 OnEnchant
			// 之后还要读 Card.DynamicVars(0.111 EnchantmentModel 第 359-366 行)。卡的附魔槽位此时已经是
			// 内层附魔,本壳只是个孤儿对象,重新挂回同一张卡只为让外层那两行不抛 NullReference。
			if (!HasCard)
			{
				ApplyInternal(card, Amount);
			}
		}
	}
}
