using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WalletFlow.WebApi.Controllers;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.Entities;
using WalletFlow.WebApi.Enums;
using WalletFlow.WebApi.Middleware;
using Xunit;

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
        context.Transacoes.AddRange(
            new Transacao { Descricao = "Receita 1", Valor = 10, Tipo = TipoTransacao.Receita, DataCadastro = data },
            new Transacao { Descricao = "Despesa", Valor = 2, Tipo = TipoTransacao.Despesa, DataCadastro = data.AddDays(1) },
            new Transacao { Descricao = "Receita 2", Valor = 20, Tipo = TipoTransacao.Receita, DataCadastro = data.AddDays(2) },
            new Transacao { Descricao = "Receita fora do período", Valor = 30, Tipo = TipoTransacao.Receita, DataCadastro = data.AddDays(5) });
        await context.SaveChangesAsync();

        var controller = new TransacaoController(context, new WalletFlow.WebApi.Services.FinanceiroService(context));
        var action = await controller.ObterFiltrado(TipoTransacao.Receita, data, data.AddDays(3), page: 2, pageSize: 1);
        var result = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<WalletFlow.WebApi.DTOs.ResultadoPaginadoDto<WalletFlow.WebApi.DTOs.TransacaoRespostaDto>>(result.Value);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
        Assert.Equal("Receita 1", page.Items[0].Descricao);

        var primeiraPaginaAction = await controller.ObterFiltrado(
            TipoTransacao.Receita, data, data.AddDays(3), page: 1, pageSize: 1);
        var primeiraPaginaResult = Assert.IsType<OkObjectResult>(primeiraPaginaAction.Result);
        var primeiraPagina = Assert.IsType<WalletFlow.WebApi.DTOs.ResultadoPaginadoDto<WalletFlow.WebApi.DTOs.TransacaoRespostaDto>>(primeiraPaginaResult.Value);
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
}
