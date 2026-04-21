using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Users.Queries.GetUser;

public sealed class GetUserHandler(
	IUserRepository userRepository
) : IRequestHandler<GetUserQuery, User?>
{
	public async Task<User?> Handle(
		GetUserQuery query,
		CancellationToken ct = default
	) => await userRepository.GetByIdAsync(userId: query.UserId, ct: ct);
}