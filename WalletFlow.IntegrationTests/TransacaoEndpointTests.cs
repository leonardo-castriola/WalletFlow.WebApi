using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Globalization;
using Microsoft.IdentityModel.Tokens;
using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Enums;
using Xunit;

namespace WalletFlow.IntegrationTests;

public sealed class TransacaoEndpointTests : IAsyncLifetime
{
    private const string ApiKey = "integration-test-api-key";
    private const string JwtSigningKey = "integration-test-jwt-signing-key-32-bytes-minimum";
    private readonly WalletFlowApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
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
    public async Task CabecalhoDeUsuarioNaoSubstituiJwtERequisicaoSemCredenciaisERejeitada()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        _client.DefaultRequestHeaders.Remove("X-User-ID");
        _client.DefaultRequestHeaders.Add("X-User-ID", "usuario-1");
        using var resposta = await _client.GetAsync("/api/Transacao");
        using var respostaOrcamento = await _client.GetAsync("/api/Orcamento?ano=2026&mes=5");
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, respostaOrcamento.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-API-Key");
        using var respostaSemCredenciais = await _client.GetAsync("/api/Transacao");
        Assert.Equal(HttpStatusCode.Unauthorized, respostaSemCredenciais.StatusCode);
    }

    [Fact]
    public async Task PreviaEConfirmacaoDeImportacaoMarcamDuplicatasDoHistoricoEDoArquivo()
    {
        UsarUsuario("usuario-1");
        await CriarTransacaoAsync("Café", 42.75m, TipoTransacao.Despesa);
        var dia = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var csv = $"Data;Descrição;Valor;Tipo\n{dia};Cafe;42,75;Despesa\n{dia};Cafe;42,75;Despesa\n{dia};Passagem;8,50;Despesa";

        using var formulario = new MultipartFormDataContent();
        using var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        formulario.Add(arquivo, "arquivo", "extrato.csv");
        using var respostaPrevia = await _client.PostAsync("/api/Transacao/importacao/previa", formulario);

        Assert.Equal(HttpStatusCode.OK, respostaPrevia.StatusCode);
        var previa = await respostaPrevia.Content.ReadFromJsonAsync<ExtratoImportacaoPreviaDto>();
        Assert.NotNull(previa);
        Assert.Equal(2, previa.TotalDuplicados);
        Assert.True(previa.Lancamentos[0].Duplicada);
        Assert.True(previa.Lancamentos[1].DuplicadaNoArquivo);
        Assert.False(previa.Lancamentos[2].Duplicada);

        var data = DateTimeOffset.ParseExact(dia, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        using var respostaImportacao = await _client.PostAsJsonAsync("/api/Transacao/importacao", new ExtratoImportacaoConfirmacaoDto
        {
            Lancamentos =
            [
                new() { Descricao = "Cafe", Valor = 42.75m, Tipo = TipoTransacao.Despesa, Data = data },
                new() { Descricao = "Cafe", Valor = 42.75m, Tipo = TipoTransacao.Despesa, Data = data },
                new() { Descricao = "Passagem", Valor = 8.50m, Tipo = TipoTransacao.Despesa, Data = data }
            ]
        });

        Assert.Equal(HttpStatusCode.OK, respostaImportacao.StatusCode);
        var resultado = await respostaImportacao.Content.ReadFromJsonAsync<ExtratoImportacaoResultadoDto>();
        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.Importados);
        Assert.Equal(2, resultado.IgnoradosComoDuplicados);
        using var respostaExtrato = await _client.GetAsync("/api/Transacao");
        var extrato = await respostaExtrato.Content.ReadFromJsonAsync<ResultadoPaginadoDto<TransacaoRespostaDto>>();
        Assert.NotNull(extrato);
        Assert.Equal(2, extrato.TotalCount);
    }

    [Fact]
    public async Task PreviaNaoMarcaComoDuplicataLancamentoQuePertenceAOutroUsuario()
    {
        UsarUsuario("usuario-2");
        await CriarTransacaoAsync("Conta de internet", 89.90m, TipoTransacao.Despesa);

        UsarUsuario("usuario-1");
        var dia = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var csv = $"Data;Descrição;Valor;Tipo\n{dia};Conta de internet;89,90;Despesa";
        using var formulario = new MultipartFormDataContent();
        using var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        formulario.Add(arquivo, "arquivo", "extrato.csv");

        using var resposta = await _client.PostAsync("/api/Transacao/importacao/previa", formulario);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var previa = await resposta.Content.ReadFromJsonAsync<ExtratoImportacaoPreviaDto>();
        Assert.NotNull(previa);
        Assert.Equal(0, previa.TotalDuplicados);
        Assert.False(Assert.Single(previa.Lancamentos).Duplicada);
    }

    [Fact]
    public async Task OrcamentoIsolaUsuariosValidaEntradasEEmiteAlertaAoAtingirLimiteUmaUnicaVez()
    {
        UsarUsuario("usuario-1");
        var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var periodo = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, fuso);
        var ano = periodo.Year;
        var mes = periodo.Month;

        using var periodoInvalido = await _client.GetAsync($"/api/Orcamento?ano={ano}&mes=13");
        Assert.Equal(HttpStatusCode.BadRequest, periodoInvalido.StatusCode);
        using var limiteInvalido = await _client.PutAsJsonAsync($"/api/Orcamento?ano={ano}&mes={mes}", new AtualizarOrcamentoMensalDto(0));
        Assert.Equal(HttpStatusCode.BadRequest, limiteInvalido.StatusCode);

        using var respostaSalvar = await _client.PutAsJsonAsync($"/api/Orcamento?ano={ano}&mes={mes}", new AtualizarOrcamentoMensalDto(100m));
        Assert.Equal(HttpStatusCode.OK, respostaSalvar.StatusCode);
        await CriarTransacaoAsync("Despesa inicial", 79m, TipoTransacao.Despesa);
        using var abaixoDoLimite = await _client.PostAsync($"/api/Orcamento/verificar-alerta?ano={ano}&mes={mes}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, abaixoDoLimite.StatusCode);

        await CriarTransacaoAsync("Despesa no limite", 21m, TipoTransacao.Despesa);
        using var alerta = await _client.PostAsync($"/api/Orcamento/verificar-alerta?ano={ano}&mes={mes}", content: null);
        Assert.Equal(HttpStatusCode.OK, alerta.StatusCode);
        var dadosAlerta = await alerta.Content.ReadFromJsonAsync<AlertaOrcamentoDto>();
        Assert.NotNull(dadosAlerta);
        Assert.Equal(100m, dadosAlerta.PercentualUtilizado);
        using var alertaRepetido = await _client.PostAsync($"/api/Orcamento/verificar-alerta?ano={ano}&mes={mes}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, alertaRepetido.StatusCode);

        using var consultaDoDono = await _client.GetAsync($"/api/Orcamento?ano={ano}&mes={mes}");
        var orcamentoDoDono = await consultaDoDono.Content.ReadFromJsonAsync<OrcamentoMensalDto>();
        Assert.NotNull(orcamentoDoDono);
        Assert.Equal(100m, orcamentoDoDono.Despesas);
        Assert.True(orcamentoDoDono.AlertaEnviado);

        UsarUsuario("usuario-2");
        using var consultaDeOutroUsuario = await _client.GetAsync($"/api/Orcamento?ano={ano}&mes={mes}");
        var orcamentoDeOutroUsuario = await consultaDeOutroUsuario.Content.ReadFromJsonAsync<OrcamentoMensalDto>();
        Assert.NotNull(orcamentoDeOutroUsuario);
        Assert.Equal(0m, orcamentoDeOutroUsuario.Limite);
        Assert.Equal(0m, orcamentoDeOutroUsuario.Despesas);
        Assert.False(orcamentoDeOutroUsuario.AlertaEnviado);
        using var alertaDeOutroUsuario = await _client.PostAsync($"/api/Orcamento/verificar-alerta?ano={ano}&mes={mes}", content: null);
        Assert.Equal(HttpStatusCode.NoContent, alertaDeOutroUsuario.StatusCode);
    }

    private void UsarUsuario(string userId)
    {
        _client.DefaultRequestHeaders.Remove("X-User-ID");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            "WalletFlow",
            "WalletFlow.Api",
            [new Claim(JwtRegisteredClaimNames.Sub, userId)],
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(3),
            credentials);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
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
