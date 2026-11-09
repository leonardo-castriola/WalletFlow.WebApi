
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using WalletFlow.Api.Middleware;
using WalletFlow.Api.Security;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.Services;
using WalletFlow.Infrastructure;
using WalletFlow.Api.Background;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTRO DE SERVIÇOS
DependencyInjection.ValidateProductionAllowedHosts(builder.Configuration, builder.Environment);
builder.Services.AddWalletFlowForwardedHeaders(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 120);
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuração da API Key
var apiKey = builder.Configuration["Authentication:ApiKey"];
var jwtSigningKey = builder.Configuration["Authentication:Jwt:SigningKey"];
var jwtIssuer = builder.Configuration["Authentication:Jwt:Issuer"];
var jwtAudience = builder.Configuration["Authentication:Jwt:Audience"];

if (!builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "Configure a chave da API na variável de ambiente " +
        "Authentication__ApiKey antes de iniciar fora de Development.");
}
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32 ||
    string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Configure Authentication:Jwt:SigningKey (pelo menos 32 bytes), Authentication:Jwt:Issuer e Authentication:Jwt:Audience. " +
        "Use a mesma chave no host Blazor e na API; em produção, configure-a por variável de ambiente ou gerenciador de segredos.");
}

// A API exige, na mesma chamada, a identidade assinada do usuário e a credencial do serviço Blazor.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
    })
    .AddScheme<
        Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { })
    .AddScheme<
        Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        UserJwtAuthenticationHandler>(
        UserJwtAuthenticationHandler.SchemeName,
        _ => { });

var apiUserPolicy = new AuthorizationPolicyBuilder(
        ApiKeyAuthenticationHandler.SchemeName,
        UserJwtAuthenticationHandler.SchemeName)
    .RequireAuthenticatedUser()
    .RequireClaim(ApiKeyAuthenticationHandler.ServiceClaimType, ApiKeyAuthenticationHandler.ServiceClaimValue)
    .RequireClaim(ClaimTypes.NameIdentifier)
    .Build();

// Todos os endpoints exigem a chave do serviço e um JWT válido vinculado ao usuário.
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = apiUserPolicy;
    options.FallbackPolicy = apiUserPolicy;
});

builder.Services.AddWalletFlowInfrastructure(builder.Configuration, builder.Environment);

// Casos de uso da aplicação
builder.Services.AddScoped<ITransacaoService, TransacaoService>();
builder.Services.AddSingleton<IExtratoImportador, ExtratoImportador>();
builder.Services.Configure<MonthlyReportScheduleOptions>(builder.Configuration.GetSection("Reports:Monthly"));
builder.Services.AddHostedService<MonthlyFinancialReportBackgroundService>();

// 2. CONSTRUÇÃO DA APLICAÇÃO
var app = builder.Build();

await app.Services.ApplyWalletFlowMigrationsAsync();

// 3. PIPELINE DE REQUISIÇÕES
app.UseForwardedHeaders();

// Tratamento global de exceções
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger somente em Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.RoutePrefix = "swagger");
}

// Cultura invariante
var supportedCultures = new[]
{
    CultureInfo.InvariantCulture
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture =
        new RequestCulture(CultureInfo.InvariantCulture),

    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// O perfil local usa HTTP; fora de Development a aplicação exige HTTPS.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Autenticação e autorização
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Health check e Controllers
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program { }
