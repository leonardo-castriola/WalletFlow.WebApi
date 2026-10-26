using System.ComponentModel.DataAnnotations;
using WalletFlow.WebApi.Enums;

namespace WalletFlow.WebApi.DTOs
{
    public class TransacaoCriacaoDto : IValidatableObject
    {
        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [StringLength(100, ErrorMessage = "A descrição não pode passar de 100 caracteres.")]
        public string Descricao { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "O valor deve ser maior que zero.", ParseLimitsInInvariantCulture = true)]
        public decimal Valor { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (decimal.Round(Valor, 2) != Valor)
                yield return new ValidationResult(
                    "O valor deve ter no máximo duas casas decimais.",
                    [nameof(Valor)]);
        }

        [Required(ErrorMessage = "O tipo de transação é obrigatório.")]
        [EnumDataType(typeof(TipoTransacao), ErrorMessage = "O tipo deve ser 0 (Despesa) ou 1 (Receita).")]
        public TipoTransacao? Tipo { get; set; }
    }
}
