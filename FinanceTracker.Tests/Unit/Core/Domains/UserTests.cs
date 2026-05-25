using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Domains.User.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class UserTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	[Test]
	public async Task Register_WithValidData_ShouldSetCorrectState()
	{
		User user = UserFactory.Create().Value!;

		await Assert.That(value: user.Id).IsNotDefault();
		await Assert.That(value: user.Email.Value).IsEqualTo(expected: "test@test.com");
		await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "hash");
		await Assert.That(value: user.BaseCurrency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: user.CreatedAt).IsNotDefault();
	}

	[Test]
	public async Task Register_WithValidData_ShouldRaiseUserRegisteredEvent()
	{
		User user = UserFactory.Create().Value!;

		await Assert.That(value: user.DomainEvents).Count().IsEqualTo(expected: 1);
		await Assert.That(value: user.DomainEvents[0]).IsTypeOf<UserRegistered>();
	}

	[Test]
	public async Task Register_WithEmptyEmail_ShouldReturnEmailException()
	{
		Result<User, DomainException> result = UserFactory.Create(email: String.Empty);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmailException>();
	}

	[Test]
	public async Task Register_WithEmptyPasswordHash_ShouldReturnPasswordException()
	{
		Result<User, DomainException> result = UserFactory.Create(passwordHash: String.Empty);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<PasswordException>();
	}

	[Test]
	public async Task Register_WithEmptyBaseCurrencyCode_ShouldReturnCurrencyException()
	{
		Result<User, DomainException> result = UserFactory.Create(baseCurrencyCode: String.Empty);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}

	[Test]
	public async Task ChangeEmail_WithNewEmail_ShouldChangeEmail()
	{
		User user = UserFactory.Create().Value!;

		user.ChangeEmail(newEmail: Email.Create(value: "new@test.com").Value, occurredAt: Now);

		await Assert.That(value: user.Email.Value).IsEqualTo(expected: "new@test.com");
	}

	[Test]
	public async Task ChangeEmail_WithNewEmail_ShouldRaiseUserEmailChangedEvent()
	{
		User user = UserFactory.Create().Value!;
		user.ClearDomainEvents();

		user.ChangeEmail(newEmail: Email.Create(value: "new@test.com").Value, occurredAt: Now);

		await Assert.That(value: user.DomainEvents).Count().IsEqualTo(expected: 1);
		await Assert.That(value: user.DomainEvents[0]).IsTypeOf<UserEmailChanged>();
	}

	[Test]
	public async Task ChangeEmail_WithSameEmail_ShouldNotRaiseDomainEvent()
	{
		User user = UserFactory.Create().Value!;
		user.ClearDomainEvents();

		user.ChangeEmail(newEmail: Email.Create(value: "test@test.com").Value, occurredAt: Now);

		await Assert.That(value: user.DomainEvents).IsEmpty();
	}

	[Test]
	public async Task ChangePassword_WithValidHash_ShouldChangePassword()
	{
		User user = UserFactory.Create().Value!;

		user.ChangePassword(newPasswordHash: "newHash", occurredAt: Now);

		await Assert.That(value: user.PasswordHash).IsEqualTo(expected: "newHash");
	}

	[Test]
	public async Task ChangePassword_WithEmptyHash_ShouldReturnPasswordException()
	{
		User user = UserFactory.Create().Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = user.ChangePassword(newPasswordHash: String.Empty, occurredAt: Now);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<PasswordException>();
	}

	[Test]
	public async Task ChangeBaseCurrency_WithNewCurrency_ShouldChangeBaseCurrency()
	{
		User user = UserFactory.Create().Value!;

		user.ChangeBaseCurrency(newBaseCurrency: Currency.Create(value: "USD").Value, occurredAt: Now);

		await Assert.That(value: user.BaseCurrency.Value).IsEqualTo(expected: "USD");
	}

	[Test]
	public async Task ChangeBaseCurrency_WithNewCurrency_ShouldRaiseUserBaseCurrencyChangedEvent()
	{
		User user = UserFactory.Create().Value!;
		user.ClearDomainEvents();

		user.ChangeBaseCurrency(newBaseCurrency: Currency.Create(value: "USD").Value, occurredAt: Now);

		await Assert.That(value: user.DomainEvents).Count().IsEqualTo(expected: 1);
		await Assert.That(value: user.DomainEvents[0]).IsTypeOf<UserBaseCurrencyChanged>();
	}

	[Test]
	public async Task ChangeBaseCurrency_WithSameCurrency_ShouldNotRaiseDomainEvent()
	{
		User user = UserFactory.Create().Value!;
		user.ClearDomainEvents();

		user.ChangeBaseCurrency(newBaseCurrency: Currency.Create(value: "RUB").Value, occurredAt: Now);

		await Assert.That(value: user.DomainEvents).IsEmpty();
	}
	
	[Test]
	public async Task ChangePassword_WithValidHash_ShouldRaiseUserPasswordChangedEvent()
	{
		User user = UserFactory.Create().Value!;
		user.ClearDomainEvents();

		user.ChangePassword(newPasswordHash: "newHash", occurredAt: Now);

		await Assert.That(value: user.DomainEvents).Count().IsEqualTo(expected: 1);
		await Assert.That(value: user.DomainEvents[0]).IsTypeOf<UserPasswordChanged>();
	}
}
