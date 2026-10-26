using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Data;
using System.Linq;
using System.Threading.Tasks;

namespace WalletFlow.WebApi.Services
{
    public class FinanceiroService : IFinanceiroService
    {
        private readonly WalletDbContext _context;

        public FinanceiroService(WalletDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalcularSaldoConsolidadoAsync(string userId, CancellationToken cancellationToken = default)
        {
            // SQLite stores decimal values as TEXT. Convert each validated currency
            // amount to integer cents before summing, avoiding floating point totals.
            var centavos = await _context.Database.SqlQuery<long>(
                    $"SELECT COALESCE(SUM(CASE WHEN Tipo = 1 THEN CAST(ROUND(CAST(Valor AS REAL) * 100) AS INTEGER) WHEN Tipo = 0 THEN -CAST(ROUND(CAST(Valor AS REAL) * 100) AS INTEGER) ELSE 0 END), 0) AS Value FROM Transacoes WHERE UserId = {userId}")
                .SingleAsync(cancellationToken);

            return centavos / 100m;
        }
    }
}
