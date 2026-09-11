using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using MediatR;
using UnresolvableEventReadModel = FinanceTracker.Core.ReadModels.UnresolvableEvent.UnresolvableEvent;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Queries.GetUnresolvableEvents;

public sealed class GetUnresolvableEventsHandler(
	IUnresolvableEventReadRepository unresolvableEventReadRepository
) : IRequestHandler<GetUnresolvableEventsQuery, Result<PagedResult<UnresolvableEventReadModel>, AppException>>
{
	public async Task<Result<PagedResult<UnresolvableEventReadModel>, AppException>> Handle(
		GetUnresolvableEventsQuery query,
		CancellationToken ct = default)
	{
		return Result<PagedResult<UnresolvableEventReadModel>, AppException>.Success(value: await unresolvableEventReadRepository.GetAllAsync(
			type: query.Type,
			isAcknowledged: query.IsAcknowledged,
			isResolved: query.IsResolved,
			cursorOccurredAt: query.CursorOccurredAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		));
	}
}
