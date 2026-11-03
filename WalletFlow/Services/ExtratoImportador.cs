using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using WalletFlow.WebApi.DTOs;
using WalletFlow.WebApi.Enums;

namespace WalletFlow.WebApi.Services;

public sealed class ExtratoImportador : IExtratoImportador
{
    private const int MaximoLancamentos = 500;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] DateFormats = ["yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd"];
    private static readonly Regex OfxBlock = new("<STMTTRN>(?<body>.*?)</STMTTRN>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OfxField = new("<(?<name>[A-Z0-9]+)>(?<value>[^<\\r\\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ExtratoImportacaoPreviaDto Analisar(string nomeArquivo, string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo)) throw new FormatException("O arquivo está vazio.");
        var extension = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        var (linhas, erros, total) = extension switch
        {
            ".csv" => AnalisarCsv(conteudo),
            ".ofx" => AnalisarOfx(conteudo),
            _ => throw new FormatException("Formato não suportado. Envie um arquivo .CSV ou .OFX.")
        };
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var resultado = linhas.Select(linha =>
        {
            var fingerprint = Fingerprint(linha.Descricao, linha.Valor, linha.TipoFormatado, linha.Data);
            return linha with { DuplicadaNoArquivo = !vistos.Add(fingerprint), GrupoDuplicidade = fingerprint[..12] };
        }).ToList();
        return new ExtratoImportacaoPreviaDto(nomeArquivo, resultado, erros, total, resultado.Count(l => l.DuplicadaNoArquivo));
    }

    private static (List<ExtratoImportacaoLinhaDto> Linhas, List<ExtratoImportacaoErroDto> Erros, int Total) AnalisarCsv(string conteudo)
    {
        using var reader = new StringReader(conteudo.TrimStart('\uFEFF'));
        using var parser = new TextFieldParser(reader) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(DetectarDelimitador(conteudo));
        if (parser.EndOfData) throw new FormatException("O CSV não contém cabeçalho.");
        var headers = parser.ReadFields() ?? [];
        var map = headers.Select((h, i) => (Nome: NormalizarCabecalho(h), Indice: i))
            .GroupBy(x => x.Nome, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Indice, StringComparer.OrdinalIgnoreCase);
        var dateColumn = Encontrar(map, "data", "date", "dt", "posteddate", "datamovimento");
        var descriptionColumn = Encontrar(map, "descricao", "description", "memo", "historico", "lancamento", "name");
        var amountColumn = Encontrar(map, "valor", "amount", "trnamt", "value");
        var typeColumn = Encontrar(map, "tipo", "type", "trntype", "natureza");
        if (dateColumn is null || descriptionColumn is null || amountColumn is null)
            throw new FormatException("O cabeçalho do CSV deve conter Data, Descrição e Valor. A coluna Tipo é opcional para valores com sinal.");

        var linhas = new List<ExtratoImportacaoLinhaDto>();
        var erros = new List<ExtratoImportacaoErroDto>();
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            if (rowNumber > MaximoLancamentos + 1)
            {
                erros.Add(new(rowNumber, $"O arquivo excede o limite de {MaximoLancamentos} linhas de lançamento."));
                break;
            }
            string[] fields;
            try { fields = parser.ReadFields() ?? []; }
            catch (MalformedLineException) { erros.Add(new(rowNumber, "Linha CSV malformada.")); continue; }
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            try
            {
                var date = ParseDate(Value(fields, dateColumn.Value));
                var description = ValidarDescricao(Value(fields, descriptionColumn.Value).Trim());
                var amount = ParseAmount(Value(fields, amountColumn.Value));
                var kind = ParseKind(typeColumn is null ? null : Value(fields, typeColumn.Value), amount);
                linhas.Add(new(rowNumber, description, ValidarValor(amount), kind.ToString(), date, false, false, string.Empty));
            }
            catch (FormatException ex) { erros.Add(new(rowNumber, ex.Message)); }
        }
        return (linhas, erros, rowNumber - 1);
    }

    private static (List<ExtratoImportacaoLinhaDto> Linhas, List<ExtratoImportacaoErroDto> Erros, int Total) AnalisarOfx(string conteudo)
    {
        var blocos = OfxBlock.Matches(conteudo);
        if (blocos.Count == 0) throw new FormatException("Não foram encontrados lançamentos <STMTTRN> no arquivo OFX.");
        if (blocos.Count > MaximoLancamentos)
            throw new FormatException($"O arquivo excede o limite de {MaximoLancamentos} lançamentos.");
        var linhas = new List<ExtratoImportacaoLinhaDto>();
        var erros = new List<ExtratoImportacaoErroDto>();
        for (var i = 0; i < blocos.Count; i++)
        {
            var fields = OfxField.Matches(blocos[i].Groups["body"].Value)
                .GroupBy(m => m.Groups["name"].Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Groups["value"].Value.Trim(), StringComparer.OrdinalIgnoreCase);
            try
            {
                fields.TryGetValue("DTPOSTED", out var dateText);
                fields.TryGetValue("TRNAMT", out var amountText);
                fields.TryGetValue("NAME", out var name);
                fields.TryGetValue("MEMO", out var memo);
                var date = ParseDate(dateText ?? string.Empty);
                var amount = ParseAmount(amountText ?? string.Empty);
                var description = ValidarDescricao(string.IsNullOrWhiteSpace(name) ? memo ?? string.Empty : name);
                var kind = amount < 0 ? TipoTransacao.Despesa : TipoTransacao.Receita;
                linhas.Add(new(i + 1, description, ValidarValor(amount), kind.ToString(), date, false, false, string.Empty));
            }
            catch (FormatException ex) { erros.Add(new(i + 1, ex.Message)); }
        }
        return (linhas, erros, blocos.Count);
    }

    public static string Fingerprint(string descricao, decimal valor, string tipo, DateTimeOffset data)
    {
        var normalized = descricao.Normalize(NormalizationForm.FormD);
        var clean = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        clean = Regex.Replace(clean.ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
        var raw = $"{data.UtcDateTime:yyyy-MM-dd}|{tipo.ToUpperInvariant()}|{decimal.Round(valor, 2).ToString("F2", CultureInfo.InvariantCulture)}|{clean}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static string DetectarDelimitador(string text)
    {
        var firstLine = text.Split(["\r\n", "\n"], StringSplitOptions.None).FirstOrDefault() ?? string.Empty;
        return new[] { ';', ',', '\t' }.OrderByDescending(c => firstLine.Count(x => x == c)).First().ToString();
    }
    private static int? Encontrar(Dictionary<string, int> map, params string[] names)
    {
        foreach (var name in names) if (map.TryGetValue(name, out var index)) return index;
        foreach (var pair in map)
            if (names.Any(name => pair.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase))) return pair.Value;
        return null;
    }
    private static string NormalizarCabecalho(string? value) => Regex.Replace((value ?? string.Empty).Normalize(NormalizationForm.FormD), @"\p{Mn}|[^A-Za-z0-9]", string.Empty).ToLowerInvariant();
    private static string Value(string[] fields, int index) => index < fields.Length ? fields[index] : string.Empty;
    private static string ValidarDescricao(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new FormatException("Descrição não informada.")
        : value.Length > 100 ? throw new FormatException("A descrição excede o limite de 100 caracteres.") : value;

    private static decimal ValidarValor(decimal value)
    {
        value = decimal.Abs(value);
        if (value <= 0 || value > 99999999.99m)
            throw new FormatException("O valor deve ser maior que zero e não pode passar de R$ 99.999.999,99.");
        if (decimal.Round(value, 2) != value)
            throw new FormatException("O valor não pode ter mais de duas casas decimais.");
        return value;
    }

    private static DateTimeOffset ParseDate(string value)
    {
        var compact = Regex.Match(value, @"^\s*(\d{8})");
        if (compact.Success && DateTime.TryParseExact(compact.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ofxDate))
            return new DateTimeOffset(DateTime.SpecifyKind(ofxDate.Date, DateTimeKind.Utc));
        if (DateTime.TryParseExact(value.Trim(), ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd"], PtBr, DateTimeStyles.None, out var date))
            return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc));
        if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
            return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc));
        throw new FormatException("Data inválida; use dd/MM/aaaa ou a data padrão do OFX.");
    }

    private static decimal ParseAmount(string value)
    {
        var raw = value.Trim().Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(" ", string.Empty);
        if (raw.Contains(',') && raw.Contains('.'))
            raw = raw.LastIndexOf(',') > raw.LastIndexOf('.') ? raw.Replace(".", string.Empty).Replace(',', '.') : raw.Replace(",", string.Empty);
        else if (raw.Contains(',')) raw = raw.Replace(',', '.');
        if (decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowParentheses, CultureInfo.InvariantCulture, out var amount)) return amount;
        throw new FormatException("Valor inválido; confira o separador decimal.");
    }

    private static TipoTransacao ParseKind(string? value, decimal amount)
    {
        var type = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (type is "receita" or "credito" or "crédito" or "credit" or "creditado" or "depósito" or "deposit" or "income" or "1") return TipoTransacao.Receita;
        if (type is "despesa" or "debito" or "débito" or "debit" or "debited" or "saque" or "pagamento" or "payment" or "expense" or "0") return TipoTransacao.Despesa;
        if (amount < 0) return TipoTransacao.Despesa;
        if (amount > 0 && string.IsNullOrWhiteSpace(type)) return TipoTransacao.Receita;
        throw new FormatException("Tipo inválido. Use Receita/Despesa, Crédito/Débito ou informe o valor com sinal.");
    }
}
