namespace WalletFlow.Application.DTOs;

public sealed record OrcamentoMensalDto(
    int Ano,
    int Mes,
    decimal Limite,
    decimal Despesas,
    decimal PercentualUtilizado,
    bool AlertaEnviado);

public sealed record AtualizarOrcamentoMensalDto(decimal Limite);

public sealed record AlertaOrcamentoDto(
    int Ano,
    int Mes,
    decimal Limite,
    decimal Despesas,
    decimal PercentualUtilizado);
