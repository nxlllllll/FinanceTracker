using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.UnresolvableEvent.Commands.AcknowledgeUnresolvableEvent;

public sealed record AcknowledgeUnresolvableEventCommand(
	Guid UserId,
	Guid EventId
) : IRequest<Result<Guid, AppException>>, IUserScopedRequest;
