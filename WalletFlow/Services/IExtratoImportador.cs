using WalletFlow.WebApi.DTOs;

namespace WalletFlow.WebApi.Services;

public interface IExtratoImportador
{
    ExtratoImportacaoPreviaDto Analisar(string nomeArquivo, string conteudo);
}
