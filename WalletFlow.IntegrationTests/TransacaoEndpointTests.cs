using System.Net;
using System.Net.Http.Json;
using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Enums;
using Xunit;

namespace WalletFlow.IntegrationTests;

public sealed class TransacaoEndpointTests : IAsyncLifetime
{
    private readonly WalletFlowApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.CriarUsuarioAsync("usuario-1");
        await _factory.CriarUsuarioAsync("usuario-2");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CriacaoEConsultaLeemDadosPersistidosPelaAplicacao()
    {
        UsarUsuario("usuario-1");

        using var respostaCriacao = await _client.PostAsJsonAsync("/api/Transacao", new TransacaoCriacaoDto
        {
            Descricao = "Salário",
            Valor = 3500m,
            Tipo = TipoTransacao.Receita
        });

        Assert.Equal(HttpStatusCode.Created, respostaCriacao.StatusCode);
        var criada = await respostaCriacao.Content.ReadFromJsonAsync<TransacaoRespostaDto>();
        Assert.NotNull(criada);
        Assert.True(criada.Id > 0);
        Assert.Equal("Salário", criada.Descricao);
        Assert.Equal(3500m, criada.Valor);
        Assert.Equal(nameof(TipoTransacao.Receita), criada.TipoFormatado);

        using var respostaConsulta = await _client.GetAsync($"/api/Transacao/{criada.Id}");
        Assert.Equal(HttpStatusCode.OK, respostaConsulta.StatusCode);
        var consultada = await respostaConsulta.Content.ReadFromJsonAsync<TransacaoRespostaDto>();
        Assert.NotNull(consultada);
        Assert.Equal(criada.Id, consultada.Id);
        Assert.Equal(criada.Descricao, consultada.Descricao);
        Assert.Equal(criada.Valor, consultada.Valor);
    }

    [Fact]
    public async Task OperacoesDeUmUsuarioNaoLeemNemAlteramLancamentosDeOutro()
    {
        UsarUsuario("usuario-1");
        using var respostaCriacao = await _client.PostAsJsonAsync("/api/Transacao", new TransacaoCriacaoDto
        {
            Descricao = "Despesa privada",
            Valor = 42m,
            Tipo = TipoTransacao.Despesa
        });
        Assert.Equal(HttpStatusCode.Created, respostaCriacao.StatusCode);
        var criada = await respostaCriacao.Content.ReadFromJsonAsync<TransacaoRespostaDto>();
        Assert.NotNull(criada);

        UsarUsuario("usuario-2");
        using var lista = await _client.GetAsync("/api/Transacao");
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var pagina = await lista.Content.ReadFromJsonAsync<ResultadoPaginadoDto<TransacaoRespostaDto>>();
        Assert.NotNull(pagina);
        Assert.Equal(0, pagina.TotalCount);
        Assert.Empty(pagina.Items);

        using var consulta = await _client.GetAsync($"/api/Transacao/{criada.Id}");
        using var exclusao = await _client.DeleteAsync($"/api/Transacao/{criada.Id}");
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, exclusao.StatusCode);

        UsarUsuario("usuario-1");
        using var aindaExiste = await _client.GetAsync($"/api/Transacao/{criada.Id}");
        Assert.Equal(HttpStatusCode.OK, aindaExiste.StatusCode);
    }

    [Fact]
    public async Task AtualizacaoExclusaoSaldoEFiltroRefletemAsAlteracoesNoBanco()
    {
        UsarUsuario("usuario-1");
        var receitaId = await CriarTransacaoAsync("Renda", 100m, TipoTransacao.Receita);
        var despesaId = await CriarTransacaoAsync("Conta", 30m, TipoTransacao.Despesa);

        using var saldoInicialResponse = await _client.GetAsync("/api/Transacao/saldo");
        Assert.Equal(HttpStatusCode.OK, saldoInicialResponse.StatusCode);
        var saldoInicial = await saldoInicialResponse.Content.ReadFromJsonAsync<SaldoRespostaDto>();
        Assert.NotNull(saldoInicial);
        Assert.Equal(70m, saldoInicial.SaldoConsolidado);

        using var respostaAtualizacao = await _client.PutAsJsonAsync($"/api/Transacao/{receitaId}", new TransacaoCriacaoDto
        {
            Descricao = "Renda ajustada",
            Valor = 120m,
            Tipo = TipoTransacao.Receita
        });
        Assert.Equal(HttpStatusCode.NoContent, respostaAtualizacao.StatusCode);

        using var filtro = await _client.GetAsync("/api/Transacao/filtro?tipo=1&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, filtro.StatusCode);
        var receitas = await filtro.Content.ReadFromJsonAsync<ResultadoPaginadoDto<TransacaoRespostaDto>>();
        Assert.NotNull(receitas);
        Assert.Equal(1, receitas.TotalCount);
        Assert.Equal("Renda ajustada", Assert.Single(receitas.Items).Descricao);

        using var saldoAtualizadoResponse = await _client.GetAsync("/api/Transacao/saldo");
        var saldoAtualizado = await saldoAtualizadoResponse.Content.ReadFromJsonAsync<SaldoRespostaDto>();
        Assert.NotNull(saldoAtualizado);
        Assert.Equal(90m, saldoAtualizado.SaldoConsolidado);

        using var respostaExclusao = await _client.DeleteAsync($"/api/Transacao/{despesaId}");
        Assert.Equal(HttpStatusCode.NoContent, respostaExclusao.StatusCode);
        using var saldoFinalResponse = await _client.GetAsync("/api/Transacao/saldo");
        var saldoFinal = await saldoFinalResponse.Content.ReadFromJsonAsync<SaldoRespostaDto>();
        Assert.NotNull(saldoFinal);
        Assert.Equal(120m, saldoFinal.SaldoConsolidado);
    }

    [Fact]
    public async Task RequisicaoSemIdentidadeERejeitada()
    {
        _client.DefaultRequestHeaders.Remove("X-User-ID");
        using var resposta = await _client.GetAsync("/api/Transacao");
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    private void UsarUsuario(string userId)
    {
        _client.DefaultRequestHeaders.Remove("X-User-ID");
        _client.DefaultRequestHeaders.Add("X-User-ID", userId);
    }

    private async Task<int> CriarTransacaoAsync(string descricao, decimal valor, TipoTransacao tipo)
    {
        using var resposta = await _client.PostAsJsonAsync("/api/Transacao", new TransacaoCriacaoDto
        {
            Descricao = descricao,
            Valor = valor,
            Tipo = tipo
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var transacao = await resposta.Content.ReadFromJsonAsync<TransacaoRespostaDto>();
        Assert.NotNull(transacao);
        return transacao.Id;
    }

    private sealed class SaldoRespostaDto
    {
        public decimal SaldoConsolidado { get; init; }
    }
}
