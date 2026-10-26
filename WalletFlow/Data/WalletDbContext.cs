using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Entities;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace WalletFlow.WebApi.Data
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
                .HasOne(transacao => transacao.User)
                .WithMany()
                .HasForeignKey(transacao => transacao.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
