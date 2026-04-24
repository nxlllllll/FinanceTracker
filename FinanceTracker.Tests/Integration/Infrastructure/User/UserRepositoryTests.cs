using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

namespace FinanceTracker.Tests.Integration.Infrastructure.User;

public sealed class UserRepositoryTests : DatabaseFixture
{
	private UserRepository _userRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
    {
        _userRepository = new UserRepository(context: Context);
        _currencyBuilder = new CurrencyBuilder(context: Context);
    }

    private async Task<Core.Domains.User.User> CreateAndSaveUserAsync(string currencyCode = "RUB")
    {
        await _currencyBuilder.CreateAsync(code: currencyCode);

        Core.Domains.User.User user = Core.Domains.User.User.Register(email: $"{Guid.NewGuid()}@test.com", passwordHash: "hash", baseCurrencyCode: currencyCode);

        await _userRepository.CreateAsync(user: user);
        return user;
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentUser_ShouldReturnNull()
    {
        Core.Domains.User.User? result = await _userRepository.GetByIdAsync(userId: Guid.NewGuid());

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WithExistingUser_ShouldReturnCorrectUser()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        Core.Domains.User.User? result = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result.Id).IsEqualTo(expected: user.Id);
        await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
        await Assert.That(value: result.BaseCurrencyCode).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task GetByEmailAsync_WithNonExistentEmail_ShouldReturnNull()
    {
        Core.Domains.User.User? result = await _userRepository.GetByEmailAsync(email: "notexist@test.com");

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByEmailAsync_WithExistingEmail_ShouldReturnCorrectUser()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        Core.Domains.User.User? result = await _userRepository.GetByEmailAsync(email: user.Email);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result.Id).IsEqualTo(expected: user.Id);
        await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
    }

    [Test]
    public async Task ChangeEmailAsync_ShouldUpdateEmail()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        await _userRepository.ChangeEmailAsync(userId: user.Id, newEmail: "new@test.com");

        Core.Domains.User.User? loaded = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.Email).IsEqualTo(expected: "new@test.com");
    }

    [Test]
    public async Task ChangePasswordAsync_ShouldUpdatePasswordHash()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();

        await _userRepository.ChangePasswordAsync(userId: user.Id, newPasswordHash: "newHash");

        Core.Domains.User.User? loaded = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.PasswordHash).IsEqualTo(expected: "newHash");
    }

    [Test]
    public async Task ChangeBaseCurrencyAsync_ShouldUpdateBaseCurrencyCode()
    {
        Core.Domains.User.User user = await CreateAndSaveUserAsync();
        await _currencyBuilder.CreateAsync(code: "USD");

        await _userRepository.ChangeBaseCurrencyAsync(userId: user.Id, newBaseCurrencyCode: "USD");

        Core.Domains.User.User? loaded = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.BaseCurrencyCode).IsEqualTo(expected: "USD");
    }
}