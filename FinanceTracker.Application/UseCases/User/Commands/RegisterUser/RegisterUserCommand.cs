using System.Net;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.RegisterUser;

public sealed record RegisterUserCommand(
	Email Email,
	string Password,
	Core.ValueObjects.Currency BaseCurrencyCode,
	IPAddress IpAddress
) : IIdempotentCommand, IRequest<Result<Guid, AppException>>, IIpScopedRequest, IEmailScopedRequest
{
	public Guid IdempotencyKey { get; init; }
}
