using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Repositories.AccountType;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

namespace FinanceTracker.Tests.Integration.Infrastructure.AccountType;

public sealed class AccountTypeReadRepositoryTests : DatabaseFixture
{
	private AccountTypeReadRepository _readRepository = null!;
	private AccountTypeBuilder _accountTypeBuilder = null!;
	
	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_readRepository = new AccountTypeReadRepository(context: Context);
		_accountTypeBuilder = new AccountTypeBuilder(context: Context);
	}

	[Test]
	public async Task GetAllAsync_WithNoAccountTypes_ShouldReturnEmptyList()
	{
		IReadOnlyList<AccountTypeDto> result = await _readRepository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetAllAsync_WithAccountTypes_ShouldReturnAll()
	{
		await _accountTypeBuilder.CreateAsync(type: Core.Domains.Account.AccountType.Checking);
		await _accountTypeBuilder.CreateAsync(type: Core.Domains.Account.AccountType.Savings);

		IReadOnlyList<AccountTypeDto> result = await _readRepository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByTypeAsync_WithNonExistentType_ShouldReturnNull()
	{
		AccountTypeDto? result = await _readRepository.GetByTypeAsync(type: "checking");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByTypeAsync_WithExistingType_ShouldReturnCorrectDto()
	{
		await _accountTypeBuilder.CreateAsync(type: Core.Domains.Account.AccountType.Checking);

		AccountTypeDto? result = await _readRepository.GetByTypeAsync(type: "checking");

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Type).IsEqualTo(expected: "checking");
		await Assert.That(value: result.Name).IsEqualTo(expected: "Текущий счёт");
	}
}