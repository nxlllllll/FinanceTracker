using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserEmail;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class ChangeUserEmailCommandValidatorTests
{
    private readonly ChangeUserEmailCommandValidator _validator = new ChangeUserEmailCommandValidator();

    [Test]
    public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        ChangeUserEmailCommand command = new ChangeUserEmailCommand(
            UserId: Guid.NewGuid(),
            NewEmail: "new@test.com"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyUserId_ShouldHaveError()
    {
        ChangeUserEmailCommand command = new ChangeUserEmailCommand(
            UserId: Guid.Empty,
            NewEmail: "new@test.com"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.UserId)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithEmptyEmail_ShouldHaveError()
    {
        ChangeUserEmailCommand command = new ChangeUserEmailCommand(
            UserId: Guid.NewGuid(),
            NewEmail: String.Empty
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.NewEmail)
        )).IsTrue();
    }

    [Test]
    public async Task Validate_WithInvalidEmail_ShouldHaveError()
    {
        ChangeUserEmailCommand command = new ChangeUserEmailCommand(
            UserId: Guid.NewGuid(),
            NewEmail: "notanemail"
        );

        ValidationResult result = await _validator.ValidateAsync(instance: command);

        await Assert.That(value: result.IsValid).IsFalse();
        await Assert.That(value: result.Errors.Any(
            predicate: e => e.PropertyName == nameof(command.NewEmail)
        )).IsTrue();
    }
}