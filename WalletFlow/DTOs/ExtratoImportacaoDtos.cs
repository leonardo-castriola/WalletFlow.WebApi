using System.ComponentModel.DataAnnotations;

namespace WalletFlow.WebApi.DTOs;

public sealed record ExtratoImportacaoLinhaDto(
    int Indice,
    string Descricao,
    decimal Valor,
    string TipoFormatado,
    DateTimeOffset Data,
    bool Duplicada,
    bool DuplicadaNoArquivo,
    string GrupoDuplicidade);

public sealed record ExtratoImportacaoErroDto(int Linha, string Mensagem);

public sealed record ExtratoImportacaoPreviaDto(
    string NomeArquivo,
    IReadOnlyList<ExtratoImportacaoLinhaDto> Lancamentos,
    IReadOnlyList<ExtratoImportacaoErroDto> Erros,
    int TotalLidos,
    int TotalDuplicados);

public sealed class ExtratoImportacaoConfirmacaoDto : IValidatableObject
{
    [Required, MinLength(1), MaxLength(500)]
    public List<ExtratoImportacaoItemDto> Lancamentos { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Lancamentos.Count > 500)
            yield return new ValidationResult("Importe no máximo 500 lançamentos por vez.", [nameof(Lancamentos)]);
    }
}

public sealed class ExtratoImportacaoItemDto
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Descricao { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Valor { get; set; }

    [Required]
    public WalletFlow.WebApi.Enums.TipoTransacao Tipo { get; set; }

    public DateTimeOffset Data { get; set; }
}

public sealed record ExtratoImportacaoResultadoDto(int Importados, int IgnoradosComoDuplicados);
