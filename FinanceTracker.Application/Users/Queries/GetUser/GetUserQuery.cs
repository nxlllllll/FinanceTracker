using FinanceTracker.Core.Domains.User;
using MediatR;

namespace FinanceTracker.Application.Users.Queries.GetUser;

public sealed record GetUserQuery(Guid UserId) : IRequest<User?>;