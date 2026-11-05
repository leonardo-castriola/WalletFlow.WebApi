using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WalletFlow.Blazor.DTOs;

namespace WalletFlow.Blazor.Services;

public static class ExtratoPdfGenerator
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

    public static byte[] Gerar(
        IReadOnlyList<TransacaoRespostaDto> transacoes,
        string periodo,
        string tipo,
        Func<DateTimeOffset, DateTimeOffset> converterFuso)
    {
        var receitas = transacoes.Where(item => item.TipoFormatado == "Receita").Sum(item => item.Valor);
        var despesas = transacoes.Where(item => item.TipoFormatado == "Despesa").Sum(item => item.Valor);

        return Document.Create(document =>
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(9).FontColor("#344054"));

                page.Header().Column(header =>
                {
                    header.Item().Text("WalletFlow").FontSize(11).SemiBold().FontColor("#2563EB");
                    header.Item().PaddingTop(8).Text("Extrato financeiro").FontSize(22).Bold().FontColor("#172033");
                    header.Item().PaddingTop(3).Text("Período: " + periodo + "   •   Tipo: " + tipo).FontSize(9).FontColor("#667085");
                    header.Item().PaddingTop(3).Text("Gerado em " + DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm", Cultura)).FontSize(8).FontColor("#98A2B3");
                });

                page.Content().PaddingTop(18).Column(content =>
                {
                    content.Spacing(14);
                    content.Item().Row(row =>
                    {
                        Resumo(row.RelativeItem(), "RECEITAS", receitas.ToString("C", Cultura), "#EAF8F0", "#157347");
                        row.ConstantItem(10);
                        Resumo(row.RelativeItem(), "DESPESAS", despesas.ToString("C", Cultura), "#FFF1F0", "#B42318");
                        row.ConstantItem(10);
                        Resumo(row.RelativeItem(), "SALDO DO PERÍODO", (receitas - despesas).ToString("C", Cultura), "#EFF6FF", "#1D4ED8");
                    });

                    content.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4.2f);
                            columns.RelativeColumn(1.35f);
                            columns.RelativeColumn(1.25f);
                            columns.RelativeColumn(1.65f);
                        });

                        table.Header(header =>
                        {
                            Cabecalho(header.Cell(), "Descrição");
                            Cabecalho(header.Cell(), "Data");
                            Cabecalho(header.Cell(), "Tipo");
                            Cabecalho(header.Cell().AlignRight(), "Valor");
                        });

                        foreach (var transacao in transacoes)
                        {
                            var data = converterFuso(transacao.DataCadastro).ToString("dd/MM/yyyy", Cultura);
                            Celula(table.Cell(), transacao.Descricao);
                            Celula(table.Cell(), data);
                            Celula(table.Cell(), transacao.TipoFormatado,
                                transacao.TipoFormatado == "Receita" ? "#157347" : "#B42318");
                            Celula(table.Cell().AlignRight(), transacao.Valor.ToString("C", Cultura), "#172033");
                        }

                        if (transacoes.Count == 0)
                        {
                            table.Cell().ColumnSpan(4).Padding(12).AlignCenter()
                                .Text("Nenhuma movimentação encontrada para os filtros selecionados.")
                                .FontColor("#667085");
                        }
                    });
                });

                page.Footer().PaddingTop(10).AlignCenter().Text(text =>
                {
                    text.Span("WalletFlow  •  Página ").FontSize(8).FontColor("#98A2B3");
                    text.CurrentPageNumber().FontSize(8).FontColor("#98A2B3");
                });
            })).GeneratePdf();
    }

    private static void Resumo(IContainer container, string titulo, string valor, string fundo, string cor)
    {
        container.Background(fundo).Border(1).BorderColor("#E4E7EC").Padding(10).Column(column =>
        {
            column.Item().Text(titulo).FontSize(7).SemiBold().FontColor(cor);
            column.Item().PaddingTop(4).Text(valor).FontSize(11).Bold().FontColor("#172033");
        });
    }

    private static void Cabecalho(IContainer container, string valor)
    {
        container.Background("#F3F6FB").BorderBottom(1).BorderColor("#D9E2EF")
            .PaddingVertical(8).PaddingHorizontal(6)
            .DefaultTextStyle(style => style.FontSize(8).SemiBold().FontColor("#475467"))
            .Text(valor);
    }

    private static void Celula(IContainer container, string valor, string cor = "#344054")
    {
        container.BorderBottom(.5f).BorderColor("#E4E7EC").PaddingVertical(7).PaddingHorizontal(6)
            .DefaultTextStyle(style => style.FontSize(8).FontColor(cor))
            .Text(valor);
    }
}
