using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Globalization;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Entities;
using WalletFlow.Domain.Enums;
using WalletFlow.Infrastructure.Persistence;

namespace WalletFlow.Infrastructure.Repositories;

public sealed class EfOrcamentoMensalService(WalletDbContext context, IConfiguration configuration) : IOrcamentoMensalService
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration["Filters:TimeZoneId"] ?? "America/Sao_Paulo");

    public async Task<OrcamentoMensalDto> ObterAsync(string userId, int ano, int mes, CancellationToken cancellationToken = default)
    {
        ValidarPeriodo(ano, mes);
        var budget = await context.OrcamentosMensais.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Ano == ano && item.Mes == mes, cancellationToken);
        var expenses = await ObterDespesasAsync(userId, ano, mes, cancellationToken);
        return CriarDto(ano, mes, budget?.Limite ?? 0m, expenses, budget?.AlertaOitentaPorcentoEnviadoEmUtc is not null);
    }

    public async Task<OrcamentoMensalDto> SalvarAsync(string userId, int ano, int mes, decimal limite, CancellationToken cancellationToken = default)
    {
        ValidarPeriodo(ano, mes);
        if (limite <= 0 || limite > 999999999.99m || decimal.Round(limite, 2) != limite)
            throw new ArgumentOutOfRangeException(nameof(limite), "O limite deve ser maior que zero e ter até duas casas decimais.");

        var budget = await context.OrcamentosMensais
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Ano == ano && item.Mes == mes, cancellationToken);
        if (budget is null)
        {
            budget = new OrcamentoMensal { UserId = userId, Ano = ano, Mes = mes, Limite = limite };
            context.OrcamentosMensais.Add(budget);
        }
        else if (budget.Limite != limite)
        {
            budget.Limite = limite;
        }

        await context.SaveChangesAsync(cancellationToken);
        var expenses = await ObterDespesasAsync(userId, ano, mes, cancellationToken);
        return CriarDto(ano, mes, budget.Limite, expenses, budget.AlertaOitentaPorcentoEnviadoEmUtc is not null);
    }

    public async Task<AlertaOrcamentoDto?> TentarRegistrarAlertaAsync(string userId, int ano, int mes, CancellationToken cancellationToken = default)
    {
        ValidarPeriodo(ano, mes);
        var budget = await context.OrcamentosMensais.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Ano == ano && item.Mes == mes, cancellationToken);
        if (budget is null)
            return null;

        var expenses = await ObterDespesasAsync(userId, ano, mes, cancellationToken);
        if (expenses < budget.Limite * 0.8m)
            return null;

        var now = DateTimeOffset.UtcNow;
        var claimed = await context.OrcamentosMensais
            .Where(item => item.UserId == userId && item.Ano == ano && item.Mes == mes && item.AlertaOitentaPorcentoEnviadoEmUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AlertaOitentaPorcentoEnviadoEmUtc, now), cancellationToken);
        return claimed == 0
            ? null
            : new AlertaOrcamentoDto(ano, mes, budget.Limite, expenses, CalcularPercentual(expenses, budget.Limite));
    }

    private async Task<decimal> ObterDespesasAsync(string userId, int ano, int mes, CancellationToken cancellationToken)
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
            AdicionarParametro(command, "@tipo", DbType.Int32, (int)TipoTransacao.Despesa);
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

    private static OrcamentoMensalDto CriarDto(int ano, int mes, decimal limite, decimal despesas, bool alertaEnviado) =>
        new(ano, mes, limite, despesas, CalcularPercentual(despesas, limite), alertaEnviado);

    private static decimal CalcularPercentual(decimal despesas, decimal limite) =>
        limite <= 0 ? 0 : decimal.Round(despesas / limite * 100m, 1);

    private static void ValidarPeriodo(int ano, int mes)
    {
        if (ano is < 1 or > 9998 || mes is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(mes), "Informe um mês e ano válidos.");
    }
}
