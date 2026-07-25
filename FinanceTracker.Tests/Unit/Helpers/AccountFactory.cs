using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
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
		Result<Name, DomainException> nameResult = Name.Create(value: name);
		if (nameResult.IsFailure)
			return Result<Account, DomainException>.Failure(error: nameResult.Error!);

		Result<Currency, DomainException> currencyResult = Currency.Create(value: currency);
		if (currencyResult.IsFailure)
			return Result<Account, DomainException>.Failure(error: currencyResult.Error!);

		return Account.Create(
			occurredAt: FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.CreateVersion7(),
			name: nameResult.Value,
			type: type,
			currency: currencyResult.Value,
			balance: balance
		);
	}

	public static Account CreateWithArchivation(
		Guid? userId = null,
		string currency = "RUB",
		decimal balance = 1000,
		bool archived = false)
	{
		Currency currencyValue = Currency.Create(value: currency).Value!;

		return Account.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: userId ?? Guid.CreateVersion7(),
			name: Name.Create(value: "Карта Сбер").Value!,
			type: AccountType.Checking,
			balance: Money.Reconstitute(amount: balance, currency: currencyValue),
			isArchived: archived,
			createdAt: FakeDateProvider.Default.UtcNow,
			version: 1
		);
	}

	public static AccountReadModel CreateReadModel(
		Guid? id = null,
		Guid? userId = null,
		string name = "Карта Сбер",
		AccountType type = AccountType.Checking,
		string currency = "RUB",
		decimal balance = 1000m,
		bool isArchived = false,
		int version = 1)
	{
		Currency curr = Currency.Reconstitute(value: currency);

		return new AccountReadModel(
			Id: id ?? Guid.CreateVersion7(),
			UserId: userId ?? Guid.CreateVersion7(),
			Name: Name.Create(value: name).Value!,
			Type: type,
			Balance: Money.Reconstitute(amount: balance, currency: curr),
			IsArchived: isArchived,
			Version: version,
			CreatedAt: FakeDateProvider.Default.UtcNow
		);
	}
}
