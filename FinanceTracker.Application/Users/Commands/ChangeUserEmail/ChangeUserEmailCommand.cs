using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Users.Commands.ChangeUserEmail;

public sealed record ChangeUserEmailCommand(
	Guid UserId,
	string NewEmail
) : IRequest<Guid>, IAuthorizable;