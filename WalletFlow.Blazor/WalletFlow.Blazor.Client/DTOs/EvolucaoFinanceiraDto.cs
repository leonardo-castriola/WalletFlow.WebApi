namespace WalletFlow.Blazor.DTOs;

public sealed record EvolucaoFinanceiraDto(IReadOnlyList<PontoEvolucaoFinanceiraDto> Meses);

public sealed record PontoEvolucaoFinanceiraDto(
    DateOnly Mes,
    decimal Receitas,
    decimal Despesas,
    decimal Saldo);
