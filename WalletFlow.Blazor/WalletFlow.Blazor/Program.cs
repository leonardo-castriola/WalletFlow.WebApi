using System.Threading.RateLimiting;
using WalletFlow.Blazor.Components;
using WalletFlow.Blazor.Components.Layout;
using WalletFlow.Blazor.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using WalletFlow.Application.Services;
using WalletFlow.Infrastructure;
using WalletFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

DependencyInjection.ValidateProductionAllowedHosts(builder.Configuration, builder.Environment);
builder.Services.AddWalletFlowForwardedHeaders(builder.Configuration);

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("WalletFlow");
var keysDirectory = builder.Configuration["DataProtection:KeysDirectory"];
if (!string.IsNullOrWhiteSpace(keysDirectory) || !builder.Environment.IsDevelopment())
{
    var resolvedKeysDir = string.IsNullOrWhiteSpace(keysDirectory)
        ? Path.Combine(builder.Environment.ContentRootPath, "dpkeys")
        : Path.GetFullPath(keysDirectory, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(resolvedKeysDir);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(resolvedKeysDir));
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 30);
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method))
            return RateLimitPartition.GetNoLimiter("read");

        var path = httpContext.Request.Path.Value ?? string.Empty;
        var isSensitiveAuthRoute = path.Equals("/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/criar-conta", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/esqueci-a-senha", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/redefinir-senha", StringComparison.OrdinalIgnoreCase);

        if (!isSensitiveAuthRoute)
            return RateLimitPartition.GetNoLimiter("general");

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();

builder.Services.AddWalletFlowInfrastructure(builder.Configuration, builder.Environment);
var requireConfirmedEmail = builder.Configuration.GetValue(
    "Identity:RequireConfirmedEmail",
    !builder.Environment.IsDevelopment());
builder.Services.AddIdentityCore<WalletUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = requireConfirmedEmail;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddEntityFrameworkStores<WalletDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(1));

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".WalletFlow.Identity";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.Redirect("/");
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri) ||
    (apiBaseUri.Scheme != Uri.UriSchemeHttp && apiBaseUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        "Configure Api:BaseUrl com a URL absoluta da API WalletFlow.");
}

var apiKey = builder.Configuration["Authentication:ApiKey"];
var jwtSigningKey = builder.Configuration["Authentication:Jwt:SigningKey"];
var jwtIssuer = builder.Configuration["Authentication:Jwt:Issuer"];
var jwtAudience = builder.Configuration["Authentication:Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || System.Text.Encoding.UTF8.GetByteCount(jwtSigningKey) < 32 ||
    string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException(
        "Configure Authentication:Jwt:SigningKey (pelo menos 32 bytes), Authentication:Jwt:Issuer e Authentication:Jwt:Audience no host Blazor.");

if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("Configure Authentication:ApiKey no servidor Blazor para acessar a API em produção.");
    if (apiBaseUri.Scheme != Uri.UriSchemeHttps)
        throw new InvalidOperationException("Api:BaseUrl deve usar HTTPS fora de Development.");
    if (!Uri.TryCreate(builder.Configuration["Identity:PublicBaseUrl"], UriKind.Absolute, out var publicBaseUri) ||
        publicBaseUri.Scheme != Uri.UriSchemeHttps)
        throw new InvalidOperationException("Configure Identity:PublicBaseUrl como uma URL HTTPS pública.");
    if (string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:Host"]) ||
        string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:Username"]) ||
        string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:Password"]) ||
        string.IsNullOrWhiteSpace(builder.Configuration["Email:Smtp:From"]))
        throw new InvalidOperationException("Configure Email:Smtp:Host, Username, Password e From em produção.");
}

builder.Services.AddHttpClient<TransacaoApiClient>(client =>
{
    var baseAddress = apiBaseUri.ToString();
    client.BaseAddress = new Uri(baseAddress.EndsWith('/') ? baseAddress : $"{baseAddress}/");

    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
});

var app = builder.Build();

await app.Services.ApplyWalletFlowMigrationsAsync();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapStaticAssets();
app.MapHub<BudgetAlertsHub>("/hubs/alertas-orcamento").RequireAuthorization();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
