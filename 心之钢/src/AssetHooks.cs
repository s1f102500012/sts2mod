using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MonoMod.RuntimeDetour;

namespace Heartsteel;

internal static class AssetHooks
{
	private static readonly Dictionary<string, PortableCompressedTexture2D> ManualTextureCache = new();

	private static Hook? _relicIconHook;
	private static Hook? _relicBigIconHook;
	private static Hook? _relicReloadHook;
	private static Hook? _powerIconHook;
	private static Hook? _powerBigIconHook;
	private static Hook? _combatPowerReloadHook;

	private static readonly FieldInfo NRelicModelField = typeof(NRelic).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NRelic._model.");

	private static readonly FieldInfo CombatPowerModelField = typeof(NPower).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._model.");

	private static readonly FieldInfo CombatPowerIconField = typeof(NPower).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._icon.");

	private static readonly FieldInfo CombatPowerFlashField = typeof(NPower).GetField("_powerFlash", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._powerFlash.");

	private delegate Texture2D OrigGetRelicIcon(RelicModel self);

	private delegate Texture2D OrigGetRelicBigIcon(RelicModel self);

	private delegate void OrigNRelicReload(NRelic self);

	private delegate Texture2D OrigGetPowerIcon(PowerModel self);

	private delegate Texture2D OrigGetPowerBigIcon(PowerModel self);

	private delegate void OrigCombatPowerReload(NPower self);

	public static void Install()
	{
		MethodInfo getRelicIcon = RequireGetter(typeof(RelicModel), nameof(RelicModel.Icon));
		MethodInfo getRelicBigIcon = RequireGetter(typeof(RelicModel), nameof(RelicModel.BigIcon));
		MethodInfo relicReload = RequireMethod(typeof(NRelic), "Reload", BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo getPowerIcon = RequireGetter(typeof(PowerModel), nameof(PowerModel.Icon));
		MethodInfo getPowerBigIcon = RequireGetter(typeof(PowerModel), nameof(PowerModel.BigIcon));
		MethodInfo combatPowerReload = RequireMethod(typeof(NPower), "Reload", BindingFlags.Instance | BindingFlags.NonPublic);

		_relicIconHook = new Hook(getRelicIcon, RelicIconDetour);
		_relicBigIconHook = new Hook(getRelicBigIcon, RelicBigIconDetour);
		_relicReloadHook = new Hook(relicReload, NRelicReloadDetour);
		_powerIconHook = new Hook(getPowerIcon, PowerIconDetour);
		_powerBigIconHook = new Hook(getPowerBigIcon, PowerBigIconDetour);
		_combatPowerReloadHook = new Hook(combatPowerReload, CombatPowerReloadDetour);
	}

	private static Texture2D RelicIconDetour(OrigGetRelicIcon orig, RelicModel self)
	{
		return TryGetHeartsteelRelicTexture(self, out Texture2D? texture) ? texture! : orig(self);
	}

	private static Texture2D RelicBigIconDetour(OrigGetRelicBigIcon orig, RelicModel self)
	{
		return TryGetHeartsteelRelicTexture(self, out Texture2D? texture) ? texture! : orig(self);
	}

	private static void NRelicReloadDetour(OrigNRelicReload orig, NRelic self)
	{
		if (!self.IsNodeReady()
			|| NRelicModelField.GetValue(self) is not RelicModel model
			|| !TryGetHeartsteelRelicTexture(model, out Texture2D? texture))
		{
			orig(self);
			return;
		}

		model.UpdateTexture(self.Icon);
		self.Icon.Texture = texture;
		self.Outline.Visible = false;
	}

	private static Texture2D PowerIconDetour(OrigGetPowerIcon orig, PowerModel self)
	{
		Texture2D? texture = TryGetHeartsteelPowerTexture(self);
		return texture ?? orig(self);
	}

	private static Texture2D PowerBigIconDetour(OrigGetPowerBigIcon orig, PowerModel self)
	{
		Texture2D? texture = TryGetHeartsteelPowerTexture(self);
		return texture ?? orig(self);
	}

	private static void CombatPowerReloadDetour(OrigCombatPowerReload orig, NPower self)
	{
		orig(self);

		if (!self.IsNodeReady())
		{
			return;
		}

		if (CombatPowerModelField.GetValue(self) is not PowerModel model)
		{
			return;
		}

		Texture2D? texture = TryGetHeartsteelPowerTexture(model);
		if (texture == null)
		{
			return;
		}

		((TextureRect)CombatPowerIconField.GetValue(self)!).Texture = texture;
		((CpuParticles2D)CombatPowerFlashField.GetValue(self)!).Texture = texture;
	}

	private static Texture2D? TryGetHeartsteelPowerTexture(PowerModel self)
	{
		if (self.Id != ModelDb.GetId<HeartsteelDevourPower>())
		{
			return null;
		}

		return LoadPortableTexture(ModInfo.PowerIconPath);
	}

	private static bool TryGetHeartsteelRelicTexture(RelicModel self, out Texture2D? texture)
	{
		texture = null;
		if (self.Id != ModelDb.GetId<HeartsteelRelic>())
		{
			return false;
		}

		texture = LoadPortableTexture(ModInfo.RelicIconPath);
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
