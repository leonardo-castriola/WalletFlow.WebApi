using WalletFlow.Application.DTOs;

namespace WalletFlow.Application.Abstractions;

public interface IExtratoImportador
{
    ExtratoImportacaoPreviaDto Analisar(string nomeArquivo, string conteudo);
}
