namespace WalletFlow.Domain.Entities;

public sealed class OrcamentoMensal
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Ano { get; set; }
    public int Mes { get; set; }
    public decimal Limite { get; set; }
    public string Modo { get; set; } = "valor";
    public decimal? Percentual { get; set; }
    public DateTimeOffset? AlertaLimiteAtingidoEmUtc { get; set; }
}
