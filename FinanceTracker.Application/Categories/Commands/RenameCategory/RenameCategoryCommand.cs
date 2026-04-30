using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Categories.Commands.RenameCategory;

public sealed record RenameCategoryCommand(
	Guid UserId,
	Guid CategoryId,
	string NewName
) : IRequest, IAuthorizable;