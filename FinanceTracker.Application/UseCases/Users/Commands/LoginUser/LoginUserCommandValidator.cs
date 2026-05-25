using FluentValidation;

namespace FinanceTracker.Application.UseCases.Users.Commands.LoginUser;

public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
	public LoginUserCommandValidator()
	{
		RuleFor(expression: x => x.Email)
			.Must(predicate: email => email != default).WithMessage(errorMessage: "The email cannot be empty.");

		RuleFor(expression: x => x.Password)
			.NotEmpty().WithMessage(errorMessage: "Password is required.")
			.MinimumLength(minimumLength: 8).WithMessage(errorMessage: "Password must be at least 8 characters.");
	}
}
