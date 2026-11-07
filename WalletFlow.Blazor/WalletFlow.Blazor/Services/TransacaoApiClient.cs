using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;
using WalletFlow.Blazor.Components.Layout;
using WalletFlow.Blazor.DTOs;
using WalletFlow.Blazor.Enums;
using AlertaOrcamentoDto = WalletFlow.Application.DTOs.AlertaOrcamentoDto;
using AtualizarOrcamentoMensalDto = WalletFlow.Application.DTOs.AtualizarOrcamentoMensalDto;
using OrcamentoMensalDto = WalletFlow.Application.DTOs.OrcamentoMensalDto;

namespace WalletFlow.Blazor.Services;

public sealed class TransacaoApiClient(
    HttpClient httpClient,
    IConfiguration configuration,
    AuthenticationStateProvider authenticationStateProvider,
    IHubContext<BudgetAlertsHub> alertHub,
    ILogger<TransacaoApiClient> logger)
{
    private static readonly HttpRequestOptionsKey<string> UserIdOption = new("WalletFlow.UserId");
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["Filters:TimeZoneId"] ?? "America/Sao_Paulo");

    public DateTimeOffset ConverterParaFusoConfigurado(DateTimeOffset data) =>
        TimeZoneInfo.ConvertTime(data, _timeZone);

    public async Task<ResultadoPaginadoDto<TransacaoRespostaDto>> ObterTransacoesAsync(
        int page,
        int pageSize,
        TipoTransacao? tipo,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken = default)
    {
        var parametros = new List<string>
        {
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}"
        };

        if (tipo.HasValue)
            parametros.Add($"tipo={(int)tipo.Value}");
        if (dataInicio.HasValue)
            parametros.Add($"dataInicio={Uri.EscapeDataString(InicioDoDiaUtc(dataInicio.Value, _timeZone))}");
        if (dataFim.HasValue)
            parametros.Add($"dataFim={Uri.EscapeDataString(FimDoDiaUtc(dataFim.Value, _timeZone))}");

        var caminho = tipo.HasValue || dataInicio.HasValue || dataFim.HasValue
            ? "api/Transacao/filtro"
            : "api/Transacao";

        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Get, $"{caminho}?{string.Join('&', parametros)}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ResultadoPaginadoDto<TransacaoRespostaDto>>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("A API retornou uma resposta vazia para o extrato.");
    }

    public async Task<IReadOnlyList<TransacaoRespostaDto>> ObterTodasTransacoesAsync(
        TipoTransacao? tipo,
        DateOnly? dataInicio,
        DateOnly? dataFim,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 100;
        var primeiraPagina = await ObterTransacoesAsync(1, pageSize, tipo, dataInicio, dataFim, cancellationToken);
        var transacoes = new List<TransacaoRespostaDto>(primeiraPagina.TotalCount);
        transacoes.AddRange(primeiraPagina.Items);

        for (var page = 2; page <= primeiraPagina.TotalPages; page++)
        {
            var resultado = await ObterTransacoesAsync(page, pageSize, tipo, dataInicio, dataFim, cancellationToken);
            transacoes.AddRange(resultado.Items);
        }

        return transacoes;
    }

    public async Task<TransacaoRespostaDto> CriarTransacaoAsync(
        TransacaoCriacaoDto transacao,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, "api/Transacao", cancellationToken);
        request.Content = JsonContent.Create(transacao);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        var criada = await response.Content.ReadFromJsonAsync<TransacaoRespostaDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("A API não retornou a transação criada.");
        await NotificarTransacoesAtualizadasAsync(UserIdDaRequisicao(request), cancellationToken);
        await VerificarEPublicarAlertaAsync(UserIdDaRequisicao(request), cancellationToken);
        return criada;
    }

    public async Task<TransacaoRespostaDto> ObterTransacaoAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Get, $"api/Transacao/{id.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TransacaoRespostaDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("A API não retornou a transação solicitada.");
    }

    public async Task AtualizarTransacaoAsync(
        int id,
        TransacaoCriacaoDto transacao,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Put, $"api/Transacao/{id.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
        request.Content = JsonContent.Create(transacao);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);
        await NotificarTransacoesAtualizadasAsync(UserIdDaRequisicao(request), cancellationToken);
        await VerificarEPublicarAlertaAsync(UserIdDaRequisicao(request), cancellationToken);
    }

    public async Task ExcluirTransacaoAsync(int id, CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Delete, $"api/Transacao/{id.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);
        await NotificarTransacoesAtualizadasAsync(UserIdDaRequisicao(request), cancellationToken);
        await VerificarEPublicarAlertaAsync(UserIdDaRequisicao(request), cancellationToken);
    }

    public async Task<ExtratoImportacaoPreviaDto> AnalisarExtratoAsync(
        Stream arquivo,
        string nomeArquivo,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, "api/Transacao/importacao/previa", cancellationToken);
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(arquivo);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        multipart.Add(fileContent, "arquivo", Path.GetFileName(nomeArquivo));
        request.Content = multipart;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoImportacaoAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ExtratoImportacaoPreviaDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("A API não retornou a prévia do extrato.");
    }

    public async Task<ExtratoImportacaoResultadoDto> ImportarExtratoAsync(
        ExtratoImportacaoConfirmacaoDto confirmacao,
        CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Post, "api/Transacao/importacao", cancellationToken);
        request.Content = JsonContent.Create(confirmacao);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoImportacaoAsync(response, cancellationToken);
        var resultado = await response.Content.ReadFromJsonAsync<ExtratoImportacaoResultadoDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("A API não retornou o resultado da importação.");
        if (resultado.Importados > 0)
            await NotificarTransacoesAtualizadasAsync(UserIdDaRequisicao(request), cancellationToken);
        await VerificarEPublicarAlertaAsync(UserIdDaRequisicao(request), cancellationToken);
        return resultado;
    }

    public async Task<OrcamentoMensalDto> ObterOrcamentoMensalAsync(int ano, int mes, CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, $"api/Orcamento?ano={ano}&mes={mes}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<OrcamentoMensalDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("A API não retornou os dados do orçamento.");
    }

    public async Task<OrcamentoMensalDto> SalvarOrcamentoMensalAsync(int ano, int mes, string modo, decimal limite, decimal? percentual = null, CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Put, $"api/Orcamento?ano={ano}&mes={mes}", cancellationToken);
        request.Content = JsonContent.Create(new AtualizarOrcamentoMensalDto(limite, modo, percentual));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);
        var saved = await response.Content.ReadFromJsonAsync<OrcamentoMensalDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("A API não retornou o orçamento salvo.");
        await VerificarEPublicarAlertaAsync(UserIdDaRequisicao(request), cancellationToken);
        return saved;
    }

    private async Task NotificarTransacoesAtualizadasAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            await alertHub.Clients.Group(BudgetAlertsHub.GroupName(userId))
                .SendAsync("TransacoesAtualizadas", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao publicar a atualização de movimentações; a operação financeira foi concluída.");
        }
    }

    private async Task VerificarEPublicarAlertaAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var localDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone);
            using var request = await CreateAuthenticatedRequestAsync(
                HttpMethod.Post,
                $"api/Orcamento/verificar-alerta?ano={localDate.Year}&mes={localDate.Month}",
                cancellationToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NoContent)
                return;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("A API não conseguiu verificar o limite do orçamento mensal ({StatusCode}).", response.StatusCode);
                return;
            }

            var alert = await response.Content.ReadFromJsonAsync<AlertaOrcamentoDto>(cancellationToken: cancellationToken);
            if (alert is not null)
                await alertHub.Clients.Group(BudgetAlertsHub.GroupName(userId)).SendAsync("AlertaOrcamento", alert, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao publicar o alerta de orçamento; a operação financeira foi concluída.");
        }
    }

    private static string UserIdDaRequisicao(HttpRequestMessage request) =>
        request.Options.TryGetValue(UserIdOption, out var userId)
            ? userId
            : throw new InvalidOperationException("A requisição não contém o usuário autenticado.");

    public async Task<decimal> ObterSaldoAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, "api/Transacao/saldo", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        var resultado = await response.Content.ReadFromJsonAsync<SaldoRespostaDto>(
            cancellationToken: cancellationToken);
        return resultado?.SaldoConsolidado
               ?? throw new InvalidOperationException("A API não retornou o saldo consolidado.");
    }

    public async Task<EvolucaoFinanceiraDto> ObterEvolucaoMensalAsync(CancellationToken cancellationToken = default)
    {
        var mesAtual = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone).DateTime);
        using var request = await CreateAuthenticatedRequestAsync(
            HttpMethod.Get,
            $"api/Transacao/evolucao-mensal?ano={mesAtual.Year.ToString(CultureInfo.InvariantCulture)}&mes={mesAtual.Month.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<EvolucaoFinanceiraDto>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("A API não retornou a evolução financeira.");
    }

    private static string InicioDoDiaUtc(DateOnly data, TimeZoneInfo timeZone) =>
        new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone), TimeSpan.Zero)
        .ToString("O", CultureInfo.InvariantCulture);

    private static string FimDoDiaUtc(DateOnly data, TimeZoneInfo timeZone) =>
        new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
            data.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified), timeZone), TimeSpan.Zero)
        .ToString("O", CultureInfo.InvariantCulture);

    private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(
        HttpMethod method,
        string uri,
        CancellationToken cancellationToken)
    {
        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userId = authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("É necessário entrar para acessar suas transações.");

        var signingKey = configuration["Authentication:Jwt:SigningKey"];
        var issuer = configuration["Authentication:Jwt:Issuer"];
        var audience = configuration["Authentication:Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32 ||
            string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("A configuração de autenticação JWT do host Blazor está incompleta.");

        var now = DateTime.UtcNow;
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim(JwtRegisteredClaimNames.Sub, userId)],
            now,
            now.AddMinutes(3),
            signingCredentials);
        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
        request.Options.Set(UserIdOption, userId);
        return request;
    }

    private static async Task GarantirSucessoAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var mensagem = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "A API rejeitou os dados enviados. Confira os campos e os filtros.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "A API não autorizou a operação. Confira a configuração de acesso no servidor.",
            HttpStatusCode.NotFound => "A transação não foi encontrada ou não pertence à sua conta.",
            _ => "Não foi possível concluir a operação na API. Tente novamente mais tarde."
        };

        throw new HttpRequestException(mensagem, null, response.StatusCode);
    }

    private static async Task GarantirSucessoImportacaoAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var mensagem = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(mensagem))
            mensagem = response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden
                ? "A API não autorizou a operação. Entre novamente e tente outra vez."
                : "Não foi possível processar o extrato. Confira o arquivo e tente novamente.";
        throw new HttpRequestException(mensagem.Trim('"'), null, response.StatusCode);
    }

    private sealed class SaldoRespostaDto
    {
        public decimal SaldoConsolidado { get; init; }
    }
}
