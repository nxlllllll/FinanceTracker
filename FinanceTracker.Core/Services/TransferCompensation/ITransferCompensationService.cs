using FinanceTracker.Core.ReadModels;

namespace FinanceTracker.Core.Services.TransferCompensation;

/// <summary>
/// Compensates a stuck transfer by refunding the debit side of the source account.
/// Called by <c>TransferCreditLagJob</c> for transfers exceeding the compensation threshold.
/// </summary>
public interface ITransferCompensationService
{
	/// <remarks>Caller must wrap this call in IUnitOfWork.ExecuteInTransactionAsync.</remarks>
	Task CompensateAsync(PendingCreditTransfer transfer, CancellationToken ct = default);
}
