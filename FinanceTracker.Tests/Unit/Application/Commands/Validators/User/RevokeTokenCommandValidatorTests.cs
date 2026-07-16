using System.Net;
using FinanceTracker.Application.UseCases.User.Commands.RevokeToken;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Application.Commands.Validators.User;

public sealed class RevokeTokenCommandValidatorTests
{
	private readonly RevokeTokenCommandValidator _validator = new RevokeTokenCommandValidator();

	private static RevokeTokenCommand ValidCommand()
		=> new RevokeTokenCommand(RefreshToken: "some-refresh-token", IpAddress: IPAddress.Loopback);

	[Test]
	public async Task Validate_WithValidCommand_ShouldNotHaveErrors()
	{
		RevokeTokenCommand command = ValidCommand();

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsTrue();
	}

	[Test]
	public async Task Validate_WithEmptyRefreshToken_ShouldHaveError()
	{
		RevokeTokenCommand command = ValidCommand() with { RefreshToken = String.Empty };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.RefreshToken))).IsTrue();
	}

	[Test]
	public async Task Validate_WithNullIpAddress_ShouldHaveError()
	{
		RevokeTokenCommand command = ValidCommand() with { IpAddress = null! };

		ValidationResult result = await _validator.ValidateAsync(instance: command);

		await Assert.That(value: result.IsValid).IsFalse();
		await Assert.That(value: result.Errors.Any(predicate: e => e.PropertyName == nameof(command.IpAddress))).IsTrue();
	}
}
