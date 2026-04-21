using FinanceTracker.Core.Domains.User;
using FinanceTracker.Infrastructure.Database.Repositories;
using FinanceTracker.Tests.Integration.Infrastructure;

namespace FinanceTracker.Tests.Integration;

public sealed class UserRepositoryTests : DatabaseFixture
{
	private UserRepository _userRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
        => _userRepository = new UserRepository(context: Context);

    private async Task<User> CreateAndSaveUserAsync(string currencyCode = "RUB")
    {
        await CreateCurrencyAsync(currencyCode);

        User user = User.Register(email: $"{Guid.NewGuid()}@test.com", passwordHash: "hash", baseCurrencyCode: currencyCode);

        await _userRepository.CreateAsync(user: user);
        return user;
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentUser_ShouldReturnNull()
    {
        User? result = await _userRepository.GetByIdAsync(userId: Guid.NewGuid());

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WithExistingUser_ShouldReturnCorrectUser()
    {
        User user = await CreateAndSaveUserAsync();

        User? result = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result.Id).IsEqualTo(expected: user.Id);
        await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
        await Assert.That(value: result.BaseCurrencyCode).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task GetByEmailAsync_WithNonExistentEmail_ShouldReturnNull()
    {
        User? result = await _userRepository.GetByEmailAsync(email: "notexist@test.com");

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByEmailAsync_WithExistingEmail_ShouldReturnCorrectUser()
    {
        User user = await CreateAndSaveUserAsync();

        User? result = await _userRepository.GetByEmailAsync(email: user.Email);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result.Id).IsEqualTo(expected: user.Id);
        await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
    }

    [Test]
    public async Task ChangeEmailAsync_ShouldUpdateEmail()
    {
        User user = await CreateAndSaveUserAsync();

        await _userRepository.ChangeEmailAsync(userId: user.Id, newEmail: "new@test.com");

        User? loaded = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.Email).IsEqualTo(expected: "new@test.com");
    }

    [Test]
    public async Task ChangePasswordAsync_ShouldUpdatePasswordHash()
    {
        User user = await CreateAndSaveUserAsync();

        await _userRepository.ChangePasswordAsync(userId: user.Id, newPasswordHash: "newHash");

        User? loaded = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.PasswordHash).IsEqualTo(expected: "newHash");
    }

    [Test]
    public async Task ChangeBaseCurrencyAsync_ShouldUpdateBaseCurrencyCode()
    {
        User user = await CreateAndSaveUserAsync();
        await CreateCurrencyAsync(code: "USD");

        await _userRepository.ChangeBaseCurrencyAsync(userId: user.Id, newBaseCurrencyCode: "USD");

        User? loaded = await _userRepository.GetByIdAsync(userId: user.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.BaseCurrencyCode).IsEqualTo(expected: "USD");
    }
}