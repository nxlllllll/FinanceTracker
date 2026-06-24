using FluentValidation;

namespace FinanceTracker.Application.UseCases.User.Commands.RevokeToken;

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
	public RevokeTokenCommandValidator()
	{
		RuleFor(expression: x => x.RefreshToken)
			.NotEmpty().WithMessage(errorMessage: "Refresh token is required.");

		RuleFor(expression: x => x.IpAddress)
			.NotEmpty().WithMessage(errorMessage: "IP address is required.");
	}
}