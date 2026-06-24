using FluentValidation;

namespace FinanceTracker.Application.UseCases.User.Commands.RefreshToken;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
	public RefreshTokenCommandValidator()
	{
		RuleFor(expression: x => x.RefreshToken)
			.NotEmpty().WithMessage(errorMessage: "Refresh token is required.");

		RuleFor(expression: x => x.IpAddress)
			.NotEmpty().WithMessage(errorMessage: "IP address is required.");
	}
}