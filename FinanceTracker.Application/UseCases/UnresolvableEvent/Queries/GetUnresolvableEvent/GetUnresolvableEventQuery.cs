using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Queries.GetUnresolvableEvent;

public sealed record GetUnresolvableEventQuery(
	Guid EventId,
	Guid UserId
) : IRequest<Result<UnresolvableEventDetail, AppException>>, IUserScopedRequest;
