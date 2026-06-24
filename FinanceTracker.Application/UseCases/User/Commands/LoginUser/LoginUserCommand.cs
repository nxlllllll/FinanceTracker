using System.Net;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.LoginUser;

public sealed record LoginUserCommand(
	Email Email,
	string Password,
	IPAddress IpAddress
) : IRequest<Result<SessionToken, DomainException>>, IIpScopedRequest, IEmailScopedRequest;