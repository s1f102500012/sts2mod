using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Enchantments;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 墨影(Inkshadow)符文在小刀生成时已上 Inky,而 Inky 不可叠加(IsStackable=false):
/// 原版 BladeOfInk.OnPlay 对生成的小刀二次 CardCmd.Enchant 会因 CanEnchant=false 抛
/// InvalidOperationException 打断打出管线(玩家实测:墨影+瓦库之肩自动打出墨之刃即卡死;手动打出同样中招)。
/// 仅当持有墨影符文时替换 OnPlay 实现,补附魔前守卫;未持有时走原版路径不受影响。
/// </summary>
internal static class HextechInkshadowHooks
{


	internal static async Task PlayWithGuardedEnchant(BladeOfInk card, Player owner, HextechCombatState combatState)
	{
		foreach (CardModel item in await Shiv.CreateInHand(owner, card.DynamicVars.Cards.IntValue, combatState))
		{
			if (item.Enchantment is Inky)
			{
				// 墨影已在生成时上过 Inky,原版的二次附魔按语义就是"确保有 Inky",直接跳过。
				continue;
			}

			Inky enchantment = (Inky)ModelDb.Enchantment<Inky>().ToMutable();
			if (enchantment.CanEnchant(item))
			{
				CardCmd.Enchant(enchantment, item, 1m);
			}
		}
	}

}
