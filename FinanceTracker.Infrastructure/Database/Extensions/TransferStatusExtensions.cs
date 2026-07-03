using FinanceTracker.Core.Domains.Transfer;

namespace FinanceTracker.Infrastructure.Database.Extensions;

public static class TransferStatusExtensions
{
	public static string ToCode(this TransferStatus status) => status switch
	{
		TransferStatus.PendingCredit => "pending_credit",
		TransferStatus.Completed => "completed",
		TransferStatus.Compensated => "compensated",
		TransferStatus.Failed => "failed",
		_ => throw new ArgumentOutOfRangeException(nameof(status), status, message: null)
	};

	public static TransferStatus FromCode(this string code) => code switch
	{
		"pending_credit" => TransferStatus.PendingCredit,
		"completed" => TransferStatus.Completed,
		"compensated" => TransferStatus.Compensated,
		"failed" => TransferStatus.Failed,
		_ => throw new ArgumentOutOfRangeException(nameof(code), code, message: null)
	};
}
