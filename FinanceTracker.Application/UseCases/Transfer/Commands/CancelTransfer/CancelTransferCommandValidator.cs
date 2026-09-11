using FluentValidation;

namespace FinanceTracker.Application.UseCases.Transfer.Commands.CancelTransfer;

public sealed class CancelTransferCommandValidator : AbstractValidator<CancelTransferCommand>
{
	public CancelTransferCommandValidator()
	{
		RuleFor(expression: x => x.UserId).NotEmpty();
		RuleFor(expression: x => x.TransferId).NotEmpty();
	}
}
