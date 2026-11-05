using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WalletFlow.Infrastructure.Persistence;

#nullable disable

namespace WalletFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WalletDbContext))]
[Migration("20261105220000_AddMonthlyBudgets")]
partial class AddMonthlyBudgets
{
}
