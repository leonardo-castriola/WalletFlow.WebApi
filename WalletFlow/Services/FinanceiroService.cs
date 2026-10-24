using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.Enums;
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

        public async Task<decimal> CalcularSaldoConsolidadoAsync(CancellationToken cancellationToken = default)
        {
            decimal saldo = 0m;
            var transacoes = _context.Transacoes
                .AsNoTracking()
                .Select(t => new { t.Tipo, t.Valor })
                .AsAsyncEnumerable();

            await foreach (var transacao in transacoes.WithCancellation(cancellationToken))
            {
                saldo += transacao.Tipo switch
                {
                    TipoTransacao.Receita => transacao.Valor,
                    TipoTransacao.Despesa => -transacao.Valor,
                    _ => 0m
                };
            }

            return saldo;
        }
    }
}
