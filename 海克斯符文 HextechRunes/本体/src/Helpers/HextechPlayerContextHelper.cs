using MegaCrit.Sts2.Core.Models.Characters;

namespace HextechRunes;

internal static class HextechPlayerContextHelper
{
	public static bool IsNetworkMultiplayerRun()
	{
		try
		{
			return RunManager.Instance?.NetService?.Type is NetGameType.Host or NetGameType.Client;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsClientRun(bool fallbackWhenUnavailable = false)
	{
		try
		{
			var netService = RunManager.Instance?.NetService;
			return netService == null
				? fallbackWhenUnavailable
				: netService.Type == NetGameType.Client;
		}
		catch (NullReferenceException)
		{
			return fallbackWhenUnavailable;
		}
	}

	public static int GetActNumberForScaling(Player? owner)
	{
		if (owner?.RunState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault()?.IsEndlessLoopActive == true)
		{
			return 3;
		}

		return Math.Clamp((owner?.RunState.CurrentActIndex ?? 0) + 1, 1, 3);
	}

	public static bool IsDefectPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Defect>();
	}

	public static bool IsIroncladPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Ironclad>();
	}

	public static bool IsSilentPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Silent>();
	}

	public static bool IsRegentPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Regent>();
	}

	public static bool IsNecrobinderPlayer(Player player)
	{
		return player.Character.Id == ModelDb.GetId<Necrobinder>();
	}

	public static bool TryGetRuneCharacterPool(Player player, out PlayerRuneCharacterPool characterPool)
	{
		if (IsIroncladPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Ironclad;
			return true;
		}

		if (IsSilentPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Silent;
			return true;
		}

		if (IsRegentPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Regent;
			return true;
		}

		if (IsDefectPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Defect;
			return true;
		}

		if (IsNecrobinderPlayer(player))
		{
			characterPool = PlayerRuneCharacterPool.Necrobinder;
			return true;
		}

		characterPool = default;
		return false;
	}
}
