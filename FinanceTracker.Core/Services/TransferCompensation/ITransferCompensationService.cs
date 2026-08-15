using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Pending;

namespace FinanceTracker.Core.Services.TransferCompensation;

/// <summary>
/// Unwinds a transfer whose credit side never landed: the debit is refunded to the source account
/// and the transfer is settled as failed, or escalated to <c>unresolvable_events</c> when the refund
/// itself cannot be applied.
/// </summary>
public interface ITransferCompensationService
{
	/// <remarks>Caller must wrap this call in IUnitOfWork.ExecuteInTransactionAsync.</remarks>
	Task CompensateAsync(PendingCreditTransfer transfer, CancellationToken ct = default);
}
