using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WalletFlow.Infrastructure.Persistence;

#nullable disable

namespace WalletFlow.Migrations;

[DbContext(typeof(WalletDbContext))]
[Migration("20261106160000_AddWalletUserProfile")]
public partial class AddWalletUserProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "NomeCompleto", table: "AspNetUsers", type: "TEXT", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Cpf", table: "AspNetUsers", type: "TEXT", maxLength: 11, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EnderecoCep", table: "AspNetUsers", type: "TEXT", maxLength: 8, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EnderecoLogradouro", table: "AspNetUsers", type: "TEXT", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EnderecoNumero", table: "AspNetUsers", type: "TEXT", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EnderecoComplemento", table: "AspNetUsers", type: "TEXT", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EnderecoBairro", table: "AspNetUsers", type: "TEXT", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EnderecoCidade", table: "AspNetUsers", type: "TEXT", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EnderecoEstado", table: "AspNetUsers", type: "TEXT", maxLength: 2, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "NomeCompleto", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "Cpf", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "EnderecoCep", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "EnderecoLogradouro", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "EnderecoNumero", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "EnderecoComplemento", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "EnderecoBairro", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "EnderecoCidade", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "EnderecoEstado", table: "AspNetUsers");
    }
}
