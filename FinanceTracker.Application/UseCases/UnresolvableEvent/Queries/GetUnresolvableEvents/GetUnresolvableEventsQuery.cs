using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;
using UnresolvableEventReadModel = FinanceTracker.Core.ReadModels.UnresolvableEvent.UnresolvableEvent;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Queries.GetUnresolvableEvents;

public sealed record GetUnresolvableEventsQuery(
	Guid UserId,
	UnresolvableEventType? Type = null,
	bool? IsAcknowledged = null,
	bool? IsResolved = null,
	DateTimeOffset? CursorOccurredAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<Result<PagedResult<UnresolvableEventReadModel>, AppException>>, IUserScopedRequest;
