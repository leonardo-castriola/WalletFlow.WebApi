using Microsoft.EntityFrameworkCore;
using WalletFlow.Application.Abstractions;
using WalletFlow.Infrastructure.Persistence;

namespace WalletFlow.Infrastructure.Repositories;

public sealed class IdentityUsuarioConsulta(WalletDbContext context) : IUsuarioConsulta
{
    public Task<bool> ExisteAsync(string userId, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken);
}
