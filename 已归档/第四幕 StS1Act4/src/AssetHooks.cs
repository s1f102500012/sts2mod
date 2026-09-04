using System;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MonoMod.RuntimeDetour;

namespace StS1Act4;

internal static class AssetHooks
{
	private static readonly MegaCrit.Sts2.Core.Logging.Logger Logger = new("StS1Act4.AssetHooks", LogType.Generic);

	private static Hook? _assetLoadHook;

	private static Hook? _loadActAssetsHook;

	private static Hook? _mapTopPathHook;

	private static Hook? _mapMidPathHook;

	private static Hook? _mapBotPathHook;

	private static Hook? _actAssetPathsHook;

	private static Hook? _topBarRoomIconUpdateHook;

	private static Hook? _powerIconHook;

	private static Hook? _powerBigIconHook;

	private static Hook? _combatPowerReloadHook;

	private static Hook? _creatureStateDisplayBoundsHook;

	private static Hook? _bossMapPointReadyHook;

	private static readonly FieldInfo CombatPowerModelField = typeof(NPower).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._model.");
	private static readonly FieldInfo CombatPowerIconField = typeof(NPower).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._icon.");
	private static readonly FieldInfo CombatPowerFlashField = typeof(NPower).GetField("_powerFlash", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NPower._powerFlash.");
	private static readonly FieldInfo CreatureStateDisplayCreatureField = typeof(NCreatureStateDisplay).GetField("_creature", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NCreatureStateDisplay._creature.");
	private static readonly FieldInfo CreatureStateDisplayOriginalPositionField = typeof(NCreatureStateDisplay).GetField("_originalPosition", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NCreatureStateDisplay._originalPosition.");
	private static readonly FieldInfo CreatureStateDisplayShowHideTweenField = typeof(NCreatureStateDisplay).GetField("_showHideTween", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NCreatureStateDisplay._showHideTween.");
	private static readonly FieldInfo BossMapPointActField = typeof(NBossMapPoint).GetField("_act", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access NBossMapPoint._act.");

	private static readonly MethodInfo LoadAssetSetsMethod = typeof(PreloadManager).GetMethod("LoadAssetSets", BindingFlags.Static | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("Could not access PreloadManager.LoadAssetSets.");

	private delegate Resource OrigLoadAsset(AssetCache self, string path);

	private delegate Task OrigLoadActAssets(ActModel act);

	private delegate string OrigGetActPath(ActModel self);

	private delegate IEnumerable<string> OrigGetActAssetPaths(ActModel self);

	private delegate void OrigUpdateTopBarRoomIcon(NTopBarRoomIcon self);

	private delegate Texture2D OrigGetPowerIcon(PowerModel self);

	private delegate Texture2D OrigGetPowerBigIcon(PowerModel self);

	private delegate void OrigCombatPowerReload(NPower self);

	private delegate void OrigSetCreatureBounds(NCreatureStateDisplay self, Control bounds);

	private delegate void OrigBossMapPointReady(NBossMapPoint self);

	public static void Install()
	{
		MethodInfo loadAsset = RequireMethod(typeof(AssetCache), "LoadAsset", BindingFlags.Instance | BindingFlags.NonPublic, typeof(string));
		MethodInfo loadActAssets = RequireMethod(typeof(PreloadManager), nameof(PreloadManager.LoadActAssets), BindingFlags.Public | BindingFlags.Static, typeof(ActModel));
		MethodInfo getMapTopPath = RequireGetter(typeof(ActModel), nameof(ActModel.MapTopBgPath));
		MethodInfo getMapMidPath = RequireGetter(typeof(ActModel), nameof(ActModel.MapMidBgPath));
		MethodInfo getMapBotPath = RequireGetter(typeof(ActModel), nameof(ActModel.MapBotBgPath));
		MethodInfo getActAssetPaths = RequireGetter(typeof(ActModel), nameof(ActModel.AssetPaths));
		MethodInfo updateTopBarRoomIcon = RequireMethod(typeof(NTopBarRoomIcon), "UpdateIcon", BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo getPowerIcon = RequireGetter(typeof(PowerModel), nameof(PowerModel.Icon));
		MethodInfo getPowerBigIcon = RequireGetter(typeof(PowerModel), nameof(PowerModel.BigIcon));
		MethodInfo combatPowerReload = RequireMethod(typeof(NPower), "Reload", BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo setCreatureBounds = RequireMethod(typeof(NCreatureStateDisplay), nameof(NCreatureStateDisplay.SetCreatureBounds), BindingFlags.Instance | BindingFlags.Public, typeof(Control));
		MethodInfo bossMapPointReady = RequireMethod(typeof(NBossMapPoint), nameof(NBossMapPoint._Ready), BindingFlags.Instance | BindingFlags.Public);

		_assetLoadHook = new Hook(loadAsset, LoadAssetDetour);
		_loadActAssetsHook = new Hook(loadActAssets, LoadActAssetsDetour);
		_mapTopPathHook = new Hook(getMapTopPath, MapTopBgPathDetour);
		_mapMidPathHook = new Hook(getMapMidPath, MapMidBgPathDetour);
		_mapBotPathHook = new Hook(getMapBotPath, MapBotBgPathDetour);
		_actAssetPathsHook = new Hook(getActAssetPaths, ActAssetPathsDetour);
		_topBarRoomIconUpdateHook = new Hook(updateTopBarRoomIcon, UpdateTopBarRoomIconDetour);
		_powerIconHook = new Hook(getPowerIcon, PowerIconDetour);
		_powerBigIconHook = new Hook(getPowerBigIcon, PowerBigIconDetour);
		_combatPowerReloadHook = new Hook(combatPowerReload, CombatPowerReloadDetour);
		_creatureStateDisplayBoundsHook = new Hook(setCreatureBounds, SetCreatureBoundsDetour);
		_bossMapPointReadyHook = new Hook(bossMapPointReady, BossMapPointReadyDetour);
	}

	private static Resource LoadAssetDetour(OrigLoadAsset orig, AssetCache self, string path)
	{
		if (TryCreateResourceForPath(path, out Resource? resource))
		{
			self.SetAsset(path, resource);
			return resource;
		}

		return orig(self, path);
	}

	private static Task LoadActAssetsDetour(OrigLoadActAssets orig, ActModel act)
	{
		if (!IsAct4(act))
		{
			return orig(act);
		}

		Logger.Info($"Preloading act assets for {act.Id.Entry} with deterministic asset path overrides.");
		return LoadActAssetsManually(act);
	}

	private static string MapTopBgPathDetour(OrigGetActPath orig, ActModel self)
	{
		return IsAct4(self) ? ModelDb.Act<Overgrowth>().MapTopBgPath : orig(self);
	}

	private static string MapMidBgPathDetour(OrigGetActPath orig, ActModel self)
	{
		return IsAct4(self) ? ModelDb.Act<Overgrowth>().MapMidBgPath : orig(self);
	}

	private static string MapBotBgPathDetour(OrigGetActPath orig, ActModel self)
	{
		return IsAct4(self) ? ModelDb.Act<Overgrowth>().MapBotBgPath : orig(self);
	}

	private static IEnumerable<string> ActAssetPathsDetour(OrigGetActAssetPaths orig, ActModel self)
	{
		IEnumerable<string> paths = orig(self);
		if (!IsAct4(self))
		{
			return paths;
		}

		return paths.Where(path =>
			path != "res://images/map/placeholder/corruptheartencounter_icon.png" &&
			path != "res://images/map/placeholder/corruptheartencounter_icon_outline.png");
	}

	private static bool TryCreateResourceForPath(string path, out Resource? resource)
	{
		resource = path switch
		{
			"res://scenes/backgrounds/sts1_act4/sts1_act4_background.tscn" => ResourceLoader.Load<Resource>("res://scenes/backgrounds/overgrowth/overgrowth_background.tscn"),
			"res://scenes/backgrounds/sts1_act4/layers/sts1_act4_bg_00_a.tscn" => ResourceLoader.Load<Resource>("res://scenes/backgrounds/overgrowth/layers/overgrowth_bg_00_a.tscn"),
			"res://scenes/backgrounds/sts1_act4/layers/sts1_act4_bg_01_a.tscn" => ResourceLoader.Load<Resource>("res://scenes/backgrounds/overgrowth/layers/overgrowth_bg_01_a.tscn"),
			"res://scenes/backgrounds/sts1_act4/layers/sts1_act4_bg_02_a.tscn" => ResourceLoader.Load<Resource>("res://scenes/backgrounds/overgrowth/layers/overgrowth_bg_02_a.tscn"),
			"res://scenes/backgrounds/sts1_act4/layers/sts1_act4_bg_03_a.tscn" => ResourceLoader.Load<Resource>("res://scenes/backgrounds/overgrowth/layers/overgrowth_bg_03_a.tscn"),
			"res://scenes/backgrounds/sts1_act4/layers/sts1_act4_bg_04_a.tscn" => ResourceLoader.Load<Resource>("res://scenes/backgrounds/overgrowth/layers/overgrowth_bg_04_a.tscn"),
			"res://scenes/backgrounds/sts1_act4/layers/sts1_act4_fg_a.tscn" => ResourceLoader.Load<Resource>("res://scenes/backgrounds/overgrowth/layers/overgrowth_fg_a.tscn"),
			"res://images/map/placeholder/corruptheartencounter_icon" => LoadPortableTexture("res://images/map/placeholder/corruptheartencounter_icon.png"),
			"res://images/map/placeholder/corruptheartencounter_icon_outline" => LoadPortableTexture("res://images/map/placeholder/corruptheartencounter_icon_outline.png"),
			"res://images/map/placeholder/corruptheartencounter_icon.png" => LoadPortableTexture(path),
			"res://images/map/placeholder/corruptheartencounter_icon_outline.png" => LoadPortableTexture(path),
			"res://images/ui/run_history/corrupt_heart_encounter.png" => LoadPortableTexture(path),
			"res://images/ui/run_history/corrupt_heart_encounter_outline.png" => LoadPortableTexture(path),
			"res://images/ui/run_history/spire_shield_and_spear.png" => LoadPortableTexture(path),
			"res://images/ui/run_history/spire_shield_and_spear_outline.png" => LoadPortableTexture(path),
			"res://images/packed/map/map_bgs/sts1_act4/map_top_sts1_act4.png" => ResourceLoader.Load<Resource>("res://images/packed/map/map_bgs/overgrowth/map_top_overgrowth.png"),
			"res://images/packed/map/map_bgs/sts1_act4/map_middle_sts1_act4.png" => ResourceLoader.Load<Resource>("res://images/packed/map/map_bgs/overgrowth/map_middle_overgrowth.png"),
			"res://images/packed/map/map_bgs/sts1_act4/map_bottom_sts1_act4.png" => ResourceLoader.Load<Resource>("res://images/packed/map/map_bgs/overgrowth/map_bottom_overgrowth.png"),
			"res://images/atlases/power_atlas.sprites/act4_beat_of_death_power.tres" => LoadPortableTexture("res://StS1Act4/extracted/act4_beat_of_death_power.png"),
			"res://images/atlases/power_atlas.sprites/act4_invincible_power.tres" => LoadPortableTexture("res://StS1Act4/extracted/act4_invincible_power.png"),
			"res://images/atlases/power_atlas.sprites/act4_painful_stabs_power.tres" => LoadPortableTexture("res://StS1Act4/extracted/act4_painful_stabs_power.png"),
			"res://images/powers/act4_beat_of_death_power.png" => LoadPortableTexture("res://StS1Act4/extracted/act4_beat_of_death_power.png"),
			"res://images/powers/act4_invincible_power.png" => LoadPortableTexture("res://StS1Act4/extracted/act4_invincible_power.png"),
			"res://images/powers/act4_painful_stabs_power.png" => LoadPortableTexture("res://StS1Act4/extracted/act4_painful_stabs_power.png"),
			_ => null
		};

		return resource != null;
	}

	private static PortableCompressedTexture2D? LoadPortableTexture(string path)
	{
		byte[] bytes = Godot.FileAccess.GetFileAsBytes(path);
		if (bytes.Length == 0)
		{
			Logger.Warn("Missing raw texture bytes for " + path);
			return null;
		}

		Image image = new();
		Error err = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
			? image.LoadJpgFromBuffer(bytes)
			: image.LoadPngFromBuffer(bytes);
		if (err != Error.Ok)
		{
			Logger.Warn($"Failed decoding raw texture {path}: {err}");
			return null;
		}

		PortableCompressedTexture2D texture = new();
		texture.CreateFromImage(image, PortableCompressedTexture2D.CompressionMode.Lossless);
		return texture;
	}

	private static bool IsAct4(ActModel act)
	{
		return act.Id == ModelDb.GetId<Sts1Act4>();
	}

	private static void UpdateTopBarRoomIconDetour(OrigUpdateTopBarRoomIcon orig, NTopBarRoomIcon self)
	{
		FieldInfo runStateField = typeof(NTopBarRoomIcon).GetField("_runState", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Could not access NTopBarRoomIcon._runState.");
		FieldInfo roomIconField = typeof(NTopBarRoomIcon).GetField("_roomIcon", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Could not access NTopBarRoomIcon._roomIcon.");
		FieldInfo roomIconOutlineField = typeof(NTopBarRoomIcon).GetField("_roomIconOutline", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Could not access NTopBarRoomIcon._roomIconOutline.");

		if (runStateField.GetValue(self) is not RunState runState || !IsAct4(runState.Act) || runState.CurrentRoom == null)
		{
			orig(self);
			return;
		}

		TextureRect roomIcon = (TextureRect)roomIconField.GetValue(self)!;
		TextureRect roomIconOutline = (TextureRect)roomIconOutlineField.GetValue(self)!;

		if (runState.CurrentRoom is not CombatRoom combatRoom)
		{
			if (TryApplyAct4RoomIcon(runState, roomIcon, roomIconOutline))
			{
				return;
			}

			orig(self);
			return;
		}

		if (combatRoom.Encounter.Id == ModelDb.GetId<CorruptHeartEncounter>() || combatRoom.Encounter.Id == ModelDb.GetId<SpireShieldAndSpear>())
		{
			TryApplyAct4RoomIcon(runState, roomIcon, roomIconOutline);
			return;
		}

		orig(self);
	}

	private static bool TryApplyAct4RoomIcon(RunState runState, TextureRect roomIcon, TextureRect roomIconOutline)
	{
		MapPointType pointType = runState.CurrentMapPoint?.PointType ?? MapPointType.Unassigned;
		switch (pointType)
		{
			case MapPointType.Elite:
				AssignRoomIcon(
					roomIcon,
					roomIconOutline,
					"res://images/ui/run_history/spire_shield_and_spear.png",
					"res://images/ui/run_history/spire_shield_and_spear_outline.png");
				return true;
			case MapPointType.Boss:
				AssignRoomIcon(
					roomIcon,
					roomIconOutline,
					"res://images/ui/run_history/corrupt_heart_encounter.png",
					"res://images/ui/run_history/corrupt_heart_encounter_outline.png");
				return true;
			default:
				return false;
		}
	}

	private static void AssignRoomIcon(TextureRect roomIcon, TextureRect roomIconOutline, string iconPath, string outlinePath)
	{
		roomIcon.Visible = true;
		roomIcon.Texture = LoadPortableTexture(iconPath);
		roomIconOutline.Visible = true;
		roomIconOutline.Texture = LoadPortableTexture(outlinePath);
	}

	private static void BossMapPointReadyDetour(OrigBossMapPointReady orig, NBossMapPoint self)
	{
		orig(self);

		if (BossMapPointActField.GetValue(self) is not ActModel act || !IsAct4(act))
		{
			return;
		}

		TextureRect? placeholderImage = self.GetNodeOrNull<TextureRect>("%PlaceholderImage");
		TextureRect? placeholderOutline = self.GetNodeOrNull<TextureRect>("%PlaceholderOutline");
		if (placeholderImage == null || placeholderOutline == null)
		{
			return;
		}

		placeholderImage.Visible = true;
		placeholderOutline.Visible = true;
		placeholderImage.Texture = LoadPortableTexture("res://images/map/placeholder/corruptheartencounter_icon.png");
		placeholderOutline.Texture = LoadPortableTexture("res://images/map/placeholder/corruptheartencounter_icon_outline.png");
		placeholderImage.SelfModulate = act.MapUntraveledColor;
		placeholderOutline.SelfModulate = act.MapBgColor;
	}

	private static Texture2D PowerIconDetour(OrigGetPowerIcon orig, PowerModel self)
	{
		Texture2D? texture = TryGetAct4PowerTexture(self);
		if (texture != null)
		{
			return texture;
		}

		return orig(self);
	}

	private static Texture2D PowerBigIconDetour(OrigGetPowerBigIcon orig, PowerModel self)
	{
		Texture2D? texture = TryGetAct4PowerTexture(self);
		if (texture != null)
		{
			return texture;
		}

		return orig(self);
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

		Texture2D? texture = TryGetAct4PowerTexture(model);
		if (texture == null)
		{
			return;
		}

		((TextureRect)CombatPowerIconField.GetValue(self)!).Texture = texture;
		((CpuParticles2D)CombatPowerFlashField.GetValue(self)!).Texture = texture;
	}

	private static void SetCreatureBoundsDetour(OrigSetCreatureBounds orig, NCreatureStateDisplay self, Control bounds)
	{
		orig(self, bounds);

		if (CreatureStateDisplayCreatureField.GetValue(self) is not Creature creature
			|| creature.Monster == null
			|| !IsAct4Monster(creature.Monster))
		{
			return;
		}

		Vector2 position = self.Position;
		// Shield's bounds sit higher than Spear's, so keep its HP bar nudged down to the same baseline.
		float extraYOffset = creature.Monster.Id == ModelDb.GetId<SpireShield>() ? 0f : 0f;
		position.Y = bounds.Position.Y + bounds.Size.Y + 8f + extraYOffset;
		self.Position = position;
		CreatureStateDisplayOriginalPositionField.SetValue(self, position);

		if (CreatureStateDisplayShowHideTweenField.GetValue(self) is Tween tween && tween.IsValid() && tween.IsRunning())
		{
			tween.Kill();
			self.Visible = true;
			self.Modulate = Colors.White;
		}
	}

	private static async Task LoadActAssetsManually(ActModel act)
	{
		HashSet<string> filteredActAssets = new(act.AssetPaths.Where(path =>
			path != "res://images/map/placeholder/corruptheartencounter_icon.png" &&
			path != "res://images/map/placeholder/corruptheartencounter_icon_outline.png"));
		AssetSets.Act = filteredActAssets;
		var task = (Task<AssetLoadingSession>)LoadAssetSetsMethod.Invoke(null, new object[] { "Act=" + act.Id.Entry, new IEnumerable<string>[] { AssetSets.CommonAssets, AssetSets.RunSet, AssetSets.Act } })!;
		AssetLoadingSession session = await task;
		await session.WaitForCompletion();
		GC.Collect();
	}

	private static Texture2D? TryGetAct4PowerTexture(PowerModel self)
	{
		if (self.Id == ModelDb.GetId<Act4BeatOfDeathPower>())
		{
			return LoadPortableTexture("res://StS1Act4/extracted/act4_beat_of_death_power.png");
		}

		if (self.Id == ModelDb.GetId<Act4InvinciblePower>())
		{
			return LoadPortableTexture("res://StS1Act4/extracted/act4_invincible_power.png");
		}

		if (self.Id == ModelDb.GetId<Act4PainfulStabsPower>())
		{
			return LoadPortableTexture("res://StS1Act4/extracted/act4_painful_stabs_power.png");
		}

		return null;
	}

	private static bool IsAct4Monster(MonsterModel monster)
	{
		ModelId id = monster.Id;
		return id == ModelDb.GetId<SpireShield>()
			|| id == ModelDb.GetId<SpireSpear>()
			|| id == ModelDb.GetId<CorruptHeart>();
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
