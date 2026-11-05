using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletFlow.Infrastructure.Persistence.Migrations;

public partial class AddMonthlyBudgets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrcamentosMensais",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                Ano = table.Column<int>(type: "INTEGER", nullable: false),
                Mes = table.Column<int>(type: "INTEGER", nullable: false),
                Limite = table.Column<decimal>(type: "TEXT", precision: 11, scale: 2, nullable: false),
                AlertaOitentaPorcentoEnviadoEmUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrcamentosMensais", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrcamentosMensais_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrcamentosMensais_UserId_Ano_Mes",
            table: "OrcamentosMensais",
            columns: new[] { "UserId", "Ano", "Mes" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "OrcamentosMensais");
}
