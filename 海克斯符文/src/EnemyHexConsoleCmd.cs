using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes;

public sealed class EnemyHexConsoleCmd : AbstractConsoleCmd
{
	public override string CmdName => "enemyhex";

	public override string Args => "<hex:string>";

	public override string Description => "将当前楼层的敌方海克斯替换为指定海克斯，并立即刷新显示与当前敌群效果。";

	public override bool IsNetworked => true;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		if (issuingPlayer?.RunState is not RunState runState || !RunManager.Instance.IsInProgress)
		{
			return new CmdResult(success: false, "该命令只能在跑局中使用。");
		}

		if (args.Length != 1)
		{
			return new CmdResult(success: false, "用法: enemyhex <hex>");
		}

		if (!TryParseMonsterHex(args[0], out MonsterHexKind hex))
		{
			return new CmdResult(success: false, $"未知海克斯: {args[0]}");
		}

		int actIndex = runState.CurrentActIndex;
		if (actIndex < 0 || actIndex > 2)
		{
			return new CmdResult(success: false, "该命令当前只支持第 1-3 层。");
		}

		HextechMayhemModifier modifier = ModEntry.EnsureMayhemModifier(runState);
		modifier.DebugSetOnlyMonsterHex(actIndex, hex, ModInfo.GetMonsterHexRarity(hex));
		HextechEnemyUi.Refresh(modifier);
		return new CmdResult(ApplyIfNeeded(modifier), success: true, $"当前楼层敌方海克斯已设置为 {hex}。");
	}

	public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
	{
		if (args.Length <= 1)
		{
			return CompleteArgument(
				GetAvailableMonsterHexes().Select(static hex => hex.ToString()).ToArray(),
				Array.Empty<string>(),
				args.FirstOrDefault() ?? "");
		}

		return base.GetArgumentCompletions(player, args);
	}

	private static async System.Threading.Tasks.Task ApplyIfNeeded(HextechMayhemModifier modifier)
	{
		await modifier.ApplyToCurrentEnemiesIfNeeded();
		HextechEnemyUi.Refresh(modifier);
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

			string relicId = ModInfo.GetIconRelicForMonsterHex(candidate).Id.Entry;
			if (Normalize(relicId) == normalized)
			{
				hex = candidate;
				return true;
			}
		}

		hex = default;
		return false;
	}

	private static IEnumerable<MonsterHexKind> GetAvailableMonsterHexes()
	{
		foreach (HextechRarityTier rarity in Enum.GetValues<HextechRarityTier>())
		{
			foreach (MonsterHexKind hex in ModInfo.GetMonsterHexesForRarity(rarity))
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
}
