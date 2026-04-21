using MediatR;

namespace FinanceTracker.Application.Categories.Commands.UnarchiveCategory;

public sealed record UnarchiveCategoryCommand(Guid CategoryId) : IRequest;