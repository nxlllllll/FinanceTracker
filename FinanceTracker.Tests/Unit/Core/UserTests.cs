using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class UserTests
{
    [Test]
    public async Task Register_WithValidData_ShouldSetCorrectState()
    {
        User user = UserFactory.Create().Value!;

        await Assert.That(value: user.Id).IsNotDefault();
        await Assert.That(value: user.Email).IsEqualTo(expected: "test@test.com");
        await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "hash");
        await Assert.That(value: user.BaseCurrency).IsEqualTo(expected: "RUB");
        await Assert.That(value: user.CreatedAt).IsNotDefault();
    }

    [Test]
    public async Task Register_WithEmptyEmail_ShouldThrowEmptyEmailException()
    {
        Result<User, DomainException> result = UserFactory.Create(email: String.Empty);
        
        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<EmailException>();
    }

    [Test]
    public async Task Register_WithEmptyPasswordHash_ShouldThrowPasswordException()
    {
        Result<User, DomainException> result = UserFactory.Create(passwordHash: String.Empty);
        
        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<PasswordException>();
    }

    [Test]
    public async Task Register_WithEmptyBaseCurrencyCode_ShouldThrowCurrencyException()
    {
        Result<User, DomainException> result = UserFactory.Create(baseCurrencyCode: String.Empty);
        
        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
    }

    [Test]
    public async Task ChangeEmail_WithValidEmail_ShouldChangeEmail()
    {
        User user = UserFactory.Create().Value!;

        user.ChangeEmail(newEmail: "new@test.com");

        await Assert.That(value: user.Email).IsEqualTo(expected: "new@test.com");
    }

    [Test]
    public async Task ChangeEmail_WithSameEmail_ShouldNotChangeEmail()
    {
        User user = UserFactory.Create().Value!;

        user.ChangeEmail(newEmail: "test@test.com");

        await Assert.That(value: user.Email).IsEqualTo(expected: "test@test.com");
    }

    [Test]
    public async Task ChangePassword_WithValidHash_ShouldChangePassword()
    {
        User user = UserFactory.Create().Value!;

        user.ChangePassword(newPasswordHash: "newHash");

        await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "newHash");
    }

    [Test]
    public async Task ChangePassword_WithEmptyHash_ShouldThrowArgumentException()
    {
        User user = UserFactory.Create().Value!;

        Result<FinanceTracker.Core.Results.Unit, DomainException> result = user.ChangePassword(newPasswordHash: String.Empty);
        
        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<PasswordException>();
    }

    [Test]
    public async Task ChangeBaseCurrency_WithValidCode_ShouldChangeBaseCurrency()
    {
        User user = UserFactory.Create().Value!;

        user.ChangeBaseCurrency(newBaseCurrency: "USD");

        await Assert.That(value: user.BaseCurrency).IsEqualTo(expected: "USD");
    }

    [Test]
    public async Task ChangeBaseCurrency_WithSameCode_ShouldNotChangeBaseCurrency()
    {
        User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;

        user.ChangeBaseCurrency(newBaseCurrency: "RUB");

        await Assert.That(value: user.BaseCurrency).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task ChangeBaseCurrency_WithEmptyCode_ShouldThrowArgumentException()
    {
        User user = UserFactory.Create().Value!;

        Result<Currency, DomainException> currencyResult = Currency.Create(value: String.Empty);
        await Assert.That(value: currencyResult.IsFailure).IsTrue();
        await Assert.That(value: currencyResult.Error).IsTypeOf<CurrencyException>();
    }
}