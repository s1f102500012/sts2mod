using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;

namespace HextechRunes;

public sealed class EnemyHexConsoleCmd : AbstractConsoleCmd
{
	private const string AddAction = "add";
	private const string RemoveAction = "remove";
	private const string SetAction = "set";
	private const string ReplaceAction = "replace";

	public override string CmdName => "enemyhex";

	public override string Args => "<add|remove|set> <hex:string>";

	public override string Description => "添加、移除或替换敌方海克斯，并立即刷新显示与当前敌群效果。";

	public override bool IsNetworked => true;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		if (issuingPlayer?.RunState is not RunState runState || !RunManager.Instance.IsInProgress)
		{
			return new CmdResult(success: false, "该命令只能在跑局中使用。");
		}

		if (!TryParseArguments(args, out EnemyHexConsoleAction action, out string? hexInput, out string? usageError))
		{
			return new CmdResult(success: false, usageError ?? GetUsage());
		}

		if (!TryParseMonsterHex(hexInput!, out MonsterHexKind hex))
		{
			return new CmdResult(success: false, $"未知海克斯: {hexInput}");
		}

		HextechMayhemModifier modifier = ModEntry.EnsureMayhemModifier(runState);
		switch (action)
		{
			case EnemyHexConsoleAction.Add:
				return AddEnemyHex(modifier, hex);
			case EnemyHexConsoleAction.Remove:
				return RemoveEnemyHex(modifier, hex);
			case EnemyHexConsoleAction.Set:
				return SetEnemyHex(runState, modifier, hex);
			default:
				return new CmdResult(success: false, GetUsage());
		}
	}

	public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
	{
		if (args.Length <= 1)
		{
			string[] actionCompletions = [ AddAction, RemoveAction, SetAction, ReplaceAction ];
			string[] legacyHexCompletions = GetAvailableMonsterHexes().Select(static hex => hex.ToString()).ToArray();
			return CompleteArgument(
				actionCompletions.Concat(legacyHexCompletions).ToArray(),
				Array.Empty<string>(),
				args.FirstOrDefault() ?? "");
		}

		if (args.Length == 2 && IsAction(args[0]))
		{
			return CompleteArgument(GetCompletionHexes(player, args[0]).ToArray(), Array.Empty<string>(), args[1]);
		}

		return base.GetArgumentCompletions(player, args);
	}

	private static CmdResult AddEnemyHex(HextechMayhemModifier modifier, MonsterHexKind hex)
	{
		if (modifier.HasActiveMonsterHex(hex))
		{
			return new CmdResult(success: true, $"敌方已拥有海克斯 {hex}。");
		}

		modifier.DebugAddMonsterHex(hex);
		HextechEnemyUi.Refresh(modifier);
		return new CmdResult(ApplyIfNeeded(modifier), success: true, $"已添加敌方海克斯 {hex}。");
	}

	private static CmdResult RemoveEnemyHex(HextechMayhemModifier modifier, MonsterHexKind hex)
	{
		if (!modifier.DebugRemoveMonsterHex(hex))
		{
			return new CmdResult(success: false, $"敌方未拥有海克斯 {hex}。");
		}

		HextechEnemyUi.Refresh(modifier);
		return new CmdResult(success: true, $"已移除敌方海克斯 {hex}。");
	}

	private static CmdResult SetEnemyHex(RunState runState, HextechMayhemModifier modifier, MonsterHexKind hex)
	{
		int actIndex = runState.CurrentActIndex;
		if (actIndex < 0)
		{
			return new CmdResult(success: false, "当前不在有效幕内，无法替换本阶段敌方海克斯。");
		}

		modifier.DebugSetOnlyMonsterHex(actIndex, hex, MonsterHexCatalog.GetMonsterHexRarity(hex));
		HextechEnemyUi.Refresh(modifier);
		return new CmdResult(ApplyIfNeeded(modifier), success: true, $"当前楼层敌方海克斯已替换为 {hex}。");
	}

	private static async System.Threading.Tasks.Task ApplyIfNeeded(HextechMayhemModifier modifier)
	{
		await modifier.ApplyToCurrentEnemiesIfNeeded();
		HextechEnemyUi.Refresh(modifier);
	}

	private static bool TryParseArguments(string[] args, out EnemyHexConsoleAction action, out string? hexInput, out string? error)
	{
		action = EnemyHexConsoleAction.Set;
		hexInput = null;
		error = null;

		if (args.Length == 1)
		{
			hexInput = args[0];
			return true;
		}

		if (args.Length != 2 || !TryParseAction(args[0], out action))
		{
			error = GetUsage();
			return false;
		}

		hexInput = args[1];
		return true;
	}

	private static bool TryParseAction(string input, out EnemyHexConsoleAction action)
	{
		switch (Normalize(input))
		{
			case "ADD":
			case "A":
				action = EnemyHexConsoleAction.Add;
				return true;
			case "REMOVE":
			case "RM":
			case "DEL":
			case "DELETE":
				action = EnemyHexConsoleAction.Remove;
				return true;
			case "SET":
			case "REPLACE":
				action = EnemyHexConsoleAction.Set;
				return true;
			default:
				action = default;
				return false;
		}
	}

	private static bool IsAction(string input)
	{
		return TryParseAction(input, out _);
	}

	private static IEnumerable<string> GetCompletionHexes(Player? player, string actionInput)
	{
		if (TryParseAction(actionInput, out EnemyHexConsoleAction action)
			&& action == EnemyHexConsoleAction.Remove
			&& player?.RunState is RunState runState
			&& RunManager.Instance.IsInProgress)
		{
			return ModEntry.EnsureMayhemModifier(runState)
				.GetActiveMonsterHexes()
				.Select(static hex => hex.ToString());
		}

		return GetAvailableMonsterHexes().Select(static hex => hex.ToString());
	}

	private static bool TryParseMonsterHex(string input, out MonsterHexKind hex)
	{
		string normalized = Normalize(input);
		foreach (MonsterHexKind candidate in GetAvailableMonsterHexes())
		{
			if (Normalize(candidate.ToString()) == normalized)
			{
				hex = candidate;
				return true;
			}

			string relicId = MonsterHexCatalog.GetIconRelicForMonsterHex(candidate).Id.Entry;
			if (Normalize(relicId) == normalized)
			{
				hex = candidate;
				return true;
			}
		}

		hex = default;
		return false;
	}

	private static string GetUsage()
	{
		return "用法: enemyhex add <hex> | enemyhex remove <hex> | enemyhex set <hex>。旧用法 enemyhex <hex> 仍会替换当前楼层敌方海克斯。";
	}

	private static IEnumerable<MonsterHexKind> GetAvailableMonsterHexes()
	{
		foreach (HextechRarityTier rarity in Enum.GetValues<HextechRarityTier>())
		{
			foreach (MonsterHexKind hex in MonsterHexCatalog.GetMonsterHexesForRarity(rarity))
			{
				yield return hex;
			}
		}
	}

	private static string Normalize(string value)
	{
		return new string(value
			.Where(static ch => ch != '_' && ch != '-' && !char.IsWhiteSpace(ch))
			.Select(char.ToUpperInvariant)
			.ToArray());
	}

	private enum EnemyHexConsoleAction
	{
		Set,
		Add,
		Remove
	}
}
