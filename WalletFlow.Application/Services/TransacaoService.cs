using WalletFlow.Application.Abstractions;
using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Entities;
using WalletFlow.Domain.Enums;

namespace WalletFlow.Application.Services;

public sealed class TransacaoService(
    ITransacaoRepository repository,
    IFinanceiroService financeiroService,
    IExtratoImportador extratoImportador) : ITransacaoService
{
    private const int MaximoLancamentos = 500;

    public async Task<TransacaoRespostaDto> CriarAsync(string userId, TransacaoCriacaoDto dto, CancellationToken cancellationToken = default)
    {
        var transacao = new Transacao
        {
            UserId = userId,
            Descricao = dto.Descricao.Trim(),
            Valor = dto.Valor,
            Tipo = dto.Tipo!.Value,
            DataCadastro = DateTimeOffset.UtcNow
        };
        await repository.AdicionarAsync(transacao, cancellationToken);
        return Mapear(transacao);
    }

    public async Task<ResultadoPaginadoDto<TransacaoRespostaDto>> ListarAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.ListarAsync(userId, page, pageSize, cancellationToken);
        return CriarPagina(items, totalCount, page, pageSize);
    }

    public async Task<ResultadoPaginadoDto<TransacaoRespostaDto>> FiltrarAsync(string userId, TipoTransacao? tipo, DateTimeOffset? dataInicio, DateTimeOffset? dataFim, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.FiltrarAsync(userId, tipo, dataInicio, dataFim, page, pageSize, cancellationToken);
        return CriarPagina(items, totalCount, page, pageSize);
    }

    public async Task<TransacaoRespostaDto?> ObterAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var transacao = await repository.ObterAsync(userId, id, cancellationToken);
        return transacao is null ? null : Mapear(transacao);
    }

    public async Task<bool> AtualizarAsync(string userId, int id, TransacaoCriacaoDto dto, CancellationToken cancellationToken = default)
    {
        var transacao = await repository.ObterAsync(userId, id, cancellationToken);
        if (transacao is null) return false;
        transacao.Descricao = dto.Descricao.Trim();
        transacao.Valor = dto.Valor;
        transacao.Tipo = dto.Tipo!.Value;
        await repository.SalvarAlteracoesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExcluirAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        var transacao = await repository.ObterAsync(userId, id, cancellationToken);
        if (transacao is null) return false;
        await repository.ExcluirAsync(transacao, cancellationToken);
        return true;
    }

    public Task<decimal> ObterSaldoAsync(string userId, CancellationToken cancellationToken = default) =>
        financeiroService.CalcularSaldoConsolidadoAsync(userId, cancellationToken);

    public async Task<ExtratoImportacaoPreviaDto> AnalisarExtratoAsync(string userId, string nomeArquivo, string conteudo, CancellationToken cancellationToken = default)
    {
        var previa = extratoImportador.Analisar(nomeArquivo, conteudo);
        if (previa.Lancamentos.Count > MaximoLancamentos)
            throw new ArgumentException($"O arquivo contém mais de {MaximoLancamentos} lançamentos válidos. Divida o extrato em arquivos menores.");

        var existentes = await repository.ListarTodasAsync(userId, cancellationToken);
        var fingerprints = existentes.Select(t => ExtratoImportador.Fingerprint(t.Descricao, t.Valor, t.Tipo.ToString(), t.DataCadastro)).ToHashSet(StringComparer.Ordinal);
        var itens = previa.Lancamentos.Select(item =>
        {
            var fingerprint = ExtratoImportador.Fingerprint(item.Descricao, item.Valor, item.TipoFormatado, item.Data);
            return item with { Duplicada = fingerprints.Contains(fingerprint) };
        }).ToList();
        return previa with { Lancamentos = itens, TotalDuplicados = itens.Count(item => item.Duplicada || item.DuplicadaNoArquivo) };
    }

    public async Task<ExtratoImportacaoResultadoDto> ImportarExtratoAsync(string userId, ExtratoImportacaoConfirmacaoDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Lancamentos.Count is < 1 or > MaximoLancamentos)
            throw new ArgumentException($"Selecione de 1 a {MaximoLancamentos} lançamentos para importar.");

        var existentes = await repository.ListarTodasAsync(userId, cancellationToken);
        var fingerprints = existentes.Select(t => ExtratoImportador.Fingerprint(t.Descricao, t.Valor, t.Tipo.ToString(), t.DataCadastro)).ToHashSet(StringComparer.Ordinal);
        var novos = new List<Transacao>();
        var ignorados = 0;
        foreach (var item in dto.Lancamentos)
        {
            if (!Enum.IsDefined(item.Tipo) || item.Valor <= 0 || item.Valor > 99999999.99m || decimal.Round(item.Valor, 2) != item.Valor ||
                string.IsNullOrWhiteSpace(item.Descricao) || item.Descricao.Length > 100)
                throw new ArgumentException("Um ou mais lançamentos têm dados inválidos.");
            var data = item.Data.ToUniversalTime();
            var fingerprint = ExtratoImportador.Fingerprint(item.Descricao, item.Valor, item.Tipo.ToString(), data);
            if (!fingerprints.Add(fingerprint)) { ignorados++; continue; }
            novos.Add(new Transacao { UserId = userId, Descricao = item.Descricao.Trim(), Valor = item.Valor, Tipo = item.Tipo, DataCadastro = data });
        }
        if (novos.Count > 0) await repository.AdicionarVariosAsync(novos, cancellationToken);
        return new ExtratoImportacaoResultadoDto(novos.Count, ignorados);
    }

    private static ResultadoPaginadoDto<TransacaoRespostaDto> CriarPagina(IReadOnlyList<Transacao> items, int totalCount, int page, int pageSize) => new()
    {
        Items = items.Select(Mapear).ToList(),
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
    };

    private static TransacaoRespostaDto Mapear(Transacao item) => new()
    {
        Id = item.Id,
        Descricao = item.Descricao,
        Valor = item.Valor,
        TipoFormatado = item.Tipo.ToString(),
        DataCadastro = item.DataCadastro
    };
}
