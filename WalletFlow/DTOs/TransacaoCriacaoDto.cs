using System.ComponentModel.DataAnnotations;
using WalletFlow.WebApi.Enums;

namespace WalletFlow.WebApi.DTOs
{
    public class TransacaoCriacaoDto
    {
        [Required(ErrorMessage = "A descrição é obrigatória.")]
        [StringLength(100, ErrorMessage = "A descrição não pode passar de 100 caracteres.")]
        public string Descricao { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "O valor deve ser maior que zero.")]
        public decimal Valor { get; set; }

        [Required(ErrorMessage = "O tipo de transação é obrigatório.")]
        [EnumDataType(typeof(TipoTransacao), ErrorMessage = "O tipo deve ser 0 (Despesa) ou 1 (Receita).")]
        public TipoTransacao? Tipo { get; set; }
    }
}
