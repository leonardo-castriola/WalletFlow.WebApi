using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Entities;

namespace WalletFlow.WebApi.Data
{
    public class WalletDbContext : DbContext
    {
        public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options)
        {
        }

        public DbSet<Transacao> Transacoes { get; set; }
    }
}
