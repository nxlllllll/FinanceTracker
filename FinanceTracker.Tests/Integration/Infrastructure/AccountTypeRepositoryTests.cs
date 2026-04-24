using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Repositories;

namespace FinanceTracker.Tests.Integration.Infrastructure;

public sealed class AccountTypeRepositoryTests : DatabaseFixture
{
	private AccountTypeRepository _repository = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
		=> _repository = new AccountTypeRepository(context: Context);

	[Test]
	public async Task GetAllAsync_WithNoAccountTypes_ShouldReturnEmptyList()
	{
		IReadOnlyList<AccountTypeDto> result = await _repository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetAllAsync_WithAccountTypes_ShouldReturnAll()
	{
		await CreateAccountTypeAsync(type: AccountType.Checking);
		await CreateAccountTypeAsync(type: AccountType.Savings);

		IReadOnlyList<AccountTypeDto> result = await _repository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByTypeAsync_WithNonExistentType_ShouldReturnNull()
	{
		AccountTypeDto? result = await _repository.GetByTypeAsync(type: "checking");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByTypeAsync_WithExistingType_ShouldReturnCorrectDto()
	{
		await CreateAccountTypeAsync(type: AccountType.Checking);

		AccountTypeDto? result = await _repository.GetByTypeAsync(type: "checking");

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Type).IsEqualTo(expected: "checking");
		await Assert.That(value: result.Name).IsEqualTo(expected: "Текущий счёт");
	}
}