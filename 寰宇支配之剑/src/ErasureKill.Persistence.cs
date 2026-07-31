using MegaCrit.Sts2.Core.Combat;

namespace UniversalDominionSword;

internal static partial class ErasureKill
{
	public static PersistenceLease BeginPersistenceLease(
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
		return new PersistenceLease(
			onCommit: () =>
			{
				lock (ledger.Gate)
				{
					ledger.CompletionArmed = true;
				}
			},
			onDispose: () =>
			{
				lock (ledger.Gate)
				{
					ledger.PersistenceLeaseCount = Math.Max(
						0,
						ledger.PersistenceLeaseCount - 1);
				}
			});
	}

	internal sealed class PersistenceLease : IDisposable
	{
		private readonly Action _onCommit;
		private readonly Action _onDispose;
		private bool _committed;
		private bool _disposed;

		internal PersistenceLease(
			Action onCommit,
			Action onDispose)
		{
			_onCommit = onCommit;
			_onDispose = onDispose;
		}

		public void Commit()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(PersistenceLease));
			}
			if (_committed)
			{
				return;
			}

			_onCommit();
			_committed = true;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_onDispose();
			_disposed = true;
		}
	}
}
