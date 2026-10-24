using System.ComponentModel.DataAnnotations;
using WalletFlow.Blazor.Enums;

namespace WalletFlow.Blazor.DTOs
{
    public class TransacaoCriacaoDto
    {
        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [StringLength(100, ErrorMessage = "A descrição não pode passar de 100 caracteres.")]
        public string Descricao { get; set; } = string.Empty;

        [Required(ErrorMessage = "O valor é obrigatório.")]
        [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "O valor deve ser maior que zero.", ParseLimitsInInvariantCulture = true)]
        public decimal? Valor { get; set; }

        [Required(ErrorMessage = "O tipo de transação é obrigatório.")]
        [EnumDataType(typeof(TipoTransacao), ErrorMessage = "O tipo deve ser 0 (Despesa) ou 1 (Receita).")]
        public TipoTransacao? Tipo { get; set; }
    }
}
