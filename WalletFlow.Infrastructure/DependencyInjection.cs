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
    public static async Task ApplyWalletFlowMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
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
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IMonthlyFinancialReportDispatcher, MonthlyFinancialReportDispatcher>();
        services.AddScoped<ITransacaoRepository, EfTransacaoRepository>();
        services.AddScoped<IOrcamentoMensalService, EfOrcamentoMensalService>();
        services.AddScoped<IUsuarioConsulta, IdentityUsuarioConsulta>();
        services.AddScoped<IFinanceiroService, FinanceiroService>();
        return services;
    }
}
