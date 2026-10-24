using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using WalletFlow.Blazor.DTOs;
using WalletFlow.Blazor.Enums;

namespace WalletFlow.Blazor.Services;

public sealed class TransacaoApiClient(HttpClient httpClient)
{
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
            parametros.Add($"dataInicio={Uri.EscapeDataString(InicioDoDiaUtc(dataInicio.Value))}");
        if (dataFim.HasValue)
            parametros.Add($"dataFim={Uri.EscapeDataString(FimDoDiaUtc(dataFim.Value))}");

        var caminho = tipo.HasValue || dataInicio.HasValue || dataFim.HasValue
            ? "api/Transacao/filtro"
            : "api/Transacao";

        using var response = await httpClient.GetAsync(
            $"{caminho}?{string.Join('&', parametros)}", cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ResultadoPaginadoDto<TransacaoRespostaDto>>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("A API retornou uma resposta vazia para o extrato.");
    }

    public async Task<TransacaoRespostaDto> CriarTransacaoAsync(
        TransacaoCriacaoDto transacao,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/Transacao", transacao, cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TransacaoRespostaDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("A API não retornou a transação criada.");
    }

    public async Task<decimal> ObterSaldoAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/Transacao/saldo", cancellationToken);
        await GarantirSucessoAsync(response, cancellationToken);

        var resultado = await response.Content.ReadFromJsonAsync<SaldoRespostaDto>(
            cancellationToken: cancellationToken);
        return resultado?.SaldoConsolidado
               ?? throw new InvalidOperationException("A API não retornou o saldo consolidado.");
    }

    private static string InicioDoDiaUtc(DateOnly data) =>
        new DateTimeOffset(data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToString("O", CultureInfo.InvariantCulture);

    private static string FimDoDiaUtc(DateOnly data) =>
        new DateTimeOffset(data.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)).ToString("O", CultureInfo.InvariantCulture);

    private static async Task GarantirSucessoAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var mensagem = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "A API rejeitou os dados enviados. Confira os campos e os filtros.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "A API não autorizou a operação. Confira a configuração de acesso no servidor.",
            HttpStatusCode.NotFound => "A rota solicitada não foi encontrada na API.",
            _ => "Não foi possível concluir a operação na API. Tente novamente mais tarde."
        };

        throw new HttpRequestException(mensagem, null, response.StatusCode);
    }

    private sealed class SaldoRespostaDto
    {
        public decimal SaldoConsolidado { get; init; }
    }
}
