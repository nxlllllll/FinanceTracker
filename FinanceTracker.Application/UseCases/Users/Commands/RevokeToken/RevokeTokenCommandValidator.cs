using FluentValidation;

namespace FinanceTracker.Application.UseCases.Users.Commands.RevokeToken;

public sealed class RevokeTokenCommandValidator : AbstractValidator<RevokeTokenCommand>
{
	public RevokeTokenCommandValidator()
	{
		RuleFor(expression: x => x.RefreshToken)
			.NotEmpty().WithMessage(errorMessage: "Refresh token is required.");
	}
}
