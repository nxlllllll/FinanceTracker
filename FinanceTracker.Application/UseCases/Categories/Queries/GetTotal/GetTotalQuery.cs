using FinanceTracker.Core.Dtos;
using MediatR;

namespace FinanceTracker.Application.UseCases.Categories.Queries.GetTotal;

public sealed record GetTotalQuery(
	Guid UserId,
	Guid CategoryId,
	DateOnly Period
) : IRequest<CategoryTotalDto?>;