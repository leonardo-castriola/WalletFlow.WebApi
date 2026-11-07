namespace WalletFlow.Application.DTOs;

public sealed record OrcamentoMensalDto(
    int Ano,
    int Mes,
    decimal Limite,
    decimal Despesas,
    decimal PercentualUtilizado,
    bool AlertaEnviado,
    string Modo = "valor",
    decimal? Percentual = null,
    bool ConfiguracaoHerdada = false,
    int? AnoConfiguracaoOrigem = null,
    int? MesConfiguracaoOrigem = null);

public sealed record AtualizarOrcamentoMensalDto(
    decimal Limite,
    string Modo = "valor",
    decimal? Percentual = null);

public sealed record AlertaOrcamentoDto(
    int Ano,
    int Mes,
    decimal Limite,
    decimal Despesas,
    decimal PercentualUtilizado);
