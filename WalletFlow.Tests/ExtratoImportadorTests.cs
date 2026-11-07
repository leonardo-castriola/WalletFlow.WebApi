using WalletFlow.Application.Services;
using Xunit;

namespace WalletFlow.Tests;

public sealed class ExtratoImportadorTests
{
    private readonly ExtratoImportador _importador = new();

    [Fact]
    public void CsvComCabecalhoAcentuadoEValorBrasileiroPreservaDescricaoEValor()
    {
        const string csv = "Data;Descrição;Valor;Tipo\n01/05/2026;\"Mercado; feira\";-R$ 1.234,50;Despesa";

        var previa = _importador.Analisar("extrato.csv", csv);

        var lancamento = Assert.Single(previa.Lancamentos);
        Assert.Equal("Mercado; feira", lancamento.Descricao);
        Assert.Equal(1234.50m, lancamento.Valor);
        Assert.Equal("Despesa", lancamento.TipoFormatado);
        Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), lancamento.Data);
        Assert.Empty(previa.Erros);
        Assert.Equal(1, previa.TotalLidos);
    }

    [Fact]
    public void CsvComValorComSinalInfereReceitaEDespesaQuandoTipoNaoFoiInformado()
    {
        const string csv = "date,description,amount\n2026-05-01,Salário,2500.75\n2026-05-02,Conta de luz,-89.90";

        var previa = _importador.Analisar("extrato.csv", csv);

        Assert.Collection(previa.Lancamentos,
            receita =>
            {
                Assert.Equal("Salário", receita.Descricao);
                Assert.Equal(2500.75m, receita.Valor);
                Assert.Equal("Receita", receita.TipoFormatado);
            },
            despesa =>
            {
                Assert.Equal("Conta de luz", despesa.Descricao);
                Assert.Equal(89.90m, despesa.Valor);
                Assert.Equal("Despesa", despesa.TipoFormatado);
            });
    }

    [Fact]
    public void CsvMarcaDuplicatasNoArquivoIgnorandoAcentosEMaiusculas()
    {
        const string csv = "Data;Descrição;Valor;Tipo\n2026-05-01;Café;12,50;Despesa\n01/05/2026;cafe;12.50;Despesa";

        var previa = _importador.Analisar("extrato.csv", csv);

        Assert.Equal(2, previa.Lancamentos.Count);
        Assert.False(previa.Lancamentos[0].DuplicadaNoArquivo);
        Assert.True(previa.Lancamentos[1].DuplicadaNoArquivo);
        Assert.Equal(previa.Lancamentos[0].GrupoDuplicidade, previa.Lancamentos[1].GrupoDuplicidade);
        Assert.Equal(1, previa.TotalDuplicados);
    }

    [Fact]
    public void CsvRetornaErrosPorLinhaSemDescartarLancamentosValidos()
    {
        const string csv = "Data,Descrição,Valor,Tipo\n2026-05-01,Almoço,35.90,Despesa\ndia inválido,Sem data,20,Despesa\n2026-05-03,Sem valor,n/a,Despesa";

        var previa = _importador.Analisar("extrato.csv", csv);

        Assert.Single(previa.Lancamentos);
        Assert.Equal(2, previa.Erros.Count);
        Assert.Contains(previa.Erros, erro => erro.Mensagem.Contains("Data inválida", StringComparison.Ordinal));
        Assert.Contains(previa.Erros, erro => erro.Mensagem.Contains("Valor inválido", StringComparison.Ordinal));
    }

    [Fact]
    public void OfxInterpretaSinalEUsaMemoQuandoNomeEstaAusente()
    {
        const string ofx = """
            OFXHEADER:100
            DATA:OFXSGML
            <OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><BANKTRANLIST>
            <STMTTRN><TRNTYPE>DEBIT<DTPOSTED>20260510120000[-3:BRT]<TRNAMT>-42.75<NAME>Farmácia</STMTTRN>
            <STMTTRN><TRNTYPE>CREDIT<DTPOSTED>20260511120000[-3:BRT]<TRNAMT>850.00<MEMO>Pagamento recebido</STMTTRN>
            </BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>
            """;

        var previa = _importador.Analisar("banco.ofx", ofx);

        Assert.Equal(2, previa.Lancamentos.Count);
        Assert.Equal("Farmácia", previa.Lancamentos[0].Descricao);
        Assert.Equal(42.75m, previa.Lancamentos[0].Valor);
        Assert.Equal("Despesa", previa.Lancamentos[0].TipoFormatado);
        Assert.Equal("Pagamento recebido", previa.Lancamentos[1].Descricao);
        Assert.Equal("Receita", previa.Lancamentos[1].TipoFormatado);
        Assert.All(previa.Lancamentos, item => Assert.Equal(TimeSpan.Zero, item.Data.Offset));
        Assert.Empty(previa.Erros);
    }

    [Fact]
    public void CsvSemCabecalhoObrigatorioEFormatoNaoSuportadoSaoRejeitados()
    {
        var erroCabecalho = Assert.Throws<FormatException>(() =>
            _importador.Analisar("extrato.csv", "Data,Valor\n2026-05-01,10"));
        var erroFormato = Assert.Throws<FormatException>(() =>
            _importador.Analisar("extrato.xlsx", "conteúdo"));
        var erroOfx = Assert.Throws<FormatException>(() =>
            _importador.Analisar("extrato.ofx", "<OFX><BANKTRANLIST></BANKTRANLIST></OFX>"));

        Assert.Contains("cabeçalho", erroCabecalho.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não suportado", erroFormato.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STMTTRN", erroOfx.Message, StringComparison.Ordinal);
    }
}
