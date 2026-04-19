using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MonoMod.RuntimeDetour;

namespace KeystoneRunes;

internal static class AssetHooks
{
	private static readonly Dictionary<string, PortableCompressedTexture2D> ManualTextureCache = new();

	private static readonly FieldInfo NRelicModelField = typeof(NRelic).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NRelic._model.");

	private static Hook? _relicIconHook;
	private static Hook? _relicBigIconHook;
	private static Hook? _relicReloadHook;

	private delegate Texture2D OrigGetRelicIcon(RelicModel self);

	private delegate Texture2D OrigGetRelicBigIcon(RelicModel self);

	private delegate void OrigNRelicReload(NRelic self);

	public static void Install()
	{
		MethodInfo getRelicIcon = RequireGetter(typeof(RelicModel), nameof(RelicModel.Icon));
		MethodInfo getRelicBigIcon = RequireGetter(typeof(RelicModel), nameof(RelicModel.BigIcon));
		MethodInfo relicReload = RequireMethod(typeof(NRelic), "Reload", BindingFlags.Instance | BindingFlags.NonPublic);

		_relicIconHook = new Hook(getRelicIcon, RelicIconDetour);
		_relicBigIconHook = new Hook(getRelicBigIcon, RelicBigIconDetour);
		_relicReloadHook = new Hook(relicReload, NRelicReloadDetour);
	}

	private static Texture2D RelicIconDetour(OrigGetRelicIcon orig, RelicModel self)
	{
		return TryGetKeystoneRelicTexture(self, out Texture2D? texture) ? texture! : orig(self);
	}

	private static Texture2D RelicBigIconDetour(OrigGetRelicBigIcon orig, RelicModel self)
	{
		return TryGetKeystoneRelicTexture(self, out Texture2D? texture) ? texture! : orig(self);
	}

	private static void NRelicReloadDetour(OrigNRelicReload orig, NRelic self)
	{
		if (!self.IsNodeReady()
			|| NRelicModelField.GetValue(self) is not RelicModel model
			|| !TryGetKeystoneRelicTexture(model, out Texture2D? texture))
		{
			orig(self);
			return;
		}

		model.UpdateTexture(self.Icon);
		self.Icon.Texture = texture;
		self.Outline.Visible = false;
	}

	private static bool TryGetKeystoneRelicTexture(RelicModel self, out Texture2D? texture)
	{
		texture = null;
		string? path = ModInfo.TryGetRelicIconPath(self);
		if (path == null)
		{
			return false;
		}

		texture = LoadPortableTexture(path);
		return texture != null;
	}

	private static PortableCompressedTexture2D? LoadPortableTexture(string path)
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

	private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, params Type[] parameterTypes)
	{
		return type.GetMethod(name, flags, binder: null, parameterTypes, modifiers: null)
			?? throw new InvalidOperationException($"Could not find method {type.FullName}.{name}.");
	}

	private static MethodInfo RequireGetter(Type type, string propertyName)
	{
		return type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetMethod
			?? throw new InvalidOperationException($"Could not find property getter {type.FullName}.{propertyName}.");
	}
}
