using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace UniversalDominionSword;

internal static class DynamicRelicIcon
{
	private static readonly Dictionary<string, Texture2D> TextureCache = new();

	private static readonly FieldInfo? NRelicModelField =
		typeof(NRelic).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? InspectRelicsField =
		typeof(NInspectRelicScreen).GetField("_relics", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? InspectIndexField =
		typeof(NInspectRelicScreen).GetField("_index", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? InspectImageField =
		typeof(NInspectRelicScreen).GetField("_relicImage", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly Dictionary<ulong, Material?> InspectOriginalMaterials = new();

	private static Shader? _cosmicShader;

	private static bool _installed;

	public static void Install(Harmony harmony)
	{
		if (_installed)
		{
			return;
		}

		PatchGetter(harmony, typeof(RelicModel), nameof(RelicModel.Icon), nameof(RelicIconPrefix));
		PatchGetter(harmony, typeof(RelicModel), nameof(RelicModel.BigIcon), nameof(RelicBigIconPrefix));
		PatchGetter(harmony, typeof(CardModel), nameof(CardModel.Portrait), nameof(CardPortraitPrefix));

		MethodInfo? reload = typeof(NRelic).GetMethod(
			"Reload",
			BindingFlags.Instance | BindingFlags.NonPublic);

		if (reload == null || NRelicModelField == null)
		{
			throw new MissingMemberException("NRelic.Reload or NRelic._model was not found.");
		}

		harmony.Patch(reload, prefix: new HarmonyMethod(typeof(DynamicRelicIcon), nameof(NRelicReloadPrefix)));

		MethodInfo? inspectUpdate = typeof(NInspectRelicScreen).GetMethod(
			"UpdateRelicDisplay",
			BindingFlags.Instance | BindingFlags.NonPublic);
		if (inspectUpdate == null
			|| InspectRelicsField == null
			|| InspectIndexField == null
			|| InspectImageField == null)
		{
			throw new MissingMemberException("NInspectRelicScreen display fields or update method were not found.");
		}

		harmony.Patch(
			inspectUpdate,
			postfix: new HarmonyMethod(typeof(DynamicRelicIcon), nameof(InspectRelicUpdatePostfix)));

		MethodInfo? eventOptionReady = typeof(NEventOptionButton).GetMethod(
			nameof(NEventOptionButton._Ready),
			BindingFlags.Instance | BindingFlags.Public);
		if (eventOptionReady == null)
		{
			throw new MissingMemberException("NEventOptionButton._Ready was not found.");
		}

		harmony.Patch(
			eventOptionReady,
			postfix: new HarmonyMethod(typeof(DynamicRelicIcon), nameof(EventOptionReadyPostfix)));
		_installed = true;
	}

	private static void PatchGetter(Harmony harmony, Type type, string propertyName, string prefixName)
	{
		MethodInfo getter = type.GetProperty(
				propertyName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?.GetMethod
			?? throw new MissingMemberException(type.FullName, propertyName);

		harmony.Patch(getter, prefix: new HarmonyMethod(typeof(DynamicRelicIcon), prefixName));
	}

	private static bool RelicIconPrefix(RelicModel __instance, ref Texture2D __result)
	{
		return TrySupplyRelicFallback(__instance, ref __result);
	}

	private static bool RelicBigIconPrefix(RelicModel __instance, ref Texture2D __result)
	{
		return TrySupplyRelicFallback(__instance, ref __result);
	}

	private static bool TrySupplyRelicFallback(RelicModel model, ref Texture2D result)
	{
		if (model is not UniversalDominionSwordRelic)
		{
			return true;
		}

		result = RequireTexture(ModInfo.RelicIconPath);
		return false;
	}

	private static bool CardPortraitPrefix(CardModel __instance, ref Texture2D __result)
	{
		if (__instance is not UniversalDominionSwordCard)
		{
			return true;
		}

		__result = RequireTexture(ModInfo.CardPortraitPath);
		return false;
	}

	private static bool NRelicReloadPrefix(NRelic __instance)
	{
		if (!__instance.IsNodeReady()
			|| NRelicModelField?.GetValue(__instance) is not UniversalDominionSwordRelic model)
		{
			return true;
		}

		__instance.Icon.Texture = RequireTexture(ModInfo.RelicIconPath);
		__instance.Icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		__instance.Icon.Material = CreateCosmicMaterial();
		model.UpdateTexture(__instance.Icon);
		__instance.Outline.Visible = false;
		return false;
	}

	private static void InspectRelicUpdatePostfix(NInspectRelicScreen __instance)
	{
		if (InspectImageField?.GetValue(__instance) is not TextureRect image
			|| InspectRelicsField?.GetValue(__instance) is not IReadOnlyList<RelicModel> relics
			|| InspectIndexField?.GetValue(__instance) is not int index
			|| index < 0
			|| index >= relics.Count)
		{
			return;
		}

		ulong imageId = image.GetInstanceId();
		InspectOriginalMaterials.TryAdd(imageId, image.Material);

		RelicModel relic = relics[index];
		if (relic is not UniversalDominionSwordRelic)
		{
			image.Material = InspectOriginalMaterials[imageId];
			image.TextureFilter = CanvasItem.TextureFilterEnum.ParentNode;
			return;
		}

		image.Texture = RequireTexture(ModInfo.RelicIconPath);
		image.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		image.Material = CreateCosmicMaterial();
		relic.UpdateTexture(image);
		Log.Info($"[{ModInfo.Id}][Inspect] Applied animated Avaritia cosmic material to large relic image.");
	}

	private static void EventOptionReadyPostfix(NEventOptionButton __instance)
	{
		if (__instance.Option.Relic is not UniversalDominionSwordRelic relic)
		{
			return;
		}

		TextureRect icon = __instance.GetNode<TextureRect>("%RelicIcon");
		icon.Texture = RequireTexture(ModInfo.RelicIconPath);
		icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		icon.Material = CreateCosmicMaterial();
		relic.UpdateTexture(icon);
		icon.GetNode<TextureRect>("%Outline").Visible = false;

		Log.Info(
			$"[{ModInfo.Id}][EventOption] Applied animated Avaritia cosmic material to Neow relic option.");
	}

	private static ShaderMaterial CreateCosmicMaterial()
	{
		_cosmicShader ??= new Shader
		{
			Code = AvaritiaCosmicShader.Code
		};

		ShaderMaterial material = new()
		{
			Shader = _cosmicShader
		};
		material.SetShaderParameter("layer_0", RequireTexture(ModInfo.Layer0Path));
		material.SetShaderParameter("layer_1", RequireTexture(ModInfo.Layer1Path));
		material.SetShaderParameter("blade_mask", RequireTexture(ModInfo.MaskPath));
		for (int index = 0; index < 10; index++)
		{
			material.SetShaderParameter($"cosmic_{index}", RequireTexture(ModInfo.CosmicPath(index)));
		}
		return material;
	}

	private static Texture2D RequireTexture(string path)
	{
		if (TextureCache.TryGetValue(path, out Texture2D? cached)
			&& GodotObject.IsInstanceValid(cached)
			&& cached.GetWidth() > 0)
		{
			return cached;
		}

		byte[] bytes = LoadBytes(path);
		Image image = new();
		Error error = image.LoadPngFromBuffer(bytes);
		if (error != Error.Ok)
		{
			throw new InvalidDataException($"Could not decode PNG '{path}': {error}.");
		}

		PortableCompressedTexture2D texture = new();
		texture.CreateFromImage(image, PortableCompressedTexture2D.CompressionMode.Lossless);
		TextureCache[path] = texture;
		return texture;
	}

	private static byte[] LoadBytes(string path)
	{
		byte[] bytes = Godot.FileAccess.GetFileAsBytes(path);
		if (bytes.Length > 0)
		{
			return bytes;
		}

		const string prefix = "res://UniversalDominionSword/";
		if (!path.StartsWith(prefix, StringComparison.Ordinal))
		{
			throw new FileNotFoundException("Unsupported mod asset path.", path);
		}

		string resourceName = "UniversalDominionSword."
			+ path[prefix.Length..].Replace('/', '.');
		using Stream? stream = typeof(DynamicRelicIcon).Assembly.GetManifestResourceStream(resourceName);
		if (stream == null)
		{
			throw new FileNotFoundException($"Embedded asset '{resourceName}' was not found.", path);
		}

		using MemoryStream memory = new();
		stream.CopyTo(memory);
		bytes = memory.ToArray();
		Log.Info($"[{ModInfo.Id}] Loaded embedded visual asset {resourceName}.");
		return bytes;
	}
}
