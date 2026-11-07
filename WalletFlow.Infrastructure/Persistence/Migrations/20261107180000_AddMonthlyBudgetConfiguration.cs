using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WalletFlow.Infrastructure.Persistence;

#nullable disable

namespace WalletFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WalletDbContext))]
[Migration("20261107180000_AddMonthlyBudgetConfiguration")]
public sealed class AddMonthlyBudgetConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Modo",
            table: "OrcamentosMensais",
            type: "TEXT",
            nullable: false,
            defaultValue: "valor");

        migrationBuilder.AddColumn<decimal>(
            name: "Percentual",
            table: "OrcamentosMensais",
            type: "TEXT",
            precision: 5,
            scale: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Modo", table: "OrcamentosMensais");
        migrationBuilder.DropColumn(name: "Percentual", table: "OrcamentosMensais");
    }
}
