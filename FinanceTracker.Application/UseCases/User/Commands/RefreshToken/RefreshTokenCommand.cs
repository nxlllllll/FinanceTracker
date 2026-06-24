using System.Net;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
	string RefreshToken,
	IPAddress IpAddress
) : IRequest<Result<SessionToken, DomainException>>, IIpScopedRequest;