namespace FinanceTracker.Core.Domains.Transfer;

/// <summary>
/// Lifecycle status of a two-phase transfer.
/// </summary>
public enum TransferStatus
{
	/// <summary>Debit has been applied to the source account; credit to the destination is pending.</summary>
	PendingCredit,

	/// <summary>Both debit and credit have been applied successfully.</summary>
	Completed,

	/// <summary>Credit failed; the debit was refunded to the source account.</summary>
	Compensated,

	/// <summary>Compensation itself failed; manual intervention is required.</summary>
	Failed
}
