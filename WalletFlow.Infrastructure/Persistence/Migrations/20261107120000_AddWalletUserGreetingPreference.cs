using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WalletFlow.Infrastructure.Persistence;

#nullable disable

namespace WalletFlow.Migrations;

[DbContext(typeof(WalletDbContext))]
[Migration("20261107120000_AddWalletUserGreetingPreference")]
public partial class AddWalletUserGreetingPreference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "Genero",
            table: "AspNetUsers",
            type: "TEXT",
            maxLength: 16,
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "FormaTratamento", table: "AspNetUsers");
}
