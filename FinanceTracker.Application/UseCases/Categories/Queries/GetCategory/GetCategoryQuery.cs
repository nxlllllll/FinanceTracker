using FinanceTracker.Core.Domains.Category;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Queries.GetCategory;

public sealed record GetCategoryQuery(Guid CategoryId, Guid UserId) : IRequest<Category?>;