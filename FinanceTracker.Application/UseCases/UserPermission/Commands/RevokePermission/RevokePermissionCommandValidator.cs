using FluentValidation;

namespace FinanceTracker.Application.UseCases.UserPermission.Commands.RevokePermission;

public sealed class RevokePermissionCommandValidator : AbstractValidator<RevokePermissionCommand>
{
	public RevokePermissionCommandValidator()
	{
		RuleFor(expression: command => command.TargetUserId)
			.NotEmpty().WithMessage(errorMessage: "The target user cannot be empty.");

		RuleFor(expression: command => command.RevokedBy)
			.NotEmpty().WithMessage(errorMessage: "The revoking user cannot be empty.");
	}
}
