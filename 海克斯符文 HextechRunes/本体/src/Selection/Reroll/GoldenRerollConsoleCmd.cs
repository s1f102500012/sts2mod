using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;

namespace HextechRunes;

public sealed class GoldenRerollConsoleCmd : AbstractConsoleCmd
{
	public override string CmdName => "goldenreroll";

	public override string Args => "[force|clear|status]";

	public override string Description => "强制当前或下一次符合条件的海克斯选择触发金色刷新。";

	// 金色刷新按本机玩家独立判定；测试命令也只改变执行命令的客户端。
	public override bool IsNetworked => false;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		string action = args.Length == 0 ? "force" : args[0].Trim().ToLowerInvariant();
		if (args.Length > 1)
		{
			return new CmdResult(success: false, GetUsage());
		}

		switch (action)
		{
			case "force":
			case "on":
				HextechGoldenRerollDebug.ForceCurrentOrNext(out bool activatedCurrent);
				return new CmdResult(
					success: true,
					activatedCurrent
						? "当前海克斯选择已强制启用金色刷新。"
						: "下一次白银或黄金海克斯选择将强制启用金色刷新。");
			case "clear":
			case "off":
				HextechGoldenRerollDebug.Clear();
				return new CmdResult(success: true, "已取消下一次金色刷新强制触发。");
			case "status":
				return new CmdResult(
					success: true,
					HextechGoldenRerollDebug.IsNextEligibleForced
						? "下一次符合条件的海克斯选择已设为强制金色刷新。"
						: "当前没有待触发的金色刷新测试覆盖。");
			default:
				return new CmdResult(success: false, GetUsage());
		}
	}

	public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
	{
		if (args.Length <= 1)
		{
			return CompleteArgument(
				[ "force", "clear", "status" ],
				Array.Empty<string>(),
				args.FirstOrDefault() ?? "");
		}

		return base.GetArgumentCompletions(player, args);
	}

	private static string GetUsage()
	{
		return "用法: goldenreroll [force|clear|status]";
	}
}
