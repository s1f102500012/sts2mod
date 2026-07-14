using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace CustomDifficulty;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeSingleplayer))]
internal static class CharacterSelectSingleplayerPatch
{
	private static void Postfix(NCharacterSelectScreen __instance)
	{
		CustomDifficultySync.Register(__instance.Lobby.NetService);
		CustomDifficultyPanel.Inject(__instance);
	}
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeMultiplayerAsHost), typeof(INetGameService), typeof(int))]
internal static class CharacterSelectHostPatch
{
	private static void Postfix(NCharacterSelectScreen __instance, INetGameService gameService)
	{
		CustomDifficultySync.Register(gameService);
		CustomDifficultyPanel.Inject(__instance);
		CustomDifficultySync.BroadcastSettings();
	}
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeMultiplayerAsClient), typeof(INetGameService), typeof(ClientLobbyJoinResponseMessage))]
internal static class CharacterSelectClientPatch
{
	private static void Postfix(NCharacterSelectScreen __instance, INetGameService gameService)
	{
		CustomDifficultySync.Register(gameService);
		CustomDifficultyPanel.Inject(__instance);
		CustomDifficultySync.RequestHostSettings();
	}
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready))]
internal static class CharacterSelectReadyPatch
{
	private static void Postfix(NCharacterSelectScreen __instance)
	{
		try
		{
			if (__instance.Lobby?.NetService != null)
			{
				CustomDifficultySync.Register(__instance.Lobby.NetService);
				CustomDifficultyPanel.Inject(__instance);
			}
		}
		catch
		{
		}
	}
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.PlayerConnected))]
internal static class CharacterSelectPlayerConnectedPatch
{
	private static void Postfix(LobbyPlayer player)
	{
		Log.Debug($"[{ModInfo.Id}] Player connected: {player.id}; resending host difficulty.");
		CustomDifficultySync.BroadcastSettings();
		CustomDifficultyPanel.Refresh();
	}
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class RunLaunchPatch
{
	private static void Postfix()
	{
		if (RunManager.Instance?.NetService == null)
		{
			return;
		}

		CustomDifficultySync.Register(RunManager.Instance.NetService);
		CustomDifficultySync.BroadcastSettings();
		CustomDifficultySync.RequestHostSettings();
	}
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class RunCleanUpPatch
{
	private static void Postfix()
	{
		CustomDifficultySync.Unregister();
	}
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.InitProfileId))]
internal static class SaveManagerInitProfilePatch
{
	private static void Postfix()
	{
		CustomDifficultyStorage.LoadCurrentProfile();
	}
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SwitchProfileId))]
internal static class SaveManagerSwitchProfilePatch
{
	private static void Postfix()
	{
		CustomDifficultyStorage.LoadCurrentProfile();
	}
}

[HarmonyPatch(typeof(Creature), nameof(Creature.ScaleMonsterHpForMultiplayer))]
internal static class MonsterScalingPatch
{
	private static void Postfix(Creature __instance)
	{
		if (!__instance.IsMonster)
		{
			return;
		}

		int floorIndex = GetEffectiveFloorIndex();
		ApplyHpMultiplier(__instance, floorIndex);
		ApplyAttackMultiplierPower(__instance, floorIndex);
	}

	// 递进模式的房间计数：RunState.TotalFloor（地图历史条目总数，联机两端一致、读档自恢复）
	// + 无尽模式往轮累计楼层（未装 EndlessMode 时为 0）。
	private static int GetEffectiveFloorIndex()
	{
		int totalFloor = 0;
		try
		{
			if (RunManager.Instance?.DebugOnlyGetState() is RunState state)
			{
				totalFloor = Math.Max(0, state.TotalFloor);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to read TotalFloor: {ex.Message}");
		}

		return totalFloor + EndlessModeCompat.GetFloorsBeforeCurrentLoop();
	}

	private static void ApplyHpMultiplier(Creature creature, int floorIndex)
	{
		decimal multiplier = CustomDifficultySettings.GetHpMultiplierForFloor(floorIndex);
		if (multiplier == 1m)
		{
			return;
		}

		int scaledHp = Math.Max(1, (int)Math.Round(creature.MaxHp * multiplier, MidpointRounding.AwayFromZero));
		creature.SetMaxHpInternal(scaledHp);
		creature.SetCurrentHpInternal(scaledHp);
		Log.Debug($"[{ModInfo.Id}] {creature.Name} HP scaled to {scaledHp} (mode={CustomDifficultySettings.Mode} floor={floorIndex} x{multiplier:0.00}).");
	}

	private static void ApplyAttackMultiplierPower(Creature creature, int floorIndex)
	{
		decimal multiplier = CustomDifficultySettings.GetAttackMultiplierForFloor(floorIndex);
		if (multiplier == 1m || creature.HasPower<MonsterAttackScalePower>())
		{
			return;
		}

		try
		{
			MonsterAttackScalePower power = (MonsterAttackScalePower)ModelDb.Power<MonsterAttackScalePower>().ToMutable();
			power.ApplyInternal(creature, MonsterAttackScalePower.EncodeMultiplierPercent(multiplier), silent: true);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] Failed to apply MonsterAttackScalePower: {ex.Message}");
		}
	}
}
