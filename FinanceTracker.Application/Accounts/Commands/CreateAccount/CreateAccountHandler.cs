using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountHandler(
	IAccountRepository accountRepository,
	IPublisher publisher
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
		
		List<IEvent> events = account.Events.ToList();
		
		await accountRepository.SaveAsync(account: account, ct: ct);

		await publisher.Publish(
			notification: new AccountEventsNotification(AccountId: account.Id, Events: events),
			cancellationToken: ct
		);
		
		return account.Id;
	}
}