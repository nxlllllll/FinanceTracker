using FinanceTracker.Core.Domains.Category;
using MediatR;

namespace FinanceTracker.Application.Categories.Queries.GetCategory;

public sealed record GetCategoryQuery(Guid CategoryId) : IRequest<Category?>;