using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WalletFlow.Infrastructure.Persistence;

public sealed class WalletUser : IdentityUser
{
    [PersonalData, MaxLength(120)]
    public string? NomeCompleto { get; set; }

    [PersonalData, MaxLength(11)]
    public string? Cpf { get; set; }

    [PersonalData, MaxLength(8)]
    public string? EnderecoCep { get; set; }

    [PersonalData, MaxLength(160)]
    public string? EnderecoLogradouro { get; set; }

    [PersonalData, MaxLength(30)]
    public string? EnderecoNumero { get; set; }

    [PersonalData, MaxLength(100)]
    public string? EnderecoComplemento { get; set; }

    [PersonalData, MaxLength(100)]
    public string? EnderecoBairro { get; set; }

    [PersonalData, MaxLength(100)]
    public string? EnderecoCidade { get; set; }

    [PersonalData, MaxLength(2)]
    public string? EnderecoEstado { get; set; }
}
