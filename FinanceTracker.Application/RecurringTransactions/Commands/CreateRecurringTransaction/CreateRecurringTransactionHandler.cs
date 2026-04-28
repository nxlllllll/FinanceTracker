using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IAccountReadRepository accountReadRepository
) : IRequestHandler<CreateRecurringTransactionCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateRecurringTransactionCommand command,
		CancellationToken ct = default)
	{
		AccountDto account = await accountReadRepository.GetByIdAsync(accountId: command.AccountId, ct: ct)
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		if (account.UserId != command.UserId)
			throw new NotFoundException(message: "Account not found.", id: command.AccountId);
		
		Guid recurringTransactionId = Guid.NewGuid();

		await recurringTransactionWriteRepository.CreateAsync(
			recurringTransactionId: recurringTransactionId,
			userId: command.UserId,
			accountId: command.AccountId,
			categoryId: command.CategoryId,
			amount: command.Amount,
			currency: command.Currency,
			direction: command.Direction,
			dayOfMonth: command.DayOfMonth,
			description: command.Description,
			ct: ct
		);

		return recurringTransactionId;
	}
}