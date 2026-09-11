using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Commands.ResolveUnresolvableEvent;

public sealed record ResolveUnresolvableEventCommand(
	Guid UserId,
	Guid EventId
) : IRequest<Result<Guid, AppException>>, IUserScopedRequest;
