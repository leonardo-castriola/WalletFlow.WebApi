using WalletFlow.Domain.Entities;
using WalletFlow.Domain.Enums;

namespace WalletFlow.Application.Abstractions;

public interface ITransacaoRepository
{
    Task<(IReadOnlyList<Transacao> Items, int TotalCount)> ListarAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Transacao> Items, int TotalCount)> FiltrarAsync(string userId, TipoTransacao? tipo, DateTimeOffset? dataInicio, DateTimeOffset? dataFim, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Transacao?> ObterAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transacao>> ListarTodasAsync(string userId, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Transacao transacao, CancellationToken cancellationToken = default);
    Task AdicionarVariosAsync(IReadOnlyCollection<Transacao> transacoes, CancellationToken cancellationToken = default);
    Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default);
    Task ExcluirAsync(Transacao transacao, CancellationToken cancellationToken = default);
}
