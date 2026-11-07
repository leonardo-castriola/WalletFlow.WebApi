using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Text.Encodings.Web;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Entities;
using WalletFlow.Domain.Enums;
using WalletFlow.Infrastructure.Persistence;

namespace WalletFlow.Infrastructure.Repositories;

public sealed class EfOrcamentoMensalService(
    WalletDbContext context,
    IConfiguration configuration,
    IEmailSender emailSender,
    ILogger<EfOrcamentoMensalService> logger) : IOrcamentoMensalService
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["Filters:TimeZoneId"] ?? "America/Sao_Paulo");

    public async Task<OrcamentoMensalDto> ObterAsync(string userId, int ano, int mes, CancellationToken cancellationToken = default)
    {
        ValidarPeriodo(ano, mes);
        var budget = await context.OrcamentosMensais.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Ano == ano && item.Mes == mes, cancellationToken);
        var inherited = false;
        var sourceYear = (int?)null;
        var sourceMonth = (int?)null;
        if (budget is null)
        {
            budget = await ObterConfiguracaoMaisRecenteAsync(userId, cancellationToken);
            inherited = budget is not null;
            sourceYear = inherited ? budget!.Ano : null;
            sourceMonth = inherited ? budget!.Mes : null;
        }
        var expenses = await ObterDespesasAsync(userId, ano, mes, cancellationToken);
        var limit = budget is null ? 0m : await ObterLimiteEfetivoAsync(userId, ano, mes, budget, cancellationToken);
        return CriarDto(ano, mes, limit, expenses, inherited ? false : budget?.AlertaLimiteAtingidoEmUtc is not null,
            budget?.Modo ?? "valor", budget?.Percentual, inherited, sourceYear, sourceMonth);
    }

    public async Task<OrcamentoMensalDto> SalvarAsync(string userId, int ano, int mes, AtualizarOrcamentoMensalDto configuracao, CancellationToken cancellationToken = default)
    {
        ValidarPeriodo(ano, mes);
        var modo = string.IsNullOrWhiteSpace(configuracao.Modo)
            ? string.Empty
            : configuracao.Modo.Trim().ToLowerInvariant();
        if (modo is not ("valor" or "percentual"))
            throw new ArgumentOutOfRangeException(nameof(configuracao), "Escolha um modo de orçamento válido.");

        decimal limite;
        decimal? percentual;
        if (modo == "percentual")
        {
            percentual = configuracao.Percentual;
            if (percentual is null or <= 0 or > 100 || decimal.Round(percentual.Value, 2) != percentual.Value)
                throw new ArgumentOutOfRangeException(nameof(configuracao), "O percentual deve ser maior que zero, até 100%, com no máximo duas casas decimais.");

            var receitas = await ObterTotalTransacoesAsync(userId, ano, mes, TipoTransacao.Receita, cancellationToken);
            limite = decimal.Round(receitas * percentual.Value / 100m, 2);
            if (limite > 999999999.99m)
                throw new ArgumentOutOfRangeException(nameof(configuracao), "O limite calculado ultrapassa o valor máximo permitido.");
        }
        else
        {
            limite = configuracao.Limite;
            percentual = null;
            if (limite <= 0 || limite > 999999999.99m || decimal.Round(limite, 2) != limite)
                throw new ArgumentOutOfRangeException(nameof(configuracao), "O limite deve ser maior que zero e ter até duas casas decimais.");
        }

        var budget = await context.OrcamentosMensais
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Ano == ano && item.Mes == mes, cancellationToken);
        if (budget is null)
        {
            budget = new OrcamentoMensal { UserId = userId, Ano = ano, Mes = mes, Limite = limite, Modo = modo, Percentual = percentual };
            context.OrcamentosMensais.Add(budget);
        }
        else
        {
            if (budget.Modo != modo || budget.Percentual != percentual || budget.Limite != limite)
                budget.AlertaLimiteAtingidoEmUtc = null;
            budget.Limite = limite;
            budget.Modo = modo;
            budget.Percentual = percentual;
        }

        await context.SaveChangesAsync(cancellationToken);
        var expenses = await ObterDespesasAsync(userId, ano, mes, cancellationToken);
        return CriarDto(ano, mes, budget.Limite, expenses, budget.AlertaLimiteAtingidoEmUtc is not null, budget.Modo, budget.Percentual);
    }

    public async Task<AlertaOrcamentoDto?> TentarRegistrarAlertaAsync(string userId, int ano, int mes, CancellationToken cancellationToken = default)
    {
        ValidarPeriodo(ano, mes);
        var budget = await context.OrcamentosMensais.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Ano == ano && item.Mes == mes, cancellationToken);
        var isInherited = budget is null;
        budget ??= await ObterConfiguracaoMaisRecenteAsync(userId, cancellationToken);
        if (budget is null)
            return null;

        var expenses = await ObterDespesasAsync(userId, ano, mes, cancellationToken);
        var limit = await ObterLimiteEfetivoAsync(userId, ano, mes, budget, cancellationToken);
        if (limit <= 0 || expenses < limit)
            return null;

        var now = DateTimeOffset.UtcNow;
        int claimed;
        if (isInherited)
        {
            claimed = await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT OR IGNORE INTO "OrcamentosMensais"
                    ("UserId", "Ano", "Mes", "Limite", "Modo", "Percentual", "AlertaLimiteAtingidoEmUtc")
                VALUES
                    ({userId}, {ano}, {mes}, {limit}, {budget.Modo}, {budget.Percentual}, {now})
                """, cancellationToken);
        }
        else
        {
            claimed = await context.OrcamentosMensais
                .Where(item => item.UserId == userId && item.Ano == ano && item.Mes == mes && item.AlertaLimiteAtingidoEmUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AlertaLimiteAtingidoEmUtc, now), cancellationToken);
        }
        return claimed == 0
            ? null
            : await EnviarEmailEConstruirAlertaAsync(userId, ano, mes, limit, expenses, cancellationToken);
    }

    private async Task<AlertaOrcamentoDto> EnviarEmailEConstruirAlertaAsync(
        string userId, int ano, int mes, decimal limite, decimal despesas, CancellationToken cancellationToken)
    {
        var recipient = await context.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.Email != null && user.Email != "")
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(recipient))
        {
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var period = culture.TextInfo.ToTitleCase(new DateTime(ano, mes, 1).ToString("MMMM 'de' yyyy", culture));
            var percent = CalcularPercentual(despesas, limite);
            var subject = $"Alerta de orçamento — {period}";
            var safePeriod = HtmlEncoder.Default.Encode(period);
            var htmlBody = $"""
                <div style="font-family:Arial,sans-serif;color:#172b4d;max-width:600px;margin:auto">
                  <h2>Alerta de orçamento</h2>
                  <p>Suas despesas de {safePeriod} atingiram ou ultrapassaram o limite mensal (<strong>{percent.ToString("N1", culture)}%</strong> utilizado).</p>
                  <p>Despesas: <strong>{despesas.ToString("C", culture)}</strong><br />
                     Limite: <strong>{limite.ToString("C", culture)}</strong></p>
                  <p>Este aviso é enviado uma única vez quando as despesas alcançam o limite definido para o mês.</p>
                </div>
                """;

            try
            {
                await emailSender.SendAsync(recipient, subject, htmlBody, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Falha ao enviar o alerta de orçamento por e-mail para o usuário {UserId} ({Year}-{Month:D2}).",
                    userId, ano, mes);
            }
        }
        else
        {
            logger.LogWarning("O usuário {UserId} não possui e-mail cadastrado; o alerta será exibido somente na aplicação.", userId);
        }

        return new AlertaOrcamentoDto(ano, mes, limite, despesas, CalcularPercentual(despesas, limite));
    }

    private async Task<decimal> ObterLimiteEfetivoAsync(string userId, int ano, int mes, OrcamentoMensal budget, CancellationToken cancellationToken)
    {
        if (!string.Equals(budget.Modo, "percentual", StringComparison.OrdinalIgnoreCase) || budget.Percentual is null)
            return budget.Limite;

        var receitas = await ObterTotalTransacoesAsync(userId, ano, mes, TipoTransacao.Receita, cancellationToken);
        return decimal.Round(receitas * budget.Percentual.Value / 100m, 2);
    }

    private Task<OrcamentoMensal?> ObterConfiguracaoMaisRecenteAsync(string userId, CancellationToken cancellationToken) =>
        context.OrcamentosMensais.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Ano)
            .ThenByDescending(item => item.Mes)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<decimal> ObterDespesasAsync(string userId, int ano, int mes, CancellationToken cancellationToken) =>
        ObterTotalTransacoesAsync(userId, ano, mes, TipoTransacao.Despesa, cancellationToken);

    private async Task<decimal> ObterTotalTransacoesAsync(string userId, int ano, int mes, TipoTransacao tipo, CancellationToken cancellationToken)
    {
        var inicioLocal = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var fimLocal = inicioLocal.AddMonths(1);
        var inicioUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(inicioLocal, _timeZone), TimeSpan.Zero);
        var fimUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(fimLocal, _timeZone), TimeSpan.Zero);
        var connection = context.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"Valor\" FROM \"Transacoes\" WHERE \"UserId\" = @userId AND \"Tipo\" = @tipo AND julianday(\"DataCadastro\") >= julianday(@inicio) AND julianday(\"DataCadastro\") < julianday(@fim)";
            if (context.Database.CurrentTransaction is { } transaction)
                command.Transaction = transaction.GetDbTransaction();

            AdicionarParametro(command, "@userId", DbType.String, userId);
            AdicionarParametro(command, "@tipo", DbType.Int32, (int)tipo);
            AdicionarParametro(command, "@inicio", DbType.String, inicioUtc.ToString("O", CultureInfo.InvariantCulture));
            AdicionarParametro(command, "@fim", DbType.String, fimUtc.ToString("O", CultureInfo.InvariantCulture));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var total = 0m;
            while (await reader.ReadAsync(cancellationToken))
                total += Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture);

            return total;
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private static void AdicionarParametro(System.Data.Common.DbCommand command, string nome, DbType tipo, object valor)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = nome;
        parameter.DbType = tipo;
        parameter.Value = valor;
        command.Parameters.Add(parameter);
    }

    private static OrcamentoMensalDto CriarDto(
        int ano, int mes, decimal limite, decimal despesas, bool alertaEnviado, string modo, decimal? percentual,
        bool configuracaoHerdada = false, int? anoOrigem = null, int? mesOrigem = null) =>
        new(ano, mes, limite, despesas, CalcularPercentual(despesas, limite), alertaEnviado, modo, percentual,
            configuracaoHerdada, anoOrigem, mesOrigem);

    private static decimal CalcularPercentual(decimal despesas, decimal limite) =>
        limite <= 0 ? 0 : decimal.Round(despesas / limite * 100m, 1);

    private static void ValidarPeriodo(int ano, int mes)
    {
        if (ano is < 1 or > 9998 || mes is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(mes), "Informe um mês e ano válidos.");
    }
}
