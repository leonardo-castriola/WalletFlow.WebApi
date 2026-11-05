namespace WalletFlow.Infrastructure.Persistence;

public sealed class MonthlyReportDelivery
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
