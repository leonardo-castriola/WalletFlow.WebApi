using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WalletFlow.Application.Abstractions;
using WalletFlow.Infrastructure.Persistence;
using WalletFlow.Infrastructure.Repositories;
using WalletFlow.Infrastructure.Email;
using WalletFlow.Infrastructure.Reports;

namespace WalletFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWalletFlowForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            foreach (var rawProxy in ReadConfiguredValues(configuration, "ForwardedHeaders:KnownProxies"))
            {
                if (!IPAddress.TryParse(rawProxy, out var ipAddress))
                    throw new InvalidOperationException($"Endereço de proxy confiável inválido em ForwardedHeaders:KnownProxies: '{rawProxy}'.");
                options.KnownProxies.Add(ipAddress);
            }

            foreach (var rawNetwork in ReadConfiguredValues(configuration, "ForwardedHeaders:KnownNetworks"))
            {
                if (!System.Net.IPNetwork.TryParse(rawNetwork, out var network))
                    throw new InvalidOperationException($"Rede de proxy confiável inválida em ForwardedHeaders:KnownNetworks: '{rawNetwork}'.");
                options.KnownIPNetworks.Add(network);
            }
        });
        return services;
    }

    public static void ValidateProductionAllowedHosts(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return;

        var allowedHosts = configuration["AllowedHosts"];
        var entries = string.IsNullOrWhiteSpace(allowedHosts)
            ? []
            : allowedHosts.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0 || entries.Contains("*", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Em produção, configure AllowedHosts com os domínios explícitos da aplicação (não utilize '*').");
        }
    }

    private static IEnumerable<string> ReadConfiguredValues(IConfiguration configuration, string sectionPath)
    {
        var section = configuration.GetSection(sectionPath);
        if (!string.IsNullOrWhiteSpace(section.Value))
        {
            foreach (var item in section.Value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return item;
        }

        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
                yield return child.Value.Trim();
        }
    }

    public static async Task ApplyWalletFlowMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetService<IConfiguration>();
        if (configuration is not null && !configuration.GetValue("Database:AutoMigrate", true))
            return;

        var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingMigrations.Count == 0)
            return;

        var connectionString = dbContext.Database.GetConnectionString();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
            var dataSource = sqliteBuilder.DataSource;
            if (!string.IsNullOrWhiteSpace(dataSource) &&
                !string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(dataSource) &&
                new FileInfo(dataSource).Length > 0)
            {
                var backupDir = configuration?["Backup:Directory"]
                    ?? Path.GetDirectoryName(Path.GetFullPath(dataSource))
                    ?? AppContext.BaseDirectory;
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss'Z'");
                var encryptionKey = configuration?["Backup:EncryptionKey"];
                if (!string.IsNullOrWhiteSpace(encryptionKey))
                {
                    var encryptedBackupPath = Path.Combine(backupDir, $"Wallet.pre-migration-{timestamp}.db.enc");
                    await SqliteBackupService.CreateEncryptedBackupAsync(dataSource, encryptedBackupPath, encryptionKey);
                }
                else
                {
                    var backupPath = Path.Combine(backupDir, $"Wallet.pre-migration-{timestamp}.db");
                    await SqliteBackupService.CreateConsistentBackupAsync(dataSource, backupPath);
                }
            }
        }

        await dbContext.Database.MigrateAsync();
    }

    public static IServiceCollection AddWalletFlowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não foi configurada.");
        var sqliteConnection = new SqliteConnectionStringBuilder(connectionString);
        if (!Path.IsPathRooted(sqliteConnection.DataSource))
            sqliteConnection.DataSource = Path.GetFullPath(Path.Combine(environment.ContentRootPath, sqliteConnection.DataSource));

        services.AddDbContext<WalletDbContext>(options => options.UseSqlite(sqliteConnection.ConnectionString));
        services.AddHealthChecks()
            .AddCheck<WalletDatabaseHealthCheck>("database");
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IMonthlyFinancialReportDispatcher, MonthlyFinancialReportDispatcher>();
        services.AddScoped<ITransacaoRepository, EfTransacaoRepository>();
        services.AddScoped<IOrcamentoMensalService, EfOrcamentoMensalService>();
        services.AddScoped<IUsuarioConsulta, IdentityUsuarioConsulta>();
        services.AddScoped<IFinanceiroService, FinanceiroService>();
        return services;
    }
}
