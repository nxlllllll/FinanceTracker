using MediatR;

namespace FinanceTracker.Application.Categories.Commands.ArchiveCategory;

public sealed record ArchiveCategoryCommand(Guid CategoryId) : IRequest;