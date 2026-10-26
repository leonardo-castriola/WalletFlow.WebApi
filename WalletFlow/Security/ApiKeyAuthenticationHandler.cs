using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Data;

namespace WalletFlow.WebApi.Security;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-API-Key";

    private readonly string? _expectedApiKey;
    private readonly bool _isDevelopment;
    private readonly WalletDbContext _dbContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        WalletDbContext dbContext)
        : base(options, logger, encoder)
    {
        _expectedApiKey = configuration["Authentication:ApiKey"];
        _isDevelopment = environment.IsDevelopment();
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_isDevelopment)
        {
            var providedApiKey = Request.Headers[HeaderName];
            if (string.IsNullOrWhiteSpace(_expectedApiKey) || providedApiKey.Count != 1)
                return AuthenticateResult.Fail("A chave da API é inválida ou não foi informada.");

            var expectedBytes = Encoding.UTF8.GetBytes(_expectedApiKey!);
            var providedBytes = Encoding.UTF8.GetBytes(providedApiKey[0]!);
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
                return AuthenticateResult.Fail("A chave da API é inválida ou não foi informada.");
        }

        if (!Request.Headers.TryGetValue("X-User-ID", out var providedUserId) ||
            providedUserId.Count != 1 ||
            string.IsNullOrWhiteSpace(providedUserId[0]))
            return AuthenticateResult.Fail("O identificador do usuário não foi informado.");

        var userId = providedUserId[0]!;
        var userExists = await _dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId, Context.RequestAborted);
        return userExists
            ? CreateSuccessResult(userId)
            : AuthenticateResult.Fail("O usuário informado não existe.");
    }

    private static AuthenticateResult CreateSuccessResult(string userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
