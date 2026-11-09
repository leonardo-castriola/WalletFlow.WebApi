using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Data.Sqlite;
using WalletFlow.Application.Abstractions;
using WalletFlow.Infrastructure.Persistence;

namespace WalletFlow.IntegrationTests;

public sealed class WalletFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(), "WalletFlow.IntegrationTests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryEmailSender _emailSender = new();

    public IReadOnlyCollection<SentEmailMessage> SentEmails => _emailSender.Messages;

    public WalletFlowApiFactory()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(FindApiContentRoot());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    $"Data Source={Path.Combine(_temporaryDirectory, "WalletFlow.IntegrationTests.db")}",
                ["Authentication:ApiKey"] = "integration-test-api-key",
                ["Authentication:Jwt:SigningKey"] = "integration-test-jwt-signing-key-32-bytes-minimum",
                ["Authentication:Jwt:Issuer"] = "WalletFlow",
                ["Authentication:Jwt:Audience"] = "WalletFlow.Api"
            });
        });

        // Pin the context to this factory's private file database. The app config
        // override alone can be superseded by host configuration in some runners.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<WalletDbContext>();
            services.RemoveAll<DbContextOptions<WalletDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WalletDbContext>>();
            services.RemoveAll<IEmailSender>();

            var databasePath = Path.Combine(_temporaryDirectory, "WalletFlow.IntegrationTests.db");
            services.AddDbContext<WalletDbContext>(options => options.UseSqlite(
                new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()));
            services.AddSingleton<IEmailSender>(_emailSender);
        });
    }

    public async Task CriarUsuarioAsync(string userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        context.Users.Add(new WalletUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = $"{userId}@example.test",
            NormalizedEmail = $"{userId}@example.test".ToUpperInvariant()
        });
        await context.SaveChangesAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_temporaryDirectory))
            Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private static string FindApiContentRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var projectFile = Path.Combine(directory.FullName, "WalletFlow", "WalletFlow.csproj");
            if (File.Exists(projectFile))
                return Path.Combine(directory.FullName, "WalletFlow");
        }

        throw new DirectoryNotFoundException("Não foi possível localizar o projeto WalletFlow da API.");
    }

    public sealed record SentEmailMessage(string Recipient, string Subject, string HtmlBody);

    private sealed class InMemoryEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<SentEmailMessage> _messages = new();

        public IReadOnlyCollection<SentEmailMessage> Messages => _messages.ToArray();

        public Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            _messages.Enqueue(new SentEmailMessage(recipient, subject, htmlBody));
            return Task.CompletedTask;
        }

        public Task SendAsync(
            string recipient,
            string subject,
            string htmlBody,
            IReadOnlyCollection<EmailAttachment> attachments,
            CancellationToken cancellationToken = default)
        {
            _messages.Enqueue(new SentEmailMessage(recipient, subject, htmlBody));
            return Task.CompletedTask;
        }
    }
}
