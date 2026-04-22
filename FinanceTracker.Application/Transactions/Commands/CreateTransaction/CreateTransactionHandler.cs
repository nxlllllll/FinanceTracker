using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	IAccountRepository accountRepository,
	ITransactionWriteRepository transactionWriteRepository
) : IRequestHandler<CreateTransactionCommand, Guid>
{
	private void ChangeAccountBalance(Account account, CreateTransactionCommand command, Guid transactionId)
	{
		Action<Guid, Guid, decimal, decimal, string?> func = command.Direction switch
		{
			DirectionType.Debit => account.Debit,
			DirectionType.Credit => account.Credit,
			_ => throw new ArgumentOutOfRangeException(message: $"Direction is unknown.", paramName: nameof(command.Direction))
		};

		func(transactionId, command.CategoryId, command.Amount, command.ExchangeRate, command.Description);
	}
	
	public async Task<Guid> Handle(
		CreateTransactionCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: command.AccountId, ct: ct) 
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);
		
		Guid transactionId = Guid.NewGuid();
		ChangeAccountBalance(account: account, command: command, transactionId: transactionId);
		
		await transactionWriteRepository.CreateAsync(
			transactionId: transactionId,
			accountId: command.AccountId,
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: command.Amount,
			direction: command.Direction,
			exchangeRate: command.ExchangeRate,
			description: command.Description,
			occurredAt: command.OccurredAt,
			ct: ct
		);

		await accountRepository.SaveAsync(account: account, ct: ct);

		return transactionId;
	}
}