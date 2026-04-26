using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace HextechRunes;

internal static class HextechGoldrendSync
{
    private const int GoldrendStealAmount = 10;

    private static readonly Dictionary<ulong, int> PendingLocalCombatGoldLosses = new();

    public static void ResetCombat()
    {
        PendingLocalCombatGoldLosses.Clear();
    }

    public static async Task HandleEnemyGoldrendHit(Player targetPlayer)
    {
        NetGameType gameType = RunManager.Instance.NetService.Type;
        if (gameType is NetGameType.Singleplayer or NetGameType.None)
        {
            int singlePlayerAmount = Math.Min(GoldrendStealAmount, Math.Max(0, targetPlayer.Gold));
            if (singlePlayerAmount > 0)
            {
                await PlayerCmd.LoseGold(singlePlayerAmount, targetPlayer, GoldLossType.Lost);
            }

            return;
        }

        if (gameType is not (NetGameType.Host or NetGameType.Client))
        {
            return;
        }

        if (targetPlayer.NetId == RunManager.Instance.NetService.NetId)
        {
            TrackPendingLocalGoldLoss(targetPlayer);
        }
    }

    public static async Task ApplyPendingCombatGoldLosses(RunState runState)
    {
        if (PendingLocalCombatGoldLosses.Count == 0)
        {
            return;
        }

        KeyValuePair<ulong, int>[] losses = PendingLocalCombatGoldLosses.ToArray();
        PendingLocalCombatGoldLosses.Clear();

        foreach ((ulong targetNetId, int pendingAmount) in losses)
        {
            Player? targetPlayer = runState.Players.FirstOrDefault(player => player.NetId == targetNetId);
            if (targetPlayer == null || targetPlayer.NetId != RunManager.Instance.NetService.NetId)
            {
                continue;
            }

            int amount = Math.Min(pendingAmount, Math.Max(0, targetPlayer.Gold));
            if (amount <= 0)
            {
                continue;
            }

            await PlayerCmd.LoseGold(amount, targetPlayer, GoldLossType.Lost);
            if (RunManager.Instance.NetService.Type is NetGameType.Host or NetGameType.Client)
            {
                RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(amount);
            }
        }
    }

    private static void TrackPendingLocalGoldLoss(Player targetPlayer)
    {
        int alreadyPending = PendingLocalCombatGoldLosses.GetValueOrDefault(targetPlayer.NetId, 0);
        int remainingGold = Math.Max(0, targetPlayer.Gold - alreadyPending);
        int amount = Math.Min(GoldrendStealAmount, remainingGold);
        if (amount <= 0)
        {
            return;
        }

        PendingLocalCombatGoldLosses[targetPlayer.NetId] = alreadyPending + amount;
    }
}
