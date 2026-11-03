using System;
using WalletFlow.Domain.Enums;

namespace WalletFlow.Domain.Entities
{
    public class Transacao
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public TipoTransacao Tipo { get; set; }
        public DateTimeOffset DataCadastro { get; set; } = DateTimeOffset.UtcNow;
    }
}
