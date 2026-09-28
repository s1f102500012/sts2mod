namespace HextechRunes;

public abstract partial class HextechRelicBase
{
	public static bool IsNetworkMultiplayerRun()
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun();
	}

	protected static bool IsNetworkMultiplayer()
	{
		return IsNetworkMultiplayerRun();
	}

	protected int GetPlayerActNumberForScaling()
	{
		return HextechPlayerContextHelper.GetActNumberForScaling(Owner);
	}

	protected bool IsDefectPlayer(Player player)
	{
		return HextechPlayerContextHelper.IsDefectPlayer(player);
	}

	protected bool IsDefectOwner => Owner != null && IsDefectPlayer(Owner);

	protected bool IsIroncladPlayer(Player player)
	{
		return HextechPlayerContextHelper.IsIroncladPlayer(player);
	}

	protected bool IsSilentPlayer(Player player)
	{
		return HextechPlayerContextHelper.IsSilentPlayer(player);
	}

	protected bool IsRegentPlayer(Player player)
	{
		return HextechPlayerContextHelper.IsRegentPlayer(player);
	}

	protected bool IsRegentOwner => Owner != null && IsRegentPlayer(Owner);

	protected bool IsNecrobinderPlayer(Player player)
	{
		return HextechPlayerContextHelper.IsNecrobinderPlayer(player);
	}

	// "战斗第一回合"类效果按持有者自己的回合数判定，不看 RoundNumber：额外回合不推进回合号，
	// 持有者在第 1 回合拿到额外回合时（佩尔之眼等）RoundNumber 仍为 1，会让开局效果再结算一次。
	// 原版 TurnNumber 从 1 开始，只在该玩家开始新回合（含额外回合）时递增。
	protected bool IsOwnersFirstTurn => Owner?.PlayerCombatState?.TurnNumber == 1;
}
