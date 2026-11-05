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
        public DbSet<MonthlyReportDelivery> MonthlyReportDeliveries { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Transacao>().HasIndex(transacao => transacao.UserId);
            builder.Entity<Transacao>()
                .HasOne<WalletUser>()
                .WithMany()
                .HasForeignKey(transacao => transacao.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MonthlyReportDelivery>()
                .HasIndex(delivery => new { delivery.UserId, delivery.Year, delivery.Month })
                .IsUnique();
            builder.Entity<MonthlyReportDelivery>()
                .HasOne<WalletUser>()
                .WithMany()
                .HasForeignKey(delivery => delivery.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
