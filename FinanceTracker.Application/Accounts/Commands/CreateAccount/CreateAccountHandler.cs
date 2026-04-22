using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountHandler(
	IAccountRepository accountRepository
) : IRequestHandler<CreateAccountCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = Account.Create(
			userId: command.UserId,
			name: command.Name,
			accountType: command.AccountType,
			currency: command.Currency,
			balance: command.InitialBalance
		);
		
		await accountRepository.SaveAsync(account: account, ct: ct);
		
		return account.Id;
	}
}