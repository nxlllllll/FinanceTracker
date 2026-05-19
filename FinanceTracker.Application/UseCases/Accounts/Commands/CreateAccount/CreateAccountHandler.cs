using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.UseCases.Accounts.Commands.CreateAccount;

public sealed class CreateAccountHandler(
	IAccountRepository accountRepository,
	IDateProvider dateProvider
) : IRequestHandler<CreateAccountCommand, Result<Guid, DomainException>>
{
	public async Task<Result<Guid, DomainException>> Handle(
		CreateAccountCommand command,
		CancellationToken ct = default)
	{
		Result<Account, DomainException> accountResult = Account.Create(
			occurredAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: command.Name,
			type: command.Type,
			currency: command.Currency,
			balance: command.InitialBalance
		);
		if (accountResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: accountResult.Error!);
 
		Account account = accountResult.Value!;
		await accountRepository.SaveAsync(account: account, ct: ct);
		
		return Result<Guid, DomainException>.Success(value: account.Id);
	}
}