using System.Threading.Tasks;

namespace WalletFlow.WebApi.Services
{
    public interface IFinanceiroService
    {
        Task<decimal> CalcularSaldoConsolidadoAsync(CancellationToken cancellationToken = default);
    }
}
