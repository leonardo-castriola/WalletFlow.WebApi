
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using WalletFlow.Api.Middleware;
using WalletFlow.Api.Security;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.Services;
using WalletFlow.Infrastructure;
using WalletFlow.Api.Background;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTRO DE SERVIÇOS
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuração da API Key
var apiKey = builder.Configuration["Authentication:ApiKey"];

if (!builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "Configure a chave da API na variável de ambiente " +
        "Authentication__ApiKey antes de iniciar fora de Development.");
}

// Autenticação
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<
        Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });

// Autorização: exige autenticação por padrão
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
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
app.UseAuthorization();

// Controllers
app.MapControllers();

app.Run();

public partial class Program { }
