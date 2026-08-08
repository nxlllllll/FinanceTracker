using System.Security.Claims;
using FinanceTracker.Api.Security;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Services.Token;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class JwtBearerOptionsSetupTests
{
	private IDatabase _database = null!;
	private JwtBearerOptionsSetup _setup = null!;
	private JwtBearerOptions _options = null!;
	private DefaultHttpContext _httpContext = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_database = Substitute.For<IDatabase>();

		IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: _database);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions { InstanceName = "ft_test:" });

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);

		ServiceCollection services = new ServiceCollection();
		services.AddSingleton(implementationInstance: redisCache);
		IServiceProvider serviceProvider = services.BuildServiceProvider();

		IOptions<JwtOptions> jwtOptions = Substitute.For<IOptions<JwtOptions>>();
		jwtOptions.Value.Returns(returnThis: new JwtOptions
		{
			Secret = new String(c: '0', count: 32),
			Issuer = "test-issuer",
			Audience = "test-audience"
		});

		_setup = new JwtBearerOptionsSetup(jwtOptions: jwtOptions);
		_options = new JwtBearerOptions();
		_setup.Configure(name: JwtBearerDefaults.AuthenticationScheme, options: _options);

		_httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
	}

	private TokenValidatedContext BuildContext(Guid? sessionId)
	{
		AuthenticationScheme scheme = new AuthenticationScheme(
			name: JwtBearerDefaults.AuthenticationScheme,
			displayName: null,
			handlerType: typeof(JwtBearerHandler)
		);

		List<Claim> claims = [];
		if (sessionId is { } id)
			claims.Add(item: new Claim(type: JwtRegisteredClaimNames.Sid, value: id.ToString()));

		ClaimsPrincipal principal = new ClaimsPrincipal(identity: new ClaimsIdentity(claims: claims, authenticationType: "Bearer"));

		return new TokenValidatedContext(context: _httpContext, scheme: scheme, options: _options)
		{
			Principal = principal
		};
	}

	private async Task InvokeOnTokenValidatedAsync(TokenValidatedContext context)
		=> await _options.Events.OnTokenValidated(context);

	[Test]
	public async Task OnTokenValidated_WithNoSidClaim_ShouldFail()
	{
		TokenValidatedContext context = BuildContext(sessionId: null);

		await InvokeOnTokenValidatedAsync(context: context);

		await Assert.That(value: context.Result?.Succeeded).IsFalse();
	}

	[Test]
	public async Task OnTokenValidated_WithMalformedSidClaim_ShouldFail()
	{
		AuthenticationScheme scheme = new AuthenticationScheme(
			name: JwtBearerDefaults.AuthenticationScheme,
			displayName: null,
			handlerType: typeof(JwtBearerHandler)
		);
		ClaimsPrincipal principal = new ClaimsPrincipal(identity: new ClaimsIdentity(
			claims: [new Claim(type: JwtRegisteredClaimNames.Sid, value: "not-a-guid")],
			authenticationType: "Bearer"
		));
		TokenValidatedContext context = new TokenValidatedContext(context: _httpContext, scheme: scheme, options: _options)
		{
			Principal = principal
		};

		await InvokeOnTokenValidatedAsync(context: context);

		await Assert.That(value: context.Result?.Succeeded).IsFalse();
	}

	[Test]
	public async Task OnTokenValidated_WhenSessionIsNotRevoked_ShouldNotFail()
	{
		Guid sessionId = Guid.CreateVersion7();
		_database.StringGetAsync(
			key: Arg.Any<RedisKey>(),
			flags: Arg.Any<CommandFlags>()
		).Returns(returnThis: RedisValue.Null);

		TokenValidatedContext context = BuildContext(sessionId: sessionId);

		await InvokeOnTokenValidatedAsync(context: context);

		await Assert.That(value: context.Result).IsNull();
	}

	[Test]
	public async Task OnTokenValidated_WhenSessionIsRevoked_ShouldFail()
	{
		Guid sessionId = Guid.CreateVersion7();
		byte[] serializedTrue = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value: true);
		_database.StringGetAsync(
			key: Arg.Is<RedisKey>(k => ((string)k!).EndsWith(value: $"revoked-session:{sessionId}")),
			flags: Arg.Any<CommandFlags>()
		).Returns(returnThis: (RedisValue)serializedTrue);

		TokenValidatedContext context = BuildContext(sessionId: sessionId);

		await InvokeOnTokenValidatedAsync(context: context);

		await Assert.That(value: context.Result?.Succeeded).IsFalse();
	}

	[Test]
	public async Task OnTokenValidated_WhenRedisIsUnavailable_ShouldFailOpenAndNotFailTheToken()
	{
		Guid sessionId = Guid.CreateVersion7();
		_database.StringGetAsync(
			key: Arg.Any<RedisKey>(),
			flags: Arg.Any<CommandFlags>()
		).Returns<RedisValue>(returnThis: _ => throw new RedisConnectionException(
			failureType: ConnectionFailureType.UnableToConnect,
			message: "down",
			flags: CommandFlags.None));

		TokenValidatedContext context = BuildContext(sessionId: sessionId);

		await InvokeOnTokenValidatedAsync(context: context);

		await Assert.That(value: context.Result).IsNull();
	}
}
