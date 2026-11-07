using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WalletFlow.Infrastructure.Persistence;

#nullable disable

namespace WalletFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WalletDbContext))]
[Migration("20261107200000_AlertOnlyWhenMonthlyBudgetReached")]
public sealed class AlertOnlyWhenMonthlyBudgetReached : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "AlertaOitentaPorcentoEnviadoEmUtc",
            table: "OrcamentosMensais",
            newName: "AlertaLimiteAtingidoEmUtc");

        // Previous markers represented the 80% warning. The new notification is
        // sent at 100%, so those markers must not suppress the new threshold.
        migrationBuilder.Sql("UPDATE \"OrcamentosMensais\" SET \"AlertaLimiteAtingidoEmUtc\" = NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "AlertaLimiteAtingidoEmUtc",
            table: "OrcamentosMensais",
            newName: "AlertaOitentaPorcentoEnviadoEmUtc");
    }
}
