using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WalletFlow.Api.Controllers;
using WalletFlow.Infrastructure.Persistence;
using WalletFlow.Domain.Entities;
using WalletFlow.Domain.Enums;
using WalletFlow.Api.Middleware;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.Services;
using WalletFlow.Infrastructure.Repositories;
using Xunit;
using System.Security.Claims;

namespace WalletFlow.Tests;

public sealed class ApiBehaviorTests
{
    [Fact]
    public async Task FiltrosSaoAplicadosAntesDaPaginacaoEOrdenadosDoMaisNovo()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(connection).Options;
        await using var context = new WalletDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var data = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        context.Users.Add(new WalletUser { Id = "user-1", UserName = "user-1" });
        await context.SaveChangesAsync();
        context.Transacoes.AddRange(
            new Transacao { UserId = "user-1", Descricao = "Receita 1", Valor = 10, Tipo = TipoTransacao.Receita, DataCadastro = data },
            new Transacao { UserId = "user-1", Descricao = "Despesa", Valor = 2, Tipo = TipoTransacao.Despesa, DataCadastro = data.AddDays(1) },
            new Transacao { UserId = "user-1", Descricao = "Receita 2", Valor = 20, Tipo = TipoTransacao.Receita, DataCadastro = data.AddDays(2) },
            new Transacao { UserId = "user-1", Descricao = "Receita fora do período", Valor = 30, Tipo = TipoTransacao.Receita, DataCadastro = data.AddDays(5) });
        await context.SaveChangesAsync();

        var repository = new EfTransacaoRepository(context);
        var service = new TransacaoService(repository, new FinanceiroService(context), new ExtratoImportador());
        var controller = new TransacaoController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"))
            }
        };
        var action = await controller.ObterFiltrado(TipoTransacao.Receita, data, data.AddDays(3), page: 2, pageSize: 1);
        var result = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<WalletFlow.Application.DTOs.ResultadoPaginadoDto<WalletFlow.Application.DTOs.TransacaoRespostaDto>>(result.Value);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
        Assert.Equal("Receita 1", page.Items[0].Descricao);

        var primeiraPaginaAction = await controller.ObterFiltrado(
            TipoTransacao.Receita, data, data.AddDays(3), page: 1, pageSize: 1);
        var primeiraPaginaResult = Assert.IsType<OkObjectResult>(primeiraPaginaAction.Result);
        var primeiraPagina = Assert.IsType<WalletFlow.Application.DTOs.ResultadoPaginadoDto<WalletFlow.Application.DTOs.TransacaoRespostaDto>>(primeiraPaginaResult.Value);
        Assert.Equal("Receita 2", Assert.Single(primeiraPagina.Items).Descricao);
    }

    [Fact]
    public async Task MiddlewareRetornaErroSeguroSemExporExcecao()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("segredo interno"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("Erro interno do servidor", body);
        Assert.DoesNotContain("segredo interno", body);
    }

    [Fact]
    public async Task HealthCheckRetornaHealthyQuandoBancoAcessivel()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(connection).Options;
        await using var context = new WalletDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var healthCheck = new WalletDatabaseHealthCheck(context);
        var result = await healthCheck.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task BackupERestauracaoSqlitePreservamDadosComModoWalAtivo()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"WalletFlow.BackupTest.{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var sourceDb = Path.Combine(tempDir, "source.db");
            var backupDb = Path.Combine(tempDir, "backup.db.enc");
            var restoredDb = Path.Combine(tempDir, "restored.db");
            const string passphrase = "chave-de-criptografia-de-backup-teste-segura";

            var sourceOptions = new DbContextOptionsBuilder<WalletDbContext>()
                .UseSqlite($"Data Source={sourceDb}")
                .Options;
            await using (var context = new WalletDbContext(sourceOptions))
            {
                await context.Database.EnsureCreatedAsync();
                await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                context.Users.Add(new WalletUser { Id = "u-backup", UserName = "u-backup" });
                context.Transacoes.Add(new Transacao
                {
                    UserId = "u-backup",
                    Descricao = "Receita protegida por backup",
                    Valor = 1234.56m,
                    Tipo = TipoTransacao.Receita,
                    DataCadastro = DateTimeOffset.UtcNow
                });
                await context.SaveChangesAsync();

                var checksum = await SqliteBackupService.CreateEncryptedBackupAsync(sourceDb, backupDb, passphrase);
                Assert.False(string.IsNullOrWhiteSpace(checksum));
                Assert.True(File.Exists($"{backupDb}.sha256"));

                await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(() =>
                    SqliteBackupService.RestoreEncryptedBackupAsync(backupDb, restoredDb, "chave-incorreta-123456", checksum));

                await SqliteBackupService.RestoreEncryptedBackupAsync(backupDb, restoredDb, passphrase, checksum);
            }

            var restoredOptions = new DbContextOptionsBuilder<WalletDbContext>()
                .UseSqlite($"Data Source={restoredDb}")
                .Options;
            await using (var restoredContext = new WalletDbContext(restoredOptions))
            {
                var transacao = await restoredContext.Transacoes.SingleAsync();
                Assert.Equal("Receita protegida por backup", transacao.Descricao);
                Assert.Equal(1234.56m, transacao.Valor);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ForwardedHeadersEAllowedHostsRejeitamConfiguracoesInsegurasEmProducao()
    {
        var wildcardConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AllowedHosts"] = "*" })
            .Build();
        var prodEnvironment = new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
        {
            EnvironmentName = Microsoft.Extensions.Hosting.Environments.Production
        };
        Assert.Throws<InvalidOperationException>(() =>
            WalletFlow.Infrastructure.DependencyInjection.ValidateProductionAllowedHosts(wildcardConfig, prodEnvironment));

        var explicitHostConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedHosts"] = "api.walletflow.exemplo.com;walletflow.exemplo.com",
                ["ForwardedHeaders:KnownProxies:0"] = "172.28.0.10",
                ["ForwardedHeaders:KnownNetworks:0"] = "172.28.0.0/24"
            })
            .Build();
        WalletFlow.Infrastructure.DependencyInjection.ValidateProductionAllowedHosts(explicitHostConfig, prodEnvironment);

        var services = new ServiceCollection();
        WalletFlow.Infrastructure.DependencyInjection.AddWalletFlowForwardedHeaders(services, explicitHostConfig);
        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>>()
            .Value;

        Assert.Contains(System.Net.IPAddress.Parse("172.28.0.10"), options.KnownProxies);
        Assert.Contains(System.Net.IPNetwork.Parse("172.28.0.0/24"), options.KnownIPNetworks);
    }
}
