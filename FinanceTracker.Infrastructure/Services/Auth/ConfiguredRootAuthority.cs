using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Infrastructure.Configurations.Options;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Services.Auth;

public sealed class ConfiguredRootAuthority(
	IOptionsMonitor<AuthorizationOptions> options
) : IRootAuthority
{
	public bool IsRoot(Guid userId)
	{
		if (userId == Guid.Empty)
			return false;

		return userId == options.CurrentValue.RootUserId;
	}
}
