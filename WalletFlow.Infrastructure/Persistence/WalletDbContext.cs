using Microsoft.EntityFrameworkCore;
using WalletFlow.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace WalletFlow.Infrastructure.Persistence
{
    public class WalletDbContext : IdentityDbContext<WalletUser>
    {
        public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options)
        {
        }

        public DbSet<Transacao> Transacoes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Transacao>().HasIndex(transacao => transacao.UserId);
            builder.Entity<Transacao>()
                .HasOne<WalletUser>()
                .WithMany()
                .HasForeignKey(transacao => transacao.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
