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

        public async Task<decimal> CalcularSaldoConsolidadoAsync()
        {
            var transacoes = await _context.Transacoes.ToListAsync();

            // Soma receitas e subtrai despesas
            decimal receitas = transacoes.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => t.Valor);
            decimal despesas = transacoes.Where(t => t.Tipo == TipoTransacao.Despesa).Sum(t => t.Valor);

            return receitas - despesas;
        }
    }
}
