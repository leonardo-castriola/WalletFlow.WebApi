using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Enums;

namespace WalletFlow.Application.Abstractions;

public interface ITransacaoService
{
    Task<TransacaoRespostaDto> CriarAsync(string userId, TransacaoCriacaoDto dto, CancellationToken cancellationToken = default);
    Task<ResultadoPaginadoDto<TransacaoRespostaDto>> ListarAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ResultadoPaginadoDto<TransacaoRespostaDto>> FiltrarAsync(string userId, TipoTransacao? tipo, DateTimeOffset? dataInicio, DateTimeOffset? dataFim, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TransacaoRespostaDto?> ObterAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<bool> AtualizarAsync(string userId, int id, TransacaoCriacaoDto dto, CancellationToken cancellationToken = default);
    Task<bool> ExcluirAsync(string userId, int id, CancellationToken cancellationToken = default);
    Task<decimal> ObterSaldoAsync(string userId, CancellationToken cancellationToken = default);
    Task<ExtratoImportacaoPreviaDto> AnalisarExtratoAsync(string userId, string nomeArquivo, string conteudo, CancellationToken cancellationToken = default);
    Task<ExtratoImportacaoResultadoDto> ImportarExtratoAsync(string userId, ExtratoImportacaoConfirmacaoDto dto, CancellationToken cancellationToken = default);
}
