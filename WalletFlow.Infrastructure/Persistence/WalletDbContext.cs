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
        public DbSet<OrcamentoMensal> OrcamentosMensais { get; set; }

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

            builder.Entity<OrcamentoMensal>()
                .HasIndex(item => new { item.UserId, item.Ano, item.Mes })
                .IsUnique();
            builder.Entity<OrcamentoMensal>()
                .Property(item => item.Limite)
                .HasPrecision(11, 2);
            builder.Entity<OrcamentoMensal>()
                .HasOne<WalletUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
