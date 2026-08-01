using MegaCrit.Sts2.Core.Combat;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	public static ErasurePersistenceLease BeginPersistenceLease(
		ICombatState combatState)
	{
		CombatLedger ledger = Ledgers.GetValue(
			combatState,
			state => new CombatLedger(state));
		lock (ledger.Gate)
		{
			ledger.CompletionArmed = false;
			ledger.PersistenceLeaseCount++;
		}
		return new ErasurePersistenceLease(
			onCommit: () =>
			{
				lock (ledger.Gate)
				{
					ledger.CompletionArmed = true;
					ledger.PersistenceLeaseCount = Math.Max(
						0,
						ledger.PersistenceLeaseCount - 1);
				}
			},
			onAbandon: () =>
			{
				lock (ledger.Gate)
				{
					ledger.PersistenceLeaseCount = Math.Max(
						0,
						ledger.PersistenceLeaseCount - 1);
				}
			});
	}
}
