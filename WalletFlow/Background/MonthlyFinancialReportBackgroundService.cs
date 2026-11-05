using Microsoft.Extensions.Options;
using WalletFlow.Application.Abstractions;

namespace WalletFlow.Api.Background;

public sealed class MonthlyFinancialReportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<MonthlyReportScheduleOptions> options,
    ILogger<MonthlyFinancialReportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = options.Value;
        if (!schedule.Enabled)
        {
            logger.LogInformation("O envio agendado de relatórios mensais está desativado.");
            return;
        }

        if (schedule.Day is < 1 or > 28 || schedule.Hour is < 0 or > 23 || schedule.Minute is < 0 or > 59)
            throw new InvalidOperationException("Configure Reports:Monthly:Day entre 1 e 28 e um horário válido.");
        if (schedule.PollIntervalMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Reports:Monthly:PollIntervalMinutes deve estar entre 1 e 1440.");

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        logger.LogInformation(
            "Agendador de relatórios mensais ativo para o dia {Day}, às {Hour:D2}:{Minute:D2} ({TimeZone}).",
            schedule.Day, schedule.Hour, schedule.Minute, schedule.TimeZoneId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
                if (IsDue(localNow, schedule))
                {
                    var previousMonth = new DateTime(localNow.Year, localNow.Month, 1).AddMonths(-1);
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IMonthlyFinancialReportDispatcher>();
                    var sent = await dispatcher.SendPendingReportsAsync(
                        previousMonth.Year,
                        previousMonth.Month,
                        schedule.TimeZoneId,
                        stoppingToken);

                    logger.LogInformation(
                        "Foram enviados {SentCount} relatórios financeiros de {Year}-{Month:D2}.",
                        sent, previousMonth.Year, previousMonth.Month);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao processar os relatórios financeiros mensais.");
            }

            await Task.Delay(TimeSpan.FromMinutes(schedule.PollIntervalMinutes), stoppingToken);
        }
    }

    private static bool IsDue(DateTimeOffset localNow, MonthlyReportScheduleOptions schedule) =>
        localNow.Day >= schedule.Day &&
        (localNow.Hour > schedule.Hour || localNow.Hour == schedule.Hour && localNow.Minute >= schedule.Minute);
}
