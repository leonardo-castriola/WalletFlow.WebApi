using System.Threading.Tasks;

namespace WalletFlow.Application.Abstractions
{
    public interface IFinanceiroService
    {
        Task<decimal> CalcularSaldoConsolidadoAsync(string userId, CancellationToken cancellationToken = default);
    }
}
