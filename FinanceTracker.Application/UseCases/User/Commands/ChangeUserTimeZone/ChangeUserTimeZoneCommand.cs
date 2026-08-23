using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Commands.ChangeUserTimeZone;

public sealed record ChangeUserTimeZoneCommand(
	Guid UserId,
	TimeZoneId NewTimeZone
) : IRequest<Result<Guid, AppException>>, IAuthorizable;
