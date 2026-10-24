using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace WalletFlow.WebApi.Security;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private const string HeaderName = "X-API-Key";

    private readonly string? _expectedApiKey;
    private readonly bool _isDevelopment;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration,
        IWebHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _expectedApiKey = configuration["Authentication:ApiKey"];
        _isDevelopment = environment.IsDevelopment();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (_isDevelopment)
        {
            return Task.FromResult(CreateSuccessResult());
        }

        if (string.IsNullOrWhiteSpace(_expectedApiKey) ||
            !Request.Headers.TryGetValue(HeaderName, out var providedApiKey) ||
            providedApiKey.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail("A chave da API é inválida ou não foi informada."));
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_expectedApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedApiKey[0]!);

        return Task.FromResult(
            CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes)
                ? CreateSuccessResult()
                : AuthenticateResult.Fail("A chave da API é inválida ou não foi informada."));
    }

    private AuthenticateResult CreateSuccessResult()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "api-client")],
            SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
