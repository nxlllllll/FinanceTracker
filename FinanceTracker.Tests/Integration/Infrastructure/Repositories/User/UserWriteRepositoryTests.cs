using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.User;

public sealed class UserWriteRepositoryTests : DatabaseFixture
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
            email: Email.Create(value: $"{Guid.CreateVersion7()}@test.com").Value,
            passwordHash: "hash",
            baseCurrency: Core.ValueObjects.Currency.Create(value: currencyCode).Value
        );
        Core.Domains.User.User user = result.Value!;
        
        await _writeRepository.CreateAsync(user: user);
        return user;
    }

    [Test]
    public async Task CreateAsync_ShouldPersistUser()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        Core.Domains.User.User? loaded = await _readRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.Id).IsEqualTo(expected: user.Id);
        await Assert.That(value: loaded.Email).IsEqualTo(expected: user.Email);
        await Assert.That(value: loaded.BaseCurrency.Value).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task ChangeEmailAsync_ShouldUpdateEmail()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        await _writeRepository.ChangeEmailAsync(
            userId: user.Id,
            newEmail: Email.Create(value: "new@test.com").Value
        );

        Core.Domains.User.User? loaded = await _readRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.Email.Value).IsEqualTo(expected: "new@test.com");
    }

    [Test]
    public async Task ChangePasswordAsync_ShouldUpdatePasswordHash()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        await _writeRepository.ChangePasswordAsync(userId: user.Id, newPasswordHash: "newHash");

        Core.Domains.User.User? loaded = await _readRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.PasswordHash).IsEqualTo(expected: "newHash");
    }

    [Test]
    public async Task ChangeBaseCurrencyAsync_ShouldUpdateBaseCurrencyCode()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();
        await _currencyBuilder.CreateAsync(code: "USD");

        await _writeRepository.ChangeBaseCurrencyAsync(
            userId: user.Id,
            newBaseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value
        );

        Core.Domains.User.User? loaded = await _readRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.BaseCurrency.Value).IsEqualTo(expected: "USD");
    }
}
