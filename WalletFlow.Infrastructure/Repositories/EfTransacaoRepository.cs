using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Entities;
using WalletFlow.Domain.Enums;
using WalletFlow.Infrastructure.Persistence;

namespace WalletFlow.Infrastructure.Repositories;

public sealed class EfTransacaoRepository : ITransacaoRepository
{
    private readonly WalletDbContext context;
    private readonly TimeZoneInfo timeZone;

    public EfTransacaoRepository(WalletDbContext context, IConfiguration? configuration = null)
    {
        this.context = context;
        timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration?["Filters:TimeZoneId"] ?? "America/Sao_Paulo");
    }

    public async Task<IReadOnlyList<ResumoMensalTransacao>> ObterResumoMensalAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var transacoes = await context.Transacoes.AsNoTracking()
            .Where(transacao => transacao.UserId == userId)
            .Select(transacao => new { transacao.DataCadastro, transacao.Tipo, transacao.Valor })
            .ToListAsync(cancellationToken);

        return transacoes
            .GroupBy(transacao =>
            {
                var dataLocal = TimeZoneInfo.ConvertTime(transacao.DataCadastro, timeZone);
                return (dataLocal.Year, dataLocal.Month);
            })
            .Select(grupo => new ResumoMensalTransacao
            {
                Ano = grupo.Key.Year,
                Mes = grupo.Key.Month,
                Receitas = grupo.Where(transacao => transacao.Tipo == TipoTransacao.Receita).Sum(transacao => transacao.Valor),
                Despesas = grupo.Where(transacao => transacao.Tipo == TipoTransacao.Despesa).Sum(transacao => transacao.Valor)
            })
            .OrderBy(resumo => resumo.Ano)
            .ThenBy(resumo => resumo.Mes)
            .ToList();
    }

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
