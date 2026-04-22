using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	ITransactionRepository transactionRepository
) : IRequestHandler<CreateTransactionCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateTransactionCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = Transaction.Create(
			accountId: command.AccountId,
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: command.Amount,
			direction: command.Direction,
			exchangeRate: command.ExchangeRate,
			description: command.Description,
			occurredAt: command.OccurredAt
		);

		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);
		
		return transaction.Id;
	}
}