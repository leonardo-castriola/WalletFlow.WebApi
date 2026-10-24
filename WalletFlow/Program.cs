
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.Security;
using WalletFlow.WebApi.Services;
using WalletFlow.WebApi.Middleware;

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

// Entity Framework Core com SQLite
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Serviço financeiro
builder.Services.AddScoped<IFinanceiroService, FinanceiroService>();

// 2. CONSTRUÇÃO DA APLICAÇÃO
var app = builder.Build();

// Aplica as migrações do banco de dados
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<WalletDbContext>();

    await dbContext.Database.MigrateAsync();
}

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

// HTTPS
app.UseHttpsRedirection();

// Autenticação e autorização
app.UseAuthentication();
app.UseAuthorization();

// Controllers
app.MapControllers();

app.Run();
