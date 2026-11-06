using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace WalletFlow.Api.Security;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string ServiceClaimType = "walletflow:service";
    public const string ServiceClaimValue = "blazor";
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
        if (string.IsNullOrWhiteSpace(_expectedApiKey))
        {
            if (!_isDevelopment)
                return Task.FromResult(AuthenticateResult.Fail("A chave da API é inválida ou não foi informada."));
        }
        else
        {
            var providedApiKey = Request.Headers[HeaderName];
            if (providedApiKey.Count != 1)
                return Task.FromResult(AuthenticateResult.Fail("A chave da API é inválida ou não foi informada."));

            var expectedBytes = Encoding.UTF8.GetBytes(_expectedApiKey);
            var providedBytes = Encoding.UTF8.GetBytes(providedApiKey[0]!);
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
                return Task.FromResult(AuthenticateResult.Fail("A chave da API é inválida ou não foi informada."));
        }

        return Task.FromResult(CreateSuccessResult());
    }

    private AuthenticateResult CreateSuccessResult()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ServiceClaimType, ServiceClaimValue)],
            SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
