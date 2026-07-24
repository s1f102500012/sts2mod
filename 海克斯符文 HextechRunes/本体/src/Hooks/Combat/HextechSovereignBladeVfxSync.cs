using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HextechRunes;

/// <summary>
/// 按玩家当前牌堆修正常驻君王之剑实体。原版从消耗堆自动打出时会先生成新实体，
/// 随后却只移除匹配到的第一项；旧实体仍在缩小退场时可能导致新实体漏删。
/// </summary>
internal static class HextechSovereignBladeVfxSync
{
	internal static void Reconcile(Player owner)
	{
		try
		{
			ReconcileNow(owner, createMissing: true);
			// AddChildSafely/QueueFreeSafely 都可能延至帧末，再校正一次数量、缩放和轨道间距。
			Callable.From(() => ReconcileNow(owner, createMissing: false)).CallDeferred();
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][SovereignBladeVfxSync] Could not reconcile blade entities: {ex.Message}", 2);
		}
	}

	internal static float GetNormalScaleForDamage(int damage)
	{
		float bladeSize = Mathf.Clamp((float)damage / 200f, 0f, 1f);
		return Mathf.Lerp(0.9f, 2f, bladeSize);
	}

	private static void ReconcileNow(Player owner, bool createMissing)
	{
		if (owner.PlayerCombatState == null || owner.Creature.CombatState == null)
		{
			return;
		}

		NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner.Creature);
		if (!GodotObject.IsInstanceValid(ownerNode))
		{
			return;
		}

		List<SovereignBlade> expected = owner.PlayerCombatState.AllCards
			.OfType<SovereignBlade>()
			.Where(static blade => !blade.IsDupe && blade.Pile?.Type != PileType.Exhaust)
			.ToList();
		HashSet<SovereignBlade> expectedSet = new(expected, ReferenceEqualityComparer.Instance);
		Dictionary<SovereignBlade, NSovereignBladeVfx> kept = new(ReferenceEqualityComparer.Instance);

		foreach (NSovereignBladeVfx vfx in ownerNode!.GetChildren().OfType<NSovereignBladeVfx>())
		{
			if (vfx.IsQueuedForDeletion())
			{
				continue;
			}

			SovereignBlade? blade = vfx.Card as SovereignBlade;
			if (blade == null || blade.IsDupe || !expectedSet.Contains(blade))
			{
				vfx.QueueFreeSafely();
				continue;
			}

			if (!kept.TryGetValue(blade, out NSovereignBladeVfx? existing))
			{
				kept.Add(blade, vfx);
				continue;
			}

			// 优先保留已经正常展开的实体，淘汰仍处于缩小退场状态的旧实体。
			if (GetVisualScaleScore(vfx) > GetVisualScaleScore(existing))
			{
				existing.QueueFreeSafely();
				kept[blade] = vfx;
			}
			else
			{
				vfx.QueueFreeSafely();
			}
		}

		if (createMissing)
		{
			foreach (SovereignBlade blade in expected)
			{
				if (!kept.ContainsKey(blade))
				{
					ForgeCmd.PlayCombatRoomForgeVfx(owner, blade);
				}
			}
		}

		List<NSovereignBladeVfx> active = ownerNode.GetChildren()
			.OfType<NSovereignBladeVfx>()
			.Where(static vfx => !vfx.IsQueuedForDeletion())
			.ToList();
		for (int i = 0; i < expected.Count; i++)
		{
			SovereignBlade blade = expected[i];
			NSovereignBladeVfx? vfx = active
				.Where(candidate => ReferenceEquals(candidate.Card, blade))
				.OrderByDescending(GetVisualScaleScore)
				.FirstOrDefault();
			if (vfx == null)
			{
				continue;
			}

			vfx.OrbitProgress = expected.Count == 0 ? 0d : (double)i / expected.Count;
			Node2D? spineNode = vfx.GetNodeOrNull<Node2D>("SpineSword");
			if (spineNode != null)
			{
				float scale = GetNormalScaleForDamage(blade.DynamicVars.Damage.IntValue);
				spineNode.Scale = Vector2.One * scale;
			}
		}
	}

	private static float GetVisualScaleScore(NSovereignBladeVfx vfx)
	{
		return vfx.GetNodeOrNull<Node2D>("SpineSword")?.Scale.LengthSquared() ?? -1f;
	}
}
