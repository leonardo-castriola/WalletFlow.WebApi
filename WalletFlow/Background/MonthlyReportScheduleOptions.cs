namespace WalletFlow.Api.Background;

public sealed class MonthlyReportScheduleOptions
{
    public bool Enabled { get; set; }
    public int Day { get; set; } = 1;
    public int Hour { get; set; } = 8;
    public int Minute { get; set; }
    public int PollIntervalMinutes { get; set; } = 60;
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
}
