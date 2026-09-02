using HarmonyLib;
#if STS2_110_OR_NEWER
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Forms;
using static HextechRunes.HextechHookReflection;
#endif

namespace HextechRunes;

// 自定义角色场景不一定提供 0.110 起新增的 %FormVfx 容器。形态能力本身不依赖该节点，
// 因此容器缺失时只跳过纯视觉挂载与清理，避免战斗结算和放弃流程被 VFX 异常中断。
internal static class HextechFormVfxSafetyHooks
{
	internal enum FormVfxKind
	{
		Other,
		Demon,
		Serpent
	}

	internal static bool ShouldRunOriginal(bool hasFormVfxHolder)
	{
		return hasFormVfxHolder;
	}

	internal static bool ShouldPreserveExistingForSymphony(
		bool hasSymphonyOfWar,
		FormVfxKind incoming,
		FormVfxKind existing)
	{
		return hasSymphonyOfWar
			&& existing is FormVfxKind.Demon or FormVfxKind.Serpent
			&& existing != incoming;
	}

#if STS2_110_OR_NEWER
	internal static MethodInfo ResolveAddFormVfxTarget()
	{
		return RequireMethod(
			typeof(NCreatureVisuals),
			nameof(NCreatureVisuals.AddFormVfx),
			BindingFlags.Instance | BindingFlags.Public,
			typeof(NFormVfx));
	}

	internal static MethodInfo ResolveRemoveFormVfxTarget()
	{
		return RequireMethod(
			typeof(NCreatureVisuals),
			nameof(NCreatureVisuals.RemoveFormVfx),
			BindingFlags.Instance | BindingFlags.Public);
	}


	private static Control? GetFormVfxHolder(NCreatureVisuals visuals)
	{
#if STS2_111_OR_NEWER
		return visuals.FormVfxHolder;
#else
		return visuals._formVfxHolder;
#endif
	}

	private static bool HasSymphonyOfWar(NCreatureVisuals visuals)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		return room != null && room.CreatureNodes.Any(creatureNode =>
			ReferenceEquals(creatureNode.Visuals, visuals)
			&& creatureNode.Entity.Player?.GetRelic<SymphonyOfWarRune>() != null);
	}

	private static FormVfxKind GetFormVfxKind(NFormVfx formVfx)
	{
		return formVfx switch
		{
			NDemonFormVfx => FormVfxKind.Demon,
			NSerpentFormVfx => FormVfxKind.Serpent,
			_ => FormVfxKind.Other
		};
	}
#endif

	#if STS2_110_OR_NEWER
	[HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals.AddFormVfx), typeof(NFormVfx))]
	[HextechPatch("compat.form-vfx.add", "形态特效容器安全")]
	private static class AddFormVfxPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		private static bool Prefix(NCreatureVisuals __instance, NFormVfx formVfx)
		{
			Control? holder = GetFormVfxHolder(__instance);
			if (!ShouldRunOriginal(holder != null))
			{
				return false;
			}
			if (!HasSymphonyOfWar(__instance))
			{
				return true;
			}

			// 战争交响乐固定保留恶魔与群蛇两层视觉；其他形态之间仍维持原版的后到覆盖先到。
			FormVfxKind incoming = GetFormVfxKind(formVfx);
			foreach (Node child in holder!.GetChildren())
			{
				FormVfxKind existing = child is NFormVfx existingFormVfx
					? GetFormVfxKind(existingFormVfx)
					: FormVfxKind.Other;
				if (!ShouldPreserveExistingForSymphony(
					hasSymphonyOfWar: true,
					incoming,
					existing))
				{
					child.Free();
				}
			}

			holder.AddChild(formVfx);
			formVfx.Position = Vector2.Zero;
			return false;
		}
	}

	[HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals.RemoveFormVfx), new Type[0])]
	[HextechPatch("compat.form-vfx.remove", "形态特效容器安全")]
	private static class RemoveFormVfxPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.First)]
		private static bool Prefix(NCreatureVisuals __instance)
		{
			return ShouldRunOriginal(GetFormVfxHolder(__instance) != null);
		}
	}
#endif
}
