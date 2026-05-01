using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountHandler(
	IAccountRepository accountRepository,
	IDateProvider dateProvider
) : IRequestHandler<CreateAccountCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = Account.Create(
			occurredAt: dateProvider.UtcNow,
			userId: command.UserId,
			name: command.Name,
			type: command.Type,
			currency: command.Currency,
			balance: command.InitialBalance
		);
		
		await accountRepository.SaveAsync(account: account, ct: ct);
		
		return account.Id;
	}
}