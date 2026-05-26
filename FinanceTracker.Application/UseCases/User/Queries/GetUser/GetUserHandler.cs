using FinanceTracker.Core.Repositories.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetUser;

public sealed class GetUserHandler(
	IUserReadRepository userReadRepository
) : IRequestHandler<GetUserQuery, Core.Domains.User.User?>
{
	public async Task<Core.Domains.User.User?> Handle(
		GetUserQuery query,
		CancellationToken ct = default
	) => await userReadRepository.GetByIdAsync(userId: query.UserId, ct: ct);
}
