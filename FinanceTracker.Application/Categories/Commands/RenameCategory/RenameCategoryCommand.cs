using MediatR;

namespace FinanceTracker.Application.Categories.Commands.RenameCategory;

public sealed record RenameCategoryCommand(
	Guid CategoryId,
	string NewName
) : IRequest;