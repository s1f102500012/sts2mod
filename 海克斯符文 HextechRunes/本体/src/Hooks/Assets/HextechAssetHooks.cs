using Godot;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace HextechRunes;

/// <summary>
/// 只补原版没给可覆写路径的图标入口。遗物三种图标与卡牌立绘都由模型自己的虚路径属性指向 PCK 资源,
/// 原版 getter 直接加载,不在这里拦截。
/// </summary>
internal static class HextechAssetHooks
{
	private static readonly FieldInfo? HoverTipIconField = AccessTools.Field(typeof(HoverTip), "<Icon>k__BackingField");

	// PowerModel.PackedIconPath 不是 virtual,自定义能力图标只能在 getter 后替换。
	[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.Icon), MethodType.Getter)]
	[HextechPatch("assets.power-icon", "自定义能力图标")]
	private static class PowerIconPatch
	{
		[HarmonyPostfix]
		private static void Postfix(PowerModel __instance, ref Texture2D __result)
		{
			if (TryGetHextechPowerTexture(__instance, out Texture2D? texture))
			{
				__result = texture!;
			}
		}
	}

	[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.BigIcon), MethodType.Getter)]
	[HextechPatch("assets.power-big-icon", "自定义能力图标")]
	private static class PowerBigIconPatch
	{
		[HarmonyPostfix]
		private static void Postfix(PowerModel __instance, ref Texture2D __result)
		{
			if (TryGetHextechPowerTexture(__instance, out Texture2D? texture))
			{
				__result = texture!;
			}
		}
	}

	// HoverTip 是 record struct:其构造里读的 power.Icon 拿到的是原版 NOPE 占位
	// (AtlasResourceLoader 缺 sprite 时不返回 null 而是占位纹理,get_Icon postfix 覆盖
	// 不到 struct 构造内联/值语义路径)。在返回 HoverTip 的两个总入口修返回值。
	[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.GetDumbHoverTip))]
	[HextechPatch("assets.power-dumb-hover-tip", "自定义能力图标", Optional = true)]
	private static class PowerDumbHoverTipPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HoverTipIconField != null;

		[HarmonyPostfix]
		private static void Postfix(PowerModel __instance, ref HoverTip __result)
		{
			try
			{
				if (!TryGetHextechPowerTexture(__instance, out Texture2D? texture) || texture == null)
				{
					return;
				}

				// record struct:装箱→反射改字段→拆箱赋回。
				object boxed = __result;
				HoverTipIconField!.SetValue(boxed, texture);
				__result = (HoverTip)boxed;
			}
			catch (Exception ex)
			{
				HextechTextures.LogFailure(nameof(PowerDumbHoverTipPatch), ex);
			}
		}
	}

	[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.HoverTips), MethodType.Getter)]
	[HextechPatch("assets.power-hover-tips", "自定义能力图标", Optional = true)]
	private static class PowerHoverTipsPatch
	{
		[HarmonyPrepare]
		private static bool Prepare() => HoverTipIconField != null;

		[HarmonyPostfix]
		private static void Postfix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
		{
			try
			{
				if (!TryGetHextechPowerTexture(__instance, out Texture2D? texture) || texture == null)
				{
					return;
				}

				List<IHoverTip> tips = __result as List<IHoverTip> ?? __result.ToList();
				string ownId = __instance.Id.ToString();
				foreach (IHoverTip tip in tips)
				{
					// 接口引用即装箱实例,SetValue 直接写箱内字段;只修本 power 自己的 tip。
					if (tip is HoverTip concrete && concrete.Id == ownId)
					{
						HoverTipIconField!.SetValue(tip, texture);
					}
				}

				__result = tips;
			}
			catch (Exception ex)
			{
				HextechTextures.LogFailure(nameof(PowerHoverTipsPatch), ex);
			}
		}
	}

	// EnchantmentModel.IconPath 不是 virtual:拓展包按 API 登记的附魔图标在这里替换。
	[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.Icon), MethodType.Getter)]
	[HextechPatch("assets.enchantment-icon", "自定义附魔图标")]
	private static class EnchantmentIconPatch
	{
		[HarmonyPostfix]
		private static void Postfix(EnchantmentModel __instance, ref CompressedTexture2D __result)
		{
			try
			{
				ModelId id = __instance.CanonicalInstance?.Id ?? __instance.Id;
				if (HextechExternalContentRegistry.GetEnchantmentIconPath(id) is { } iconPath
					&& HextechTextures.LoadCompressedTexture(iconPath) is { } texture)
				{
					__result = texture;
					return;
				}

				if (__instance is UniversalSpiral)
				{
					__result = ModelDb.Enchantment<Spiral>().Icon;
				}
			}
			catch (Exception ex)
			{
				HextechTextures.LogFailure(nameof(EnchantmentIconPatch), ex);
			}
		}
	}

	/// <summary>
	/// 自定义休息室选项(「添柴」)的图标。基类 RestSiteOption.Icon 从 res://images/ui/rest_site/option_&lt;id&gt;.png 取图,
	/// 模组无法在该 base-game 命名空间提供真实资源;旧实现用可被卸载的缓存别名兜底,在联机非持有方会取到 null 并在渲染
	/// 思考气泡时抛 NotImplementedException —— 该异常发生在同步的 ChooseOption 路径里,导致离开休息室时校验和分叉。
	/// 这里直接返回稳定纹理(原版 Stoke 卡牌立绘),其它选项一律放行;解析失败同样放行,绝不抛出。
	/// </summary>
	[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Icon), MethodType.Getter)]
	[HextechPatch("assets.rest-site-option-icon", "添柴休息室选项图标")]
	private static class RestSiteOptionIconPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(RestSiteOption __instance, ref Texture2D __result)
		{
			try
			{
				if (__instance is StokeRestSiteOption && StokeRestSiteOption.ResolveIcon() is { } icon)
				{
					__result = icon;
					return false;
				}
			}
			catch (Exception ex)
			{
				HextechTextures.LogFailure(nameof(RestSiteOptionIconPatch), ex);
			}

			return true;
		}
	}

	// 以下 TryGet*Texture 助手承诺永不抛出:它们被图标 postfix 直接调用,而这些 getter
	// 位于联机同步敏感路径(历史上 RestSiteOption.Icon 异常曾致 ChooseOption 校验和分叉)。
	private static bool TryGetHextechPowerTexture(PowerModel self, out Texture2D? texture)
	{
		texture = null;
		try
		{
			return TryGetHextechPowerTextureCore(self, out texture);
		}
		catch (Exception ex)
		{
			HextechTextures.LogFailure(nameof(TryGetHextechPowerTexture), ex);
			texture = null;
			return false;
		}
	}

	private static bool TryGetHextechPowerTextureCore(PowerModel self, out Texture2D? texture)
	{
		texture = null;
		if (self is HextechPlayerSlowPower)
		{
			texture = ModelDb.Power<SlowPower>().Icon;
			return texture != null;
		}

		if (self is HextechGalvanicPower)
		{
			texture = ModelDb.Power<GalvanicPower>().Icon;
			return texture != null;
		}

		if (self is HextechNextTurnDamagePower)
		{
			texture = ModelDb.Power<BlockNextTurnPower>().Icon;
			return texture != null;
		}

		string? path = self switch
		{
			HextechBurnPower => $"res://{ModInfo.Id}/images/powers/hextechBurnPower.png",
			HextechAttackReplayPower => $"res://{ModInfo.Id}/images/powers/hextechAttackReplayPower.png",
			HextechOceanDragonSoulPower => HextechAssets.OceanDragonSoulPowerIconPath,
			HextechInfernalDragonSoulPower => HextechAssets.InfernalDragonSoulPowerIconPath,
			HextechDragonSoulPower => HextechAssets.HextechDragonSoulPowerIconPath,
			HextechMountainDragonSoulPower => HextechAssets.MountainDragonSoulPowerIconPath,
			HextechChemtechDragonSoulPower => HextechAssets.ChemtechDragonSoulPowerIconPath,
			HextechCloudDragonSoulPower => HextechAssets.CloudDragonSoulPowerIconPath,
			_ => null
		};
		if (path == null)
		{
			return false;
		}

		texture = HextechTextures.LoadPortableTexture(path);
		return texture != null;
	}
}
