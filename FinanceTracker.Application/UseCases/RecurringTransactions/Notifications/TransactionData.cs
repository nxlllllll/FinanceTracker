using FinanceTracker.Application.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;
using INotification = MediatR.INotification;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Notifications;

public sealed record TransactionData(
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	Currency Currency,
	DirectionType Direction,
	string? Description,
	DateTime OccurredAt
) : IMediatRConvertible
{
	public INotification ToMediatRNotification()
	{
		return new TransactionDataNotification(
			AccountId: AccountId,
			UserId: UserId,
			CategoryId: CategoryId,
			Amount: Amount,
			Currency: Currency,
			Direction: Direction,
			Description: Description,
			OccurredAt: OccurredAt
		);
	}
}
