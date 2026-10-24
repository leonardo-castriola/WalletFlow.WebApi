using System;

namespace WalletFlow.Blazor.DTOs
{
    public class TransacaoRespostaDto
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public string TipoFormatado { get; set; } = string.Empty;
        public DateTimeOffset DataCadastro { get; set; }
    }
}
