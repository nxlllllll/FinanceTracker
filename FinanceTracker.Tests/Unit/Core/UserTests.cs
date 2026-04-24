using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class UserTests
{
    [Test]
    public async Task Register_WithValidData_ShouldSetCorrectState()
    {
        User user = UserFactory.Create();

        await Assert.That(value: user.Id).IsNotDefault();
        await Assert.That(value: user.Email).IsEqualTo(expected: "test@test.com");
        await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "hash");
        await Assert.That(value: user.BaseCurrencyCode).IsEqualTo(expected: "RUB");
        await Assert.That(value: user.CreatedAt).IsNotDefault();
    }

    [Test]
    public async Task Register_WithEmptyEmail_ShouldThrowEmptyEmailException()
        => await Assert.That(action: () => UserFactory.Create(email: String.Empty)).Throws<EmailException>();

    [Test]
    public async Task Register_WithEmptyPasswordHash_ShouldThrowPasswordException()
        => await Assert.That(action: () => UserFactory.Create(passwordHash: String.Empty)).Throws<PasswordException>();

    [Test]
    public async Task Register_WithEmptyBaseCurrencyCode_ShouldThrowCurrencyException()
        => await Assert.That(action: () => UserFactory.Create(baseCurrencyCode: String.Empty)).Throws<CurrencyException>();

    [Test]
    public async Task ChangeEmail_WithValidEmail_ShouldChangeEmail()
    {
        User user = UserFactory.Create();

        user.ChangeEmail(newEmail: "new@test.com");

        await Assert.That(value: user.Email).IsEqualTo(expected: "new@test.com");
    }

    [Test]
    public async Task ChangeEmail_WithSameEmail_ShouldNotChangeEmail()
    {
        User user = UserFactory.Create();

        user.ChangeEmail(newEmail: "test@test.com");

        await Assert.That(value: user.Email).IsEqualTo(expected: "test@test.com");
    }

    [Test]
    public async Task ChangeEmail_WithEmptyEmail_ShouldThrowEmptyEmailException()
    {
        User user = UserFactory.Create();

        await Assert.That(action: () => user.ChangeEmail(newEmail: String.Empty)).Throws<EmailException>();
    }

    [Test]
    public async Task ChangePassword_WithValidHash_ShouldChangePassword()
    {
        User user = UserFactory.Create();

        user.ChangePassword(newPasswordHash: "newHash");

        await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "newHash");
    }

    [Test]
    public async Task ChangePassword_WithEmptyHash_ShouldThrowArgumentException()
    {
        User user = UserFactory.Create();

        await Assert.That(action: () => user.ChangePassword(newPasswordHash: String.Empty)).Throws<PasswordException>();
    }

    [Test]
    public async Task ChangeBaseCurrency_WithValidCode_ShouldChangeBaseCurrency()
    {
        User user = UserFactory.Create();

        user.ChangeBaseCurrency(newBaseCurrencyCode: "USD");

        await Assert.That(value: user.BaseCurrencyCode).IsEqualTo(expected: "USD");
    }

    [Test]
    public async Task ChangeBaseCurrency_WithSameCode_ShouldNotChangeBaseCurrency()
    {
        User user = UserFactory.Create(baseCurrencyCode: "RUB");

        user.ChangeBaseCurrency(newBaseCurrencyCode: "RUB");

        await Assert.That(value: user.BaseCurrencyCode).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task ChangeBaseCurrency_WithEmptyCode_ShouldThrowArgumentException()
    {
        User user = UserFactory.Create();

        await Assert.That(action: () => user.ChangeBaseCurrency(newBaseCurrencyCode: String.Empty)).Throws<CurrencyException>();
    }
}