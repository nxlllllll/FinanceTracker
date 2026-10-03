using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Account;
using MediatR;

namespace FinanceTracker.Tests.Integration._Shared.Builders;

/// <summary>
/// Creates an account through MediatR, so that it is in the Event Store and AccountLoader can load it
/// during authorization, and fills its read model, which the projection worker would otherwise do later.
/// </summary>
public sealed class AccountFlowBuilder(IMediator mediator, FinanceTrackerContext context)
{
	public async Task<Guid> CreateAsync(Guid userId, decimal balance = 10_000m)
	{
		Result<Guid, AppException> result = await mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Основной счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			InitialBalance: balance
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Guid accountId = result.Value!;

		await context.Accounts.AddAsync(new AccountEntity
		{
			Id = accountId,
			UserId = userId,
			Name = Name.Create(value: "Основной счёт").Value,
			AccountType = AccountType.Checking,
			Currency = Currency.Create(value: "RUB").Value,
			IsArchived = false,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await context.AccountBalances.AddAsync(new AccountBalanceEntity
		{
			AccountId = accountId,
			Balance = balance,
			UpdatedAt = DateTimeOffset.UtcNow
		});
		await context.SaveChangesAsync();

		return accountId;
	}
}
