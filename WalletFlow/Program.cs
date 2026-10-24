using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.Security;
using WalletFlow.WebApi.Services;
using WalletFlow.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTRO DE SERVIÇOS
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var apiKey = builder.Configuration["Authentication:ApiKey"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "Configure a chave da API na variável de ambiente Authentication__ApiKey antes de iniciar fora de Development.");
}

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// EF Core com SQLite
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Nosso serviço financeiro
builder.Services.AddScoped<IFinanceiroService, FinanceiroService>();

var app = builder.Build();

// Cria ou atualiza o banco usando as migrações do Entity Framework.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
    await dbContext.Database.MigrateAsync();
}

// 2. PIPELINE DE REQUISIÇÕES (Middlewares)
app.UseMiddleware<GlobalExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.RoutePrefix = "swagger");
}

// Força a cultura invariante internacional (evita problemas com ponto/vírgula no JSON)
var supportedCultures = new[] { CultureInfo.InvariantCulture };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
