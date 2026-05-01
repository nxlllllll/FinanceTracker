using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

namespace FinanceTracker.Tests.Integration.Infrastructure.User;

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
        Core.Domains.User.User user = Core.Domains.User.User.Register(
            createdAt: FakeDateProvider.Default.UtcNow,
            email: $"{Guid.NewGuid()}@test.com",
            passwordHash: "hash",
            baseCurrency: currencyCode
        );
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
        await Assert.That(value: loaded.BaseCurrency).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task ChangeEmailAsync_ShouldUpdateEmail()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        await _writeRepository.ChangeEmailAsync(userId: user.Id, newEmail: "new@test.com");

        Core.Domains.User.User? loaded = await _readRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.Email).IsEqualTo(expected: "new@test.com");
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

        await _writeRepository.ChangeBaseCurrencyAsync(userId: user.Id, newBaseCurrencyCode: "USD");

        Core.Domains.User.User? loaded = await _readRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.BaseCurrency).IsEqualTo(expected: "USD");
    }
}