using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class UserTests
{
	private static User CreateUser(
        string email = "test@test.com",
        string passwordHash = "hash",
        string baseCurrencyCode = "RUB")
    {
        return User.Register(
            email: email,
            passwordHash: passwordHash,
            baseCurrencyCode: baseCurrencyCode
        );
    }

    [Test]
    public async Task Register_WithValidData_ShouldSetCorrectState()
    {
        User user = CreateUser();

        await Assert.That(value: user.Id).IsNotDefault();
        await Assert.That(value: user.Email).IsEqualTo(expected: "test@test.com");
        await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "hash");
        await Assert.That(value: user.BaseCurrencyCode).IsEqualTo(expected: "RUB");
        await Assert.That(value: user.CreatedAt).IsNotDefault();
    }

    [Test]
    public async Task Register_WithEmptyEmail_ShouldThrowEmptyEmailException()
        => await Assert.That(action: () => CreateUser(email: String.Empty)).Throws<EmailException>();

    [Test]
    public async Task Register_WithEmptyPasswordHash_ShouldThrowArgumentException()
        => await Assert.That(action: () => CreateUser(passwordHash: String.Empty)).Throws<PasswordException>();

    [Test]
    public async Task Register_WithEmptyBaseCurrencyCode_ShouldThrowArgumentException()
        => await Assert.That(action: () => CreateUser(baseCurrencyCode: String.Empty)).Throws<CurrencyException>();

    [Test]
    public async Task ChangeEmail_WithValidEmail_ShouldChangeEmail()
    {
        User user = CreateUser();

        user.ChangeEmail(newEmail: "new@test.com");

        await Assert.That(value: user.Email).IsEqualTo(expected: "new@test.com");
    }

    [Test]
    public async Task ChangeEmail_WithSameEmail_ShouldNotChangeEmail()
    {
        User user = CreateUser(email: "test@test.com");

        user.ChangeEmail(newEmail: "test@test.com");

        await Assert.That(value: user.Email).IsEqualTo(expected: "test@test.com");
    }

    [Test]
    public async Task ChangeEmail_WithEmptyEmail_ShouldThrowEmptyEmailException()
    {
        User user = CreateUser();

        await Assert.That(action: () => user.ChangeEmail(newEmail: String.Empty)).Throws<EmailException>();
    }

    [Test]
    public async Task ChangePassword_WithValidHash_ShouldChangePassword()
    {
        User user = CreateUser();

        user.ChangePassword(newPasswordHash: "newHash");

        await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "newHash");
    }

    [Test]
    public async Task ChangePassword_WithEmptyHash_ShouldThrowArgumentException()
    {
        User user = CreateUser();

        await Assert.That(action: () => user.ChangePassword(newPasswordHash: String.Empty)).Throws<PasswordException>();
    }

    [Test]
    public async Task ChangeBaseCurrency_WithValidCode_ShouldChangeBaseCurrency()
    {
        User user = CreateUser();

        user.ChangeBaseCurrency(newBaseCurrencyCode: "USD");

        await Assert.That(value: user.BaseCurrencyCode).IsEqualTo(expected: "USD");
    }

    [Test]
    public async Task ChangeBaseCurrency_WithSameCode_ShouldNotChangeBaseCurrency()
    {
        User user = CreateUser(baseCurrencyCode: "RUB");

        user.ChangeBaseCurrency(newBaseCurrencyCode: "RUB");

        await Assert.That(value: user.BaseCurrencyCode).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task ChangeBaseCurrency_WithEmptyCode_ShouldThrowArgumentException()
    {
        User user = CreateUser();

        await Assert.That(action: () => user.ChangeBaseCurrency(newBaseCurrencyCode: String.Empty)).Throws<CurrencyException>();
    }
}