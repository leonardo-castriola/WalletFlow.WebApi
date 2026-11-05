using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WalletFlow.Application.Abstractions;
using WalletFlow.Domain.Enums;
using WalletFlow.Infrastructure.Persistence;

namespace WalletFlow.Infrastructure.Reports;

public sealed class MonthlyFinancialReportDispatcher(
    WalletDbContext context,
    IEmailSender emailSender,
    ILogger<MonthlyFinancialReportDispatcher> logger) : IMonthlyFinancialReportDispatcher
{
    public async Task<int> SendPendingReportsAsync(
        int year,
        int month,
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone), TimeSpan.Zero);
        var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart.AddMonths(1), timeZone), TimeSpan.Zero);
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var periodLabel = culture.TextInfo.ToTitleCase(localStart.ToString("MMMM 'de' yyyy", culture));

        var users = await context.Users.AsNoTracking()
            .Where(user => user.EmailConfirmed && user.Email != null && user.Email != "")
            .Select(user => new { user.Id, user.Email })
            .ToListAsync(cancellationToken);

        var deliveredUserIds = await context.MonthlyReportDeliveries.AsNoTracking()
            .Where(delivery => delivery.Year == year && delivery.Month == month)
            .Select(delivery => delivery.UserId)
            .ToListAsync(cancellationToken);
        var delivered = deliveredUserIds.ToHashSet(StringComparer.Ordinal);

        var transactions = await context.Transacoes.FromSqlRaw(
                "SELECT * FROM \"Transacoes\" WHERE julianday(\"DataCadastro\") >= julianday({0}) AND julianday(\"DataCadastro\") < julianday({1})",
                startUtc.ToString("O", CultureInfo.InvariantCulture),
                endUtc.ToString("O", CultureInfo.InvariantCulture))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var totalsByUser = transactions
            .Where(transaction => transaction.UserId is not null)
            .GroupBy(transaction => transaction.UserId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new ReportTotals(
                    group.Where(transaction => transaction.Tipo == TipoTransacao.Receita).Sum(transaction => transaction.Valor),
                    group.Where(transaction => transaction.Tipo == TipoTransacao.Despesa).Sum(transaction => transaction.Valor),
                    group.Count()),
                StringComparer.Ordinal);

        var sentCount = 0;
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (delivered.Contains(user.Id))
                continue;

            var totals = totalsByUser.GetValueOrDefault(user.Id) ?? new ReportTotals(0m, 0m, 0);
            var balance = totals.Incomes - totals.Expenses;
            var subject = $"Seu resumo financeiro de {periodLabel} - WalletFlow";
            var htmlBody = BuildHtml(periodLabel, totals, balance, culture);

            try
            {
                await emailSender.SendAsync(user.Email!, subject, htmlBody, cancellationToken);
                context.MonthlyReportDeliveries.Add(new MonthlyReportDelivery
                {
                    UserId = user.Id,
                    Year = year,
                    Month = month,
                    SentAtUtc = DateTimeOffset.UtcNow
                });
                await context.SaveChangesAsync(cancellationToken);
                sentCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Falha ao enviar o relatório financeiro de {Year}-{Month} para o usuário {UserId}.",
                    year, month, user.Id);
                context.ChangeTracker.Clear();
            }
        }

        return sentCount;
    }

    private static string BuildHtml(string periodLabel, ReportTotals totals, decimal balance, CultureInfo culture)
    {
        var safePeriod = HtmlEncoder.Default.Encode(periodLabel);
        return $"""
            <div style="font-family:Arial,sans-serif;color:#172b4d;max-width:600px;margin:auto">
              <h2>Resumo financeiro de {safePeriod}</h2>
              <p>Confira o consolidado das movimentações da sua conta no período.</p>
              <ul>
                <li>Receitas: <strong>{totals.Incomes.ToString("C", culture)}</strong></li>
                <li>Despesas: <strong>{totals.Expenses.ToString("C", culture)}</strong></li>
                <li>Saldo do período: <strong>{balance.ToString("C", culture)}</strong></li>
                <li>Movimentações: <strong>{totals.TransactionCount}</strong></li>
              </ul>
              <p>Este relatório considera a data de cadastro das movimentações.</p>
              <p>WalletFlow</p>
            </div>
            """;
    }

    private sealed record ReportTotals(decimal Incomes, decimal Expenses, int TransactionCount);
}
