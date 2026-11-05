namespace WalletFlow.Application.Abstractions;

public interface IMonthlyFinancialReportDispatcher
{
    Task<int> SendPendingReportsAsync(int year, int month, string timeZoneId, CancellationToken cancellationToken = default);
}
