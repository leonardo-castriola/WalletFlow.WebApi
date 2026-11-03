using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WalletFlow.Application.Abstractions;
using WalletFlow.Domain.Entities;
using WalletFlow.Domain.Enums;
using WalletFlow.Infrastructure.Persistence;

namespace WalletFlow.Infrastructure.Repositories;

public sealed class EfTransacaoRepository(WalletDbContext context) : ITransacaoRepository
{
    public async Task<(IReadOnlyList<Transacao> Items, int TotalCount)> ListarAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.Transacoes.AsNoTracking().Where(t => t.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(t => t.Id).Skip((int)((long)(page - 1) * pageSize)).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(IReadOnlyList<Transacao> Items, int TotalCount)> FiltrarAsync(string userId, TipoTransacao? tipo, DateTimeOffset? dataInicio, DateTimeOffset? dataFim, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // SQLite persists DateTimeOffset values as text; julianday compares their instants consistently.
        var inicio = dataInicio?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        var fim = dataFim?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        var query = context.Transacoes.FromSqlRaw(
                "SELECT * FROM \"Transacoes\" WHERE (@inicio IS NULL OR julianday(\"DataCadastro\") >= julianday(@inicio)) AND (@fim IS NULL OR julianday(\"DataCadastro\") <= julianday(@fim))",
                new SqliteParameter("@inicio", (object?)inicio ?? DBNull.Value),
                new SqliteParameter("@fim", (object?)fim ?? DBNull.Value))
            .AsNoTracking().Where(t => t.UserId == userId);
        if (tipo.HasValue) query = query.Where(t => t.Tipo == tipo.Value);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(t => t.Id).Skip((int)((long)(page - 1) * pageSize)).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Transacao?> ObterAsync(string userId, int id, CancellationToken cancellationToken = default) =>
        context.Transacoes.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Transacao>> ListarTodasAsync(string userId, CancellationToken cancellationToken = default) =>
        await context.Transacoes.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(cancellationToken);

    public async Task AdicionarAsync(Transacao transacao, CancellationToken cancellationToken = default)
    {
        context.Transacoes.Add(transacao);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AdicionarVariosAsync(IReadOnlyCollection<Transacao> transacoes, CancellationToken cancellationToken = default)
    {
        context.Transacoes.AddRange(transacoes);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

    public async Task ExcluirAsync(Transacao transacao, CancellationToken cancellationToken = default)
    {
        context.Transacoes.Remove(transacao);
        await context.SaveChangesAsync(cancellationToken);
    }
}
