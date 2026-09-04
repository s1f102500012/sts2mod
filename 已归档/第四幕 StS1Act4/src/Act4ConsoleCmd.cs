using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace StS1Act4;

public sealed class Act4ConsoleCmd : AbstractConsoleCmd
{
	public override string CmdName => "act4";

	public override string Args => "[map|shield|heart]";

	public override string Description => "快速跳到第四层，或直接进入盾矛/心脏战。";

	public override bool IsNetworked => true;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		if (issuingPlayer?.RunState is not RunState runState || !RunManager.Instance.IsInProgress)
		{
			return new CmdResult(success: false, "该命令只能在跑局中使用。");
		}

		string mode = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "map";
		return mode switch
		{
			"map" => EnterAct4Map(runState),
			"shield" => EnterEncounter(ModelDb.Encounter<SpireShieldAndSpear>().ToMutable(), RoomType.Elite, "已跳转到第四层盾矛战。"),
			"heart" => EnterEncounter(ModelDb.Encounter<CorruptHeartEncounter>().ToMutable(), RoomType.Boss, "已跳转到第四层心脏战。"),
			_ => new CmdResult(success: false, "用法: act4 [map|shield|heart]")
		};
	}

	public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
	{
		if (args.Length <= 1)
		{
			return CompleteArgument(new[] { "map", "shield", "heart" }, Array.Empty<string>(), args.FirstOrDefault() ?? "");
		}

		return base.GetArgumentCompletions(player, args);
	}

	private static CmdResult EnterAct4Map(RunState runState)
	{
		ModelId act4Id = ModelDb.GetId<Sts1Act4>();
		int actIndex = runState.Acts.ToList().FindIndex(act => act.Id == act4Id);
		if (actIndex >= 0)
		{
			return new CmdResult(RefreshPresenceAfter(RunManager.Instance.EnterAct(actIndex)), success: true, "已跳转到第四层地图。");
		}

		Sts1Act4 actModel = (Sts1Act4)ModelDb.Act<Sts1Act4>().ToMutable();
		runState.SetActDebug(actModel);
		actModel.GenerateRooms(runState.Rng.UpFront, runState.UnlockState, runState.Players.Count > 1);
		return new CmdResult(RefreshPresenceAfter(RunManager.Instance.EnterAct(runState.CurrentActIndex)), success: true, "当前楼层已替换为第四层。");
	}

	private static CmdResult EnterEncounter(EncounterModel encounter, RoomType roomType, string message)
	{
		return new CmdResult(RefreshPresenceAfter(RunManager.Instance.EnterRoomDebug(roomType, roomType == RoomType.Boss ? MapPointType.Boss : MapPointType.Elite, encounter)), success: true, message);
	}

	private static async Task RefreshPresenceAfter(Task task)
	{
		await task;
		Act4SteamHooks.RefreshForCurrentRun(RunManager.Instance);
	}
}
