using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes.Relics;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static class HextechAssetHooks
{
	private static readonly Dictionary<string, Texture2D> TextureCache = new();
	private static readonly Dictionary<string, CompressedTexture2D> CompressedTextureCache = new();

	private static readonly FieldInfo? NRelicModelField = TryGetField(typeof(NRelic), "_model");

	public static void Install(Harmony harmony)
	{
		MethodInfo getRelicIcon = RequireGetter(typeof(RelicModel), nameof(RelicModel.Icon));
		MethodInfo getRelicIconOutline = RequireGetter(typeof(RelicModel), nameof(RelicModel.IconOutline));
		MethodInfo getRelicBigIcon = RequireGetter(typeof(RelicModel), nameof(RelicModel.BigIcon));
		MethodInfo? relicReload = TryGetMethod(typeof(NRelic), "Reload", BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo getPowerIcon = RequireGetter(typeof(PowerModel), nameof(PowerModel.Icon));
		MethodInfo getPowerBigIcon = RequireGetter(typeof(PowerModel), nameof(PowerModel.BigIcon));
		MethodInfo getCardPortrait = RequireGetter(typeof(CardModel), nameof(CardModel.Portrait));
		MethodInfo getEnchantmentIcon = RequireGetter(typeof(EnchantmentModel), nameof(EnchantmentModel.Icon));

		harmony.Patch(getRelicIcon, prefix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(RelicTexturePrefix)));
		harmony.Patch(getRelicIconOutline, prefix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(RelicTexturePrefix)));
		harmony.Patch(getRelicBigIcon, prefix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(RelicTexturePrefix)));
		if (relicReload != null && NRelicModelField != null)
		{
			harmony.Patch(relicReload, prefix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(NRelicReloadPrefix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] NRelic.Reload asset hook skipped: missing {(relicReload == null ? "NRelic.Reload" : "NRelic._model")}.");
		}
		harmony.Patch(getPowerIcon, postfix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(PowerIconPostfix)));
		harmony.Patch(getPowerBigIcon, postfix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(PowerBigIconPostfix)));
		harmony.Patch(getCardPortrait, postfix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(CardPortraitPostfix)));
		harmony.Patch(getEnchantmentIcon, postfix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(EnchantmentIconPostfix)));

		// HoverTip 是 record struct:其构造里读的 power.Icon 拿到的是原版 NOPE 占位
		// (AtlasResourceLoader 缺 sprite 时不返回 null 而是占位纹理,get_Icon postfix 覆盖
		// 不到 struct 构造内联/值语义路径)。在返回 HoverTip 的两个总入口修返回值:
		// GetDumbHoverTip(遗物/卡牌 ExtraHoverTips 走 FromPower)与 HoverTips(战斗内 smart tip)。
		MethodInfo? getDumbHoverTip = AccessTools.Method(typeof(PowerModel), nameof(PowerModel.GetDumbHoverTip));
		MethodInfo? getHoverTips = AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.HoverTips));
		if (getDumbHoverTip != null && getHoverTips != null)
		{
			harmony.Patch(getDumbHoverTip, postfix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(GetDumbHoverTipPostfix)));
			harmony.Patch(getHoverTips, postfix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(PowerHoverTipsPostfix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Power hover tip icon hooks skipped: target methods not found.");
		}
		if (HoverTipIconField == null)
		{
			Log.Warn($"[{ModInfo.Id}][Assets] Power hover tip icon backing field not found; hover tip icons will show the vanilla placeholder.");
		}

		// 自定义休息室选项(目前为「添柴」StokeRestSiteOption)的图标修复。
		// 基类 RestSiteOption.Icon 从 res://images/ui/rest_site/option_<id>.png 取图,模组无法在该 base-game
		// 命名空间提供真实资源,旧实现用可被卸载的缓存别名兜底,在联机非持有方会取到 null 并在渲染思考气泡时抛
		// NotImplementedException —— 该异常发生在同步的 ChooseOption 路径里,导致离开休息室时校验和分叉、客户端被踢。
		// 这里用前缀直接返回稳定纹理(原版 Stoke 卡牌立绘),保证任何端、任何时机 Icon 都有效。
		MethodInfo? getRestSiteOptionIcon = AccessTools.PropertyGetter(typeof(RestSiteOption), nameof(RestSiteOption.Icon));
		if (getRestSiteOptionIcon != null)
		{
			harmony.Patch(getRestSiteOptionIcon, prefix: new HarmonyMethod(typeof(HextechAssetHooks), nameof(RestSiteOptionIconPrefix)));
		}
		else
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] RestSiteOption.Icon asset hook skipped: getter not found (custom rest-site option icons may fail to render and could desync multiplayer).");
		}
	}

	/// <summary>
	/// 为自定义休息室选项提供稳定的图标。仅拦截 <see cref="StokeRestSiteOption"/>:返回原版 Stoke 立绘并跳过原版
	/// 缓存查找(原版查找在该 mod 路径上必然 miss,并可能因 thought bubble 以 null 初始化而抛异常)。
	/// 其它(原版)选项一律放行原逻辑。解析失败时同样放行,退化为修复前行为,绝不抛出。
	/// </summary>
	private static bool RestSiteOptionIconPrefix(RestSiteOption __instance, ref Texture2D __result)
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
			LogAssetHookFailure(nameof(RestSiteOptionIconPrefix), ex);
		}

		return true;
	}

	private static void CardPortraitPostfix(CardModel __instance, ref Texture2D __result)
	{
		if (TryGetHextechCardTexture(__instance, out Texture2D? texture))
		{
			__result = texture!;
		}
	}

	private static void EnchantmentIconPostfix(EnchantmentModel __instance, ref CompressedTexture2D __result)
	{
		try
		{
			ModelId id = __instance.CanonicalInstance?.Id ?? __instance.Id;
			if (HextechExternalContentRegistry.GetEnchantmentIconPath(id) is { } iconPath
				&& LoadCompressedTexture(iconPath) is { } texture)
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
			LogAssetHookFailure(nameof(EnchantmentIconPostfix), ex);
		}
	}

	private static bool RelicTexturePrefix(RelicModel __instance, ref Texture2D __result)
	{
		try
		{
			string? path = HextechAssets.TryGetCustomRelicIconPath(__instance);
			if (path == null)
			{
				return true;
			}

			Texture2D? texture = LoadPortableTexture(path) ?? GetMissingTexture();
			if (!IsTextureUsable(texture))
			{
				return true;
			}

			__result = texture!;
			return false;
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(RelicTexturePrefix), ex);
			return true;
		}
	}

	private static bool NRelicReloadPrefix(NRelic __instance)
	{
		try
		{
			if (!__instance.IsNodeReady()
				|| NRelicModelField == null
				|| NRelicModelField.GetValue(__instance) is not RelicModel model
				|| !TryGetHextechRelicTexture(model, out Texture2D? texture))
			{
				return true;
			}

			model.UpdateTexture(__instance.Icon);
			__instance.Icon.Texture = texture;
			__instance.Outline.Visible = false;
			return false;
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(NRelicReloadPrefix), ex);
			return true;
		}
	}

	private static void PowerIconPostfix(PowerModel __instance, ref Texture2D __result)
	{
		if (TryGetHextechPowerTexture(__instance, out Texture2D? texture))
		{
			__result = texture!;
		}
	}

	private static void PowerBigIconPostfix(PowerModel __instance, ref Texture2D __result)
	{
		if (TryGetHextechPowerTexture(__instance, out Texture2D? texture))
		{
			__result = texture!;
		}
	}

	private static readonly FieldInfo? HoverTipIconField = AccessTools.Field(typeof(HoverTip), "<Icon>k__BackingField");

	private static void GetDumbHoverTipPostfix(PowerModel __instance, ref HoverTip __result)
	{
		try
		{
			if (HoverTipIconField == null || !TryGetHextechPowerTexture(__instance, out Texture2D? texture) || texture == null)
			{
				return;
			}

			// record struct:装箱→反射改字段→拆箱赋回。
			object boxed = __result;
			HoverTipIconField.SetValue(boxed, texture);
			__result = (HoverTip)boxed;
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(GetDumbHoverTipPostfix), ex);
		}
	}

	private static void PowerHoverTipsPostfix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		try
		{
			if (HoverTipIconField == null || !TryGetHextechPowerTexture(__instance, out Texture2D? texture) || texture == null)
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
					HoverTipIconField.SetValue(tip, texture);
				}
			}

			__result = tips;
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(PowerHoverTipsPostfix), ex);
		}
	}

	// 以下 TryGet*Texture 助手承诺永不抛出:它们被图标 postfix 直接调用,而这些 getter
	// 位于联机同步敏感路径(历史上 RestSiteOption.Icon 异常曾致 ChooseOption 校验和分叉)。
	private static bool TryGetHextechRelicTexture(RelicModel self, out Texture2D? texture)
	{
		texture = null;
		try
		{
			string? path = HextechAssets.TryGetCustomRelicIconPath(self);
			if (path == null)
			{
				return false;
			}

			texture = LoadPortableTexture(path);
			return texture != null;
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(TryGetHextechRelicTexture), ex);
			texture = null;
			return false;
		}
	}

	private static bool TryGetHextechPowerTexture(PowerModel self, out Texture2D? texture)
	{
		texture = null;
		try
		{
			return TryGetHextechPowerTextureCore(self, out texture);
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(TryGetHextechPowerTexture), ex);
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

		texture = LoadPortableTexture(path);
		return texture != null;
	}

	private static bool TryGetHextechCardTexture(CardModel self, out Texture2D? texture)
	{
		texture = null;
		try
		{
			return TryGetHextechCardTextureCore(self, out texture);
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(TryGetHextechCardTexture), ex);
			texture = null;
			return false;
		}
	}

	private static bool TryGetHextechCardTextureCore(CardModel self, out Texture2D? texture)
	{
		texture = null;
		string? path = self switch
		{
			TrickMagicCard => HextechAssets.TrickMagicCardPortraitPath,
			BladeWaltzCard => HextechAssets.BladeWaltzCardPortraitPath,
			OceanDragonSoulCard => HextechAssets.OceanDragonSoulCardPortraitPath,
			InfernalDragonSoulCard => HextechAssets.InfernalDragonSoulCardPortraitPath,
			HextechDragonSoulCard => HextechAssets.HextechDragonSoulCardPortraitPath,
			MountainDragonSoulCard => HextechAssets.MountainDragonSoulCardPortraitPath,
			ChemtechDragonSoulCard => HextechAssets.ChemtechDragonSoulCardPortraitPath,
			CloudDragonSoulCard => HextechAssets.CloudDragonSoulCardPortraitPath,
			MikaelsBlessingCard => HextechAssets.MikaelsBlessingCardPortraitPath,
			_ => null
		};
		if (path == null)
		{
			return false;
		}

		texture = LoadPortableTexture(path);
		return texture != null;
	}

	internal static Texture2D? LoadUiTexture(string path)
	{
		return LoadPortableTexture(path);
	}

	private static Texture2D? _missingTexture;

	internal static Texture2D? GetMissingTexture()
	{
		if (IsTextureUsable(_missingTexture))
		{
			return _missingTexture;
		}

		try
		{
			const int size = 64;
			Image image = Image.CreateEmpty(size, size, useMipmaps: false, Image.Format.Rgba8);
			image.Fill(new Color(0.12f, 0.14f, 0.18f, 0.96f));
			Color accent = new(0.72f, 0.76f, 0.84f, 0.9f);
			for (int i = 10; i < size - 10; i++)
			{
				image.SetPixel(i, i, accent);
				image.SetPixel(size - 1 - i, i, accent);
			}

			_missingTexture = ImageTexture.CreateFromImage(image);
			return IsTextureUsable(_missingTexture) ? _missingTexture : null;
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(GetMissingTexture), ex);
			return null;
		}
	}

	internal static bool IsTextureUsable(Texture2D? texture)
	{
		if (texture == null)
		{
			return false;
		}

		try
		{
			return GodotObject.IsInstanceValid(texture) && texture.GetWidth() > 0 && texture.GetHeight() > 0;
		}
		catch (ObjectDisposedException)
		{
			return false;
		}
	}

	private static void LogAssetHookFailure(string hook, Exception ex)
	{
		if (HextechRunLogBudget.TryConsume("assets.hook-failure", 10))
		{
			Log.Error($"[{ModInfo.Id}][Assets] {hook} failed; keeping original result: {ex}");
		}
	}

	private static Texture2D? LoadPortableTexture(string path)
	{
		// 自定义运行时可能拒绝编辑器导入的 ctex，并留下非空但已释放的纹理包装对象。
		// 原始图像字节在支持的游戏版本间保持稳定，因此图片资源只手动解码，不进入导入资源链。
		Func<string, Texture2D?> secondaryLoader = AssetResourceResolver.IsRawImagePath(path)
			? static _ => null
			: LoadTextureThroughResourceLoader;
		Texture2D? texture = AssetResourceResolver.Resolve(
			path,
			TextureCache,
			IsTextureUsable,
			LoadRawImageTexture,
			secondaryLoader);
		if (texture == null)
		{
			WarnTextureMissOnce(
				path,
				AssetResourceResolver.IsRawImagePath(path)
					? "raw image decode miss"
					: "raw decode and ResourceLoader miss");
		}

		return texture;
	}

	private static Texture2D? LoadRawImageTexture(string path)
	{
		return TryLoadRawImageTexture(path, out Texture2D? texture) ? texture : null;
	}

	private static Texture2D? LoadTextureThroughResourceLoader(string path)
	{
		try
		{
			return ResourceLoader.Load<Texture2D>(path);
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(LoadTextureThroughResourceLoader), ex);
			return null;
		}
	}

	private static bool TryLoadRawImageTexture(string path, out Texture2D? texture)
	{
		texture = null;
		if (!AssetResourceResolver.IsRawImagePath(path))
		{
			return false;
		}

		bool isPng = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

		// 解码与建纹理整体兜底:Resolve 按契约不 catch,LoadUiTexture 链上也没有别的护栏。
		try
		{
			byte[] bytes = Godot.FileAccess.GetFileAsBytes(path);
			if (bytes.Length == 0)
			{
				return false;
			}

			Image image = new();
			Error err = isPng
				? image.LoadPngFromBuffer(bytes)
				: image.LoadJpgFromBuffer(bytes);
			if (err != Error.Ok)
			{
				return false;
			}

			ImageTexture? imageTexture = ImageTexture.CreateFromImage(image);
			if (!IsTextureUsable(imageTexture))
			{
				imageTexture?.Dispose();
				return false;
			}

			texture = imageTexture;
			return true;
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(TryLoadRawImageTexture), ex);
			return false;
		}
	}

	private static CompressedTexture2D? LoadCompressedTexture(string path)
	{
		if (CompressedTextureCache.TryGetValue(path, out CompressedTexture2D? cachedTexture))
		{
			if (IsTextureUsable(cachedTexture))
			{
				return cachedTexture;
			}

			CompressedTextureCache.Remove(path);
		}

		try
		{
			if (ResourceLoader.Load<CompressedTexture2D>(path) is { } loadedTexture && IsTextureUsable(loadedTexture))
			{
				CompressedTextureCache[path] = loadedTexture;
				return loadedTexture;
			}
		}
		catch (Exception ex)
		{
			LogAssetHookFailure(nameof(LoadCompressedTexture), ex);
		}

		WarnTextureMissOnce(path, "ResourceLoader returned no usable compressed texture");
		return null;
	}

	private static readonly HashSet<string> WarnedTextureMissPaths = new(StringComparer.Ordinal);

	// 加载失败最终表现是静默 NOPE 占位,肉眼难归因;每路径告警一次方便定位打包/命名问题。
	private static void WarnTextureMissOnce(string path, string reason)
	{
		if (WarnedTextureMissPaths.Add(path))
		{
			Log.Warn($"[{ModInfo.Id}][Assets] Texture load miss ({reason}): {path}");
		}
	}
}
