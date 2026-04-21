using FinanceTracker.Application.Users.Commands.RegisterUser;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class RegisterUserCommandValidatorTests
{
	private readonly RegisterUserCommandValidator _validator = new();

    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        RegisterUserCommand command = new RegisterUserCommand(
            Email: "test@test.com",
            PasswordHash: "hash",
            BaseCurrencyCode: "RUB"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyEmail_ShouldHaveError()
    {
        RegisterUserCommand command = new RegisterUserCommand(
            Email: String.Empty,
            PasswordHash: "hash",
            BaseCurrencyCode: "RUB"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Email)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithInvalidEmail_ShouldHaveError()
    {
        RegisterUserCommand command = new RegisterUserCommand(
            Email: "notanemail",
            PasswordHash: "hash",
            BaseCurrencyCode: "RUB"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.Email)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyPasswordHash_ShouldHaveError()
    {
        RegisterUserCommand command = new RegisterUserCommand(
            Email: "test@test.com",
            PasswordHash: String.Empty,
            BaseCurrencyCode: "RUB"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.PasswordHash)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithInvalidBaseCurrencyCode_ShouldHaveError()
    {
        RegisterUserCommand command = new RegisterUserCommand(
            Email: "test@test.com",
            PasswordHash: "hash",
            BaseCurrencyCode: "RU"
        );

        ValidationResult result = await _validator.ValidateAsync(command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.BaseCurrencyCode)
        )).IsTrue();
    }
}