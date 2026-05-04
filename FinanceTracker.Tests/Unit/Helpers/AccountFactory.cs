using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class AccountFactory
{
	public static Result<Account, DomainException> Create(
		Guid? userId = null,
		string name = "Карта Сбер",
		AccountType type = AccountType.Checking,
		string currency = "RUB",
		decimal balance = 1000m)
	{
		Result<Account, DomainException> result = Account.Create(
			occurredAt: FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.NewGuid(),
			name: name,
			type: type,
			currency: currency,
			balance: balance
		);
		
		return result;
	}
	
	public static Account CreateAccountWithArchivation(
		Guid? userId = null,
		decimal balance = 1000,
		bool archived = false)
	{
		Account account = Create(userId: userId, balance: balance).Value!;
		account.ClearEvents();

		if (archived)
		{
			account.Archive(occurredAt: FakeDateProvider.Default.UtcNow);
			account.ClearEvents();
		}

		return account;
	}
	
	public static AccountDto CreateAccountDto(
		Guid? id = null,
		Guid? userId = null,
		string name = "Карта Сбер",
		AccountType type = AccountType.Checking,
		string currency = "RUB",
		decimal balance = 1000m,
		bool isArchived = false)
	{
		return new AccountDto(
			Id: id ?? Guid.NewGuid(),
			UserId: userId ?? Guid.NewGuid(),
			Name: name,
			Type: type,
			Currency: currency,
			Balance: balance,
			IsArchived: isArchived,
			CreatedAt: FakeDateProvider.Default.UtcNow
		);
	}
}