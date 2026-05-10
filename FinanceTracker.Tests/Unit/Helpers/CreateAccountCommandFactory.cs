using FinanceTracker.Application.UseCases.Accounts.Commands.CreateAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateAccountCommandFactory
{
	public static CreateAccountCommand Create(
		Guid? userId = null,
		string name = "Карта Сбер",	
		AccountType type = AccountType.Checking,
		string currency = "RUB",
		decimal initialBalance = 10000)
	{
		return new CreateAccountCommand(
			UserId: userId ?? Guid.CreateVersion7(),
			Name: Name.Create(value: name).Value,
			Type: type,
			Currency: currency,
			InitialBalance: initialBalance
		);
	}
}