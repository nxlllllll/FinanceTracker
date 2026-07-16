using System.Net;
using FinanceTracker.Application.UseCases.User.Commands.RefreshToken;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class RefreshTokenCommandValidatorTests
{
	private readonly RefreshTokenCommandValidator _validator = new RefreshTokenCommandValidator();

	private static RefreshTokenCommand ValidCommand()
		=> new RefreshTokenCommand(RefreshToken: "some-refresh-token", IpAddress: IPAddress.Loopback);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		RefreshTokenCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyRefreshToken_ShouldHaveError()
	{
		RefreshTokenCommand command = ValidCommand() with { RefreshToken = String.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.RefreshToken))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNullIpAddress_ShouldHaveError()
	{
		RefreshTokenCommand command = ValidCommand() with { IpAddress = null! };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.IpAddress))).IsTrue();
	}
}
