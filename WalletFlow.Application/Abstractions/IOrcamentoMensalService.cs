using WalletFlow.Application.DTOs;

namespace WalletFlow.Application.Abstractions;

public interface IOrcamentoMensalService
{
    Task<OrcamentoMensalDto> ObterAsync(string userId, int ano, int mes, CancellationToken cancellationToken = default);
    Task<OrcamentoMensalDto> SalvarAsync(string userId, int ano, int mes, decimal limite, CancellationToken cancellationToken = default);
    Task<AlertaOrcamentoDto?> TentarRegistrarAlertaAsync(string userId, int ano, int mes, CancellationToken cancellationToken = default);
}
