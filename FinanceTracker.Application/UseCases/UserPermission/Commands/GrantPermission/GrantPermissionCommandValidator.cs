using FluentValidation;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.GrantPermission;

public sealed class GrantPermissionCommandValidator : AbstractValidator<GrantPermissionCommand>
{
	public GrantPermissionCommandValidator()
	{
		RuleFor(expression: command => command.TargetUserId)
			.NotEmpty().WithMessage(errorMessage: "The target user cannot be empty.");

		RuleFor(expression: command => command.GrantedBy)
			.NotEmpty().WithMessage(errorMessage: "The granting user cannot be empty.");
	}
}
