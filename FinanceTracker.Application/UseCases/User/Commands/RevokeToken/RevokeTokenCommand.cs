using System.Net;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.User.Commands.RevokeToken;

public sealed record RevokeTokenCommand(
	string RefreshToken,
	IPAddress IpAddress
) : IRequest<Result<Unit, DomainException>>, IIpScopedRequest;