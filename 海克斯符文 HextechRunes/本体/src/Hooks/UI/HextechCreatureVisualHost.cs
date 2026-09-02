using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

/// <summary>
/// 战斗内 creature 节点视觉附件的统一宿主:战斗房间就绪、召唤加入、节点就绪三个时机各只补一次,
/// 再按固定顺序把节点交给各附件的 <c>TryAttach</c>。新增附件只需在 <see cref="Attach"/> 里加一行,
/// 不再各自去补同一批原版方法。
/// </summary>
/// <remarks>
/// 顺序即语义:节点注册表在光环类附件之后、灼烧/玻璃大炮之前重建,与各附件此前的安装序一致。
/// 附件的 <c>TryAttach</c> 必须自行容忍重复调用与失效节点。
/// </remarks>
internal static class HextechCreatureVisualHost
{
	private static void Attach(NCreature? node)
	{
		if (!GodotObject.IsInstanceValid(node))
		{
			return;
		}

		HandOfBaronAuraVisual.TryAttach(node);
		SlowCookAuraVisual.TryAttach(node);
		HextechNearDeathFeastVisual.TryAttach(node);
		HextechCreatureNodeRegistry.Register(node);
		HextechBurnVisual.TryAttach(node);
		HextechGlassCannonHealthBarVisual.TryAttach(node);
	}

	[HarmonyPatch(typeof(NCombatRoom), "_Ready", new Type[0])]
	[HextechPatch("visual.host.room-ready", "战斗视觉附件")]
	private static class CombatRoomReadyPatch
	{
		[HarmonyPostfix]
		private static void Postfix(NCombatRoom __instance)
		{
			NCreature[] nodes = __instance.CreatureNodes.ToArray();
			foreach (NCreature node in nodes)
			{
				HandOfBaronAuraVisual.TryAttach(node);
				SlowCookAuraVisual.TryAttach(node);
				HextechNearDeathFeastVisual.TryAttach(node);
			}

			// 新战斗重建 entity → 节点映射,再挂依赖映射的附件。
			HextechCreatureNodeRegistry.Clear();
			foreach (NCreature node in nodes)
			{
				HextechCreatureNodeRegistry.Register(node);
			}

			foreach (NCreature node in nodes)
			{
				HextechBurnVisual.TryAttach(node);
				HextechGlassCannonHealthBarVisual.TryAttach(node);
			}
		}
	}

	[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature), typeof(Creature))]
	[HextechPatch("visual.host.add-creature", "战斗视觉附件")]
	private static class AddCreaturePatch
	{
		[HarmonyPostfix]
		private static void Postfix(NCombatRoom __instance, Creature creature)
		{
			Attach(HextechCreatureNodeRegistry.SafeGetCreatureNode(__instance, creature));
		}
	}

	[HarmonyPatch(typeof(NCreature), "_Ready", new Type[0])]
	[HextechPatch("visual.host.creature-ready", "战斗视觉附件")]
	private static class CreatureReadyPatch
	{
		[HarmonyPostfix]
		private static void Postfix(NCreature __instance)
		{
			Attach(__instance);
		}
	}
}
