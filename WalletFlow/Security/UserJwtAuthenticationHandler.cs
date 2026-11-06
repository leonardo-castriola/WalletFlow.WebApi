using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WalletFlow.Application.Abstractions;

namespace WalletFlow.Api.Security;

public sealed class UserJwtAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IUsuarioConsulta usuarioConsulta)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "WalletFlowUserJwt";

    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        configuration["Authentication:Jwt:SigningKey"]
        ?? throw new InvalidOperationException("Authentication:Jwt:SigningKey não foi configurada."));
    private readonly string _issuer = configuration["Authentication:Jwt:Issuer"]
        ?? throw new InvalidOperationException("Authentication:Jwt:Issuer não foi configurado.");
    private readonly string _audience = configuration["Authentication:Jwt:Audience"]
        ?? throw new InvalidOperationException("Authentication:Jwt:Audience não foi configurado.");

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization;
        if (authorization.Count == 0)
            return AuthenticateResult.NoResult();
        if (authorization.Count != 1 ||
            !System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(authorization[0], out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
            return AuthenticateResult.Fail("O token de usuário não é válido.");

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(
                header.Parameter,
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(_signingKey),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
                },
                out _);

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(userId))
                return AuthenticateResult.Fail("O token não identifica um usuário.");

            if (!await usuarioConsulta.ExisteAsync(userId, Context.RequestAborted))
                return AuthenticateResult.Fail("A conta do usuário não existe mais.");

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)],
                SchemeName);
            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException)
        {
            Logger.LogInformation("A API rejeitou um token de usuário inválido.");
            return AuthenticateResult.Fail("O token de usuário é inválido ou expirou.");
        }
    }
}
