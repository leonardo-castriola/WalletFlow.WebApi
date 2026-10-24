using System;
using WalletFlow.WebApi.Enums;

namespace WalletFlow.WebApi.Entities
{
    public class Transacao
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public TipoTransacao Tipo { get; set; }
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;
    }
}
