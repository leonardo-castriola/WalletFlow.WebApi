using WalletFlow.Blazor.Client.Pages;
using WalletFlow.Blazor.Components;
using WalletFlow.Blazor.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.Entities;
using WalletFlow.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

var databaseConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não foi configurada.");
var sqliteConnection = new SqliteConnectionStringBuilder(databaseConnection);
if (!Path.IsPathRooted(sqliteConnection.DataSource))
    sqliteConnection.DataSource = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, sqliteConnection.DataSource));

builder.Services.AddDbContext<WalletDbContext>(options => options.UseSqlite(sqliteConnection.ConnectionString));
builder.Services.AddIdentityCore<WalletUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
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
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri) ||
    (apiBaseUri.Scheme != Uri.UriSchemeHttp && apiBaseUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        "Configure Api:BaseUrl com a URL absoluta da API WalletFlow.");
}

var apiKey = builder.Configuration["Authentication:ApiKey"];
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

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
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
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(WalletFlow.Blazor.Client._Imports).Assembly);

app.Run();
