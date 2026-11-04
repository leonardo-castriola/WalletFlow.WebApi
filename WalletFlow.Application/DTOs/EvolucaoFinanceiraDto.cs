namespace WalletFlow.Application.DTOs;

public sealed class ResumoMensalTransacao
{
    public int Ano { get; init; }
    public int Mes { get; init; }
    public decimal Receitas { get; init; }
    public decimal Despesas { get; init; }
}

public sealed record PontoEvolucaoFinanceiraDto(
    DateOnly Mes,
    decimal Receitas,
    decimal Despesas,
    decimal Saldo);

public sealed record EvolucaoFinanceiraDto(IReadOnlyList<PontoEvolucaoFinanceiraDto> Meses);
