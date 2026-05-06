using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.User;

public sealed class UserReadRepositoryTests : DatabaseFixture
{
    private UserReadRepository _readRepository = null!;
    private UserWriteRepository _writeRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
    {
        _readRepository = new UserReadRepository(context: Context);
        _writeRepository = new UserWriteRepository(context: Context);
        _currencyBuilder = new CurrencyBuilder(context: Context);
    }

    private async Task<Core.Domains.User.User> CreateAndSaveUserAsync(string currencyCode = "RUB")
    {
        await _currencyBuilder.CreateAsync(code: currencyCode);
        Result<Core.Domains.User.User, DomainException> result = Core.Domains.User.User.Register(
            createdAt: FakeDateProvider.Default.UtcNow,
            email: Email.Create(value: $"{Guid.NewGuid()}@test.com").Value,
            passwordHash: "hash",
            baseCurrency: Core.ValueObjects.Currency.Create(value: currencyCode).Value
        );
        Core.Domains.User.User user = result.Value!;
        
        await _writeRepository.CreateAsync(user: user);
        return user;
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentUser_ShouldReturnNull()
    {
        Core.Domains.User.User? result = await _readRepository.GetByIdAsync(userId: Guid.NewGuid());
        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WithExistingUser_ShouldReturnCorrectUser()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        Core.Domains.User.User? result = await _readRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Id).IsEqualTo(expected: user.Id);
        await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
        await Assert.That(value: result.BaseCurrency.Value).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task GetByEmailAsync_WithNonExistentEmail_ShouldReturnNull()
    {
        Core.Domains.User.User? result = await _readRepository.GetByEmailAsync(email: "notexist@test.com");
        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByEmailAsync_WithExistingEmail_ShouldReturnCorrectUser()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        Core.Domains.User.User? result = await _readRepository.GetByEmailAsync(email: user.Email);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Id).IsEqualTo(expected: user.Id);
        await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
    }
}