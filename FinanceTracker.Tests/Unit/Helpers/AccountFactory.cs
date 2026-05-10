using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

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
			userId: userId ?? Guid.CreateVersion7(),
			name: Name.Create(value: name).Value,
			type: type,
			currency: Currency.Create(value: currency).Value,
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
			Id: id ?? Guid.CreateVersion7(),
			UserId: userId ?? Guid.CreateVersion7(),
			Name: name,
			Type: type,
			Currency: Currency.Create(value: currency).Value,
			Balance: balance,
			IsArchived: isArchived,
			CreatedAt: FakeDateProvider.Default.UtcNow
		);
	}
}