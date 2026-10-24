using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.DTOs;
using WalletFlow.WebApi.Entities;
using WalletFlow.WebApi.Enums;
using WalletFlow.WebApi.Services;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace WalletFlow.Tests;

public sealed class FinanceiroTests
{
    [Fact]
    public async Task SaldoSemTransacoesDeveSerZero()
    {
        await using var database = await TestDatabase.CriarAsync();
        var service = new FinanceiroService(database.Context);
        Assert.Equal(0m, await service.CalcularSaldoConsolidadoAsync());
    }

    [Fact]
    public async Task SaldoPodeFicarNegativoQuandoDespesasSuperamReceitas()
    {
        await using var database = await TestDatabase.CriarAsync();
        database.Context.Transacoes.AddRange(
            new Transacao { Descricao = "Receita", Valor = 100m, Tipo = TipoTransacao.Receita },
            new Transacao { Descricao = "Despesa", Valor = 150m, Tipo = TipoTransacao.Despesa });
        await database.Context.SaveChangesAsync();
        Assert.Equal(-50m, await new FinanceiroService(database.Context).CalcularSaldoConsolidadoAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidacaoRejeitaValorZeroOuNegativo(decimal valor)
    {
        var dto = new TransacaoCriacaoDto { Descricao = "Inválida", Valor = valor, Tipo = TipoTransacao.Despesa };
        Assert.Contains(Validar(dto), item => item.MemberNames.Contains(nameof(dto.Valor)));
    }

    [Fact]
    public void ValidacaoAceitaValorPositivo()
    {
        var dto = new TransacaoCriacaoDto { Descricao = "Válida", Valor = 1m, Tipo = TipoTransacao.Receita };
        Assert.Empty(Validar(dto));
    }

    private static List<ValidationResult> Validar(object model)
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        var originalUiCulture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        var results = new List<ValidationResult>();
        try
        {
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results;
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
            System.Globalization.CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, WalletDbContext context) => (Connection, Context) = (connection, context);
        private SqliteConnection Connection { get; }
        public WalletDbContext Context { get; }

        public static async Task<TestDatabase> CriarAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(connection).Options;
            var context = new WalletDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
