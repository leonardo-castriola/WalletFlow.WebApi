namespace WalletFlow.Blazor.DTOs;

public sealed class ExtratoImportacaoPreviaDto
{
    public string NomeArquivo { get; set; } = string.Empty;
    public List<ExtratoImportacaoLinhaDto> Lancamentos { get; set; } = [];
    public List<ExtratoImportacaoErroDto> Erros { get; set; } = [];
    public int TotalLidos { get; set; }
    public int TotalDuplicados { get; set; }
}

public sealed class ExtratoImportacaoLinhaDto
{
    public int Indice { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string TipoFormatado { get; set; } = string.Empty;
    public DateTimeOffset Data { get; set; }
    public bool Duplicada { get; set; }
    public bool DuplicadaNoArquivo { get; set; }
    public string GrupoDuplicidade { get; set; } = string.Empty;
    public bool EhDuplicada => Duplicada || DuplicadaNoArquivo;
}

public sealed class ExtratoImportacaoErroDto
{
    public int Linha { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public sealed class ExtratoImportacaoConfirmacaoDto
{
    public List<ExtratoImportacaoItemDto> Lancamentos { get; set; } = [];
}

public sealed class ExtratoImportacaoItemDto
{
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public WalletFlow.Blazor.Enums.TipoTransacao Tipo { get; set; }
    public DateTimeOffset Data { get; set; }
}

public sealed class ExtratoImportacaoResultadoDto
{
    public int Importados { get; set; }
    public int IgnoradosComoDuplicados { get; set; }
}
