using System.Threading.Tasks;

namespace WalletFlow.WebApi.Services
{
    public interface IFinanceiroService
    {
        Task<decimal> CalcularSaldoConsolidadoAsync(string userId, CancellationToken cancellationToken = default);
    }
}
