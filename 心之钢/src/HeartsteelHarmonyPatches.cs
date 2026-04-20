using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace Heartsteel;

internal static class HeartsteelHarmony
{
	internal const string HarmonyId = "Heartsteel";

	internal static void Install()
	{
		new Harmony(HarmonyId).PatchAll(typeof(HeartsteelHarmony).Assembly);
	}
}

[HarmonyPatch(typeof(AssetCache), "LoadAsset", [typeof(string)])]
internal static class AssetCache_LoadAsset_Patch
{
	[HarmonyPrefix]
	private static bool Prefix(AssetCache __instance, string path, ref Resource __result)
	{
		if (path != ModInfo.OrnnsForgePortraitRequestPath)
		{
			return true;
		}

		Texture2D? texture = HeartsteelTextureLoader.LoadPortableTexture(ModInfo.OrnnsForgePortraitPath);
		if (texture == null)
		{
			return true;
		}

		__instance.SetAsset(path, texture);
		__result = texture;
		return false;
	}
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Icon), MethodType.Getter)]
internal static class RelicModel_Icon_Patch
{
	[HarmonyPostfix]
	private static void Postfix(RelicModel __instance, ref Texture2D __result)
	{
		if (!HeartsteelTextureLoader.TryGetHeartsteelRelicTexture(__instance, out Texture2D? texture))
		{
			return;
		}

		__result = texture!;
	}
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.BigIcon), MethodType.Getter)]
internal static class RelicModel_BigIcon_Patch
{
	[HarmonyPostfix]
	private static void Postfix(RelicModel __instance, ref Texture2D __result)
	{
		if (!HeartsteelTextureLoader.TryGetHeartsteelRelicTexture(__instance, out Texture2D? texture))
		{
			return;
		}

		__result = texture!;
	}
}

[HarmonyPatch(typeof(NRelic), "Reload")]
internal static class NRelic_Reload_Patch
{
	private static readonly FieldInfo RelicModelField =
		typeof(NRelic).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NRelic._model.");

	[HarmonyPostfix]
	private static void Postfix(NRelic __instance)
	{
		if (!__instance.IsNodeReady())
		{
			return;
		}

		if (RelicModelField.GetValue(__instance) is not RelicModel model
			|| !HeartsteelTextureLoader.TryGetHeartsteelRelicTexture(model, out Texture2D? texture))
		{
			return;
		}

		model.UpdateTexture(__instance.Icon);
		__instance.Icon.Texture = texture;
		__instance.Outline.Visible = false;
	}
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.Icon), MethodType.Getter)]
internal static class PowerModel_Icon_Patch
{
	[HarmonyPostfix]
	private static void Postfix(PowerModel __instance, ref Texture2D __result)
	{
		Texture2D? texture = HeartsteelTextureLoader.TryGetHeartsteelPowerTexture(__instance);
		if (texture == null)
		{
			return;
		}

		__result = texture;
	}
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.BigIcon), MethodType.Getter)]
internal static class PowerModel_BigIcon_Patch
{
	[HarmonyPostfix]
	private static void Postfix(PowerModel __instance, ref Texture2D __result)
	{
		Texture2D? texture = HeartsteelTextureLoader.TryGetHeartsteelPowerTexture(__instance);
		if (texture == null)
		{
			return;
		}

		__result = texture;
	}
}

[HarmonyPatch(typeof(NPower), "Reload")]
internal static class NPower_Reload_Patch
{
	private static readonly FieldInfo PowerModelField =
		typeof(NPower).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._model.");

	private static readonly FieldInfo PowerIconField =
		typeof(NPower).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._icon.");

	private static readonly FieldInfo PowerFlashField =
		typeof(NPower).GetField("_powerFlash", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._powerFlash.");

	[HarmonyPostfix]
	private static void Postfix(NPower __instance)
	{
		if (!__instance.IsNodeReady())
		{
			return;
		}

		if (PowerModelField.GetValue(__instance) is not PowerModel model)
		{
			return;
		}

		Texture2D? texture = HeartsteelTextureLoader.TryGetHeartsteelPowerTexture(model);
		if (texture == null)
		{
			return;
		}

		((TextureRect)PowerIconField.GetValue(__instance)!).Texture = texture;
		((CpuParticles2D)PowerFlashField.GetValue(__instance)!).Texture = texture;
	}
}

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
internal static class ModelDb_AllSharedEvents_Patch
{
	[HarmonyPostfix]
	private static void Postfix(ref IEnumerable<EventModel> __result)
	{
		__result = __result.Append(ModelDb.Event<OrnnsForge>()).Distinct();
	}
}

internal static class HeartsteelTextureLoader
{
	private static readonly Dictionary<string, PortableCompressedTexture2D> ManualTextureCache = [];

	internal static Texture2D? TryGetHeartsteelPowerTexture(PowerModel self)
	{
		if (self.Id != ModelDb.GetId<HeartsteelDevourPower>())
		{
			return null;
		}

		return LoadPortableTexture(ModInfo.PowerIconPath);
	}

	internal static bool TryGetHeartsteelRelicTexture(RelicModel self, out Texture2D? texture)
	{
		texture = null;
		if (self.Id != ModelDb.GetId<HeartsteelRelic>())
		{
			return false;
		}

		texture = LoadPortableTexture(ModInfo.RelicIconPath);
		return texture != null;
	}

	internal static PortableCompressedTexture2D? LoadPortableTexture(string path)
	{
		if (ManualTextureCache.TryGetValue(path, out PortableCompressedTexture2D? cachedTexture))
		{
			return cachedTexture;
		}

		byte[] bytes = Godot.FileAccess.GetFileAsBytes(path);
		if (bytes.Length == 0)
		{
			return null;
		}

		Image image = new();
		Error err = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
			? image.LoadPngFromBuffer(bytes)
			: image.LoadJpgFromBuffer(bytes);
		if (err != Error.Ok)
		{
			return null;
		}

		PortableCompressedTexture2D texture = new();
		texture.CreateFromImage(image, PortableCompressedTexture2D.CompressionMode.Lossless);
		ManualTextureCache[path] = texture;
		return texture;
	}
}
