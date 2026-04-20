using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MonoMod.RuntimeDetour;

namespace StS1Act4;

internal static class Act4SteamHooks
{
	private static readonly MegaCrit.Sts2.Core.Logging.Logger Logger = new("StS1Act4.Steam", LogType.Generic);
	// Presence writes are confirmed in godot.log; if Steam still shows nothing, treat it as a client-side display issue.

	private static Hook? _updateRichPresenceHook;

	private static Hook? _enterActHook;

	private static Hook? _enterRoomDebugHook;

	private delegate void OrigUpdateRichPresence(RunManager self);

	private delegate Task OrigEnterAct(RunManager self, int currentActIndex, bool doTransition);

	private delegate Task<AbstractRoom> OrigEnterRoomDebug(RunManager self, RoomType roomType, MegaCrit.Sts2.Core.Map.MapPointType pointType, AbstractModel? model, bool showTransition);

	public static void Install()
	{
		MethodInfo updateRichPresence = typeof(RunManager).GetMethod("UpdateRichPresence", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Could not find RunManager.UpdateRichPresence.");
		MethodInfo enterAct = typeof(RunManager).GetMethod(nameof(RunManager.EnterAct), BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("Could not find RunManager.EnterAct.");
		MethodInfo enterRoomDebug = typeof(RunManager).GetMethod(nameof(RunManager.EnterRoomDebug), BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("Could not find RunManager.EnterRoomDebug.");
		_updateRichPresenceHook = new Hook(updateRichPresence, UpdateRichPresenceDetour);
		_enterActHook = new Hook(enterAct, EnterActDetour);
		_enterRoomDebugHook = new Hook(enterRoomDebug, EnterRoomDebugDetour);
	}

	private static void UpdateRichPresenceDetour(OrigUpdateRichPresence orig, RunManager self)
	{
		orig(self);
		RefreshForCurrentRun(self);
	}

	private static async Task EnterActDetour(OrigEnterAct orig, RunManager self, int currentActIndex, bool doTransition)
	{
		await orig(self, currentActIndex, doTransition);
		await RestorePlayersToFullHpIfEnteringAct4(self);
		RefreshForCurrentRun(self);
	}

	private static async Task<AbstractRoom> EnterRoomDebugDetour(OrigEnterRoomDebug orig, RunManager self, RoomType roomType, MegaCrit.Sts2.Core.Map.MapPointType pointType, AbstractModel? model, bool showTransition)
	{
		AbstractRoom room = await orig(self, roomType, pointType, model, showTransition);
		RefreshForCurrentRun(self);
		return room;
	}

	internal static void RefreshForCurrentRun(RunManager self)
	{
		if (TestMode.IsOn || self.DebugOnlyGetState() is not RunState state || !IsAct4Context(state))
		{
			return;
		}

		var me = LocalContext.GetMe(state);
		string characterName = me.Character.Title.GetFormattedText();
		string actName = new LocString("acts", ModelDb.GetId<Sts1Act4>().Entry + ".title").GetFormattedText();
		string statusText = $"{characterName} - {actName} 进阶{state.AscensionLevel}";
		PlatformUtil.SetRichPresence("IN_RUN", self.NetService.GetRawLobbyIdentifier(), state.Players.Count);
		PlatformUtil.SetRichPresenceValue("Character", characterName);
		PlatformUtil.SetRichPresenceValue("Act", actName);
		PlatformUtil.SetRichPresenceValue("Ascension", state.AscensionLevel.ToString());
		PlatformUtil.SetRichPresenceValue("CharacterDisplay", characterName);
		PlatformUtil.SetRichPresenceValue("ActDisplay", actName);
		PlatformUtil.SetRichPresenceValue("Status", statusText);
		PlatformUtil.SetRichPresenceValue("status", statusText);
		// Local writes succeed; if Steam still shows nothing, treat it as a client-side display issue.
		Logger.Info($"Updated Steam rich presence: Character='{characterName}', Act='{actName}', Ascension='{state.AscensionLevel}', Status='{statusText}'.");
	}

	private static async Task RestorePlayersToFullHpIfEnteringAct4(RunManager self)
	{
		if (TestMode.IsOn || self.DebugOnlyGetState() is not RunState state || state.Act.Id != ModelDb.GetId<Sts1Act4>())
		{
			return;
		}

		int restoredPlayers = 0;
		foreach (var player in state.Players)
		{
			if (player.Creature.CurrentHp >= player.Creature.MaxHp)
			{
				continue;
			}

			await CreatureCmd.SetCurrentHp(player.Creature, player.Creature.MaxHp);
			restoredPlayers++;
		}

		if (restoredPlayers > 0)
		{
			Logger.Info($"Restored {restoredPlayers} player(s) to full HP on entering {state.Act.Id.Entry}.");
		}
	}

	private static bool IsAct4Context(RunState state)
	{
		if (state.Act.Id == ModelDb.GetId<Sts1Act4>())
		{
			return true;
		}

		if (state.CurrentRoom is CombatRoom combatRoom)
		{
			ModelId encounterId = combatRoom.Encounter.Id;
			return encounterId == ModelDb.GetId<CorruptHeartEncounter>()
				|| encounterId == ModelDb.GetId<SpireShieldAndSpear>();
		}

		return false;
	}
}
