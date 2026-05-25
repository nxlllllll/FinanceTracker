using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetUser;

public sealed class GetUserHandler(
	IUserReadRepository userReadRepository
) : IRequestHandler<GetUserQuery, User?>
{
	public async Task<User?> Handle(
		GetUserQuery query,
		CancellationToken ct = default
	) => await userReadRepository.GetByIdAsync(userId: query.UserId, ct: ct);
}
