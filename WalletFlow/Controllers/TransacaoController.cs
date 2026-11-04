using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.DTOs;
using WalletFlow.Domain.Enums;

namespace WalletFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TransacaoController(ITransacaoService transacoes) : ControllerBase
{
    private const long TamanhoMaximoArquivo = 5 * 1024 * 1024;
    private const long TamanhoMaximoRequisicao = TamanhoMaximoArquivo + 64 * 1024;
    private const int MaximoLancamentos = 500;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("A requisição não contém um usuário autenticado.");

    [HttpPost("importacao/previa")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TamanhoMaximoRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = TamanhoMaximoRequisicao)]
    public async Task<ActionResult<ExtratoImportacaoPreviaDto>> AnalisarExtrato(
        IFormFile? arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest("Selecione um arquivo OFX ou CSV não vazio.");
        if (arquivo.Length > TamanhoMaximoArquivo)
            return BadRequest("O arquivo excede o limite de 5 MB.");
        if (!new[] { ".ofx", ".csv" }.Contains(Path.GetExtension(arquivo.FileName), StringComparer.OrdinalIgnoreCase))
            return BadRequest("Formato não suportado. Envie um arquivo .CSV ou .OFX.");

        try
        {
            await using var stream = arquivo.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var conteudo = await reader.ReadToEndAsync(cancellationToken);
            return Ok(await transacoes.AnalisarExtratoAsync(CurrentUserId, Path.GetFileName(arquivo.FileName), conteudo, cancellationToken));
        }
        catch (FormatException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (IOException) { return BadRequest("Não foi possível ler o arquivo enviado."); }
    }

    [HttpPost("importacao")]
    public async Task<ActionResult<ExtratoImportacaoResultadoDto>> ImportarExtrato(
        [FromBody] ExtratoImportacaoConfirmacaoDto dto,
        CancellationToken cancellationToken)
    {
        try { return Ok(await transacoes.ImportarExtratoAsync(CurrentUserId, dto, cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost]
    public async Task<ActionResult<TransacaoRespostaDto>> CriarTransacao(
        [FromBody] TransacaoCriacaoDto dto,
        CancellationToken cancellationToken)
    {
        var criada = await transacoes.CriarAsync(CurrentUserId, dto, cancellationToken);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    [HttpGet]
    public async Task<ActionResult<ResultadoPaginadoDto<TransacaoRespostaDto>>> ObterTodas(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!PaginacaoValida(page, pageSize))
            return BadRequest("Informe page maior que zero e pageSize entre 1 e 100.");
        return Ok(await transacoes.ListarAsync(CurrentUserId, page, pageSize, cancellationToken));
    }

    [HttpGet("filtro")]
    public async Task<ActionResult<ResultadoPaginadoDto<TransacaoRespostaDto>>> ObterFiltrado(
        [FromQuery] TipoTransacao? tipo = null,
        [FromQuery] DateTimeOffset? dataInicio = null,
        [FromQuery] DateTimeOffset? dataFim = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!PaginacaoValida(page, pageSize))
            return BadRequest("Informe page maior que zero e pageSize entre 1 e 100.");
        if (tipo.HasValue && !Enum.IsDefined(tipo.Value))
            return BadRequest("Tipo deve ser 0 (Despesa) ou 1 (Receita).");
        if (dataInicio.HasValue && dataFim.HasValue && dataInicio > dataFim)
            return BadRequest("dataInicio deve ser anterior ou igual a dataFim.");
        return Ok(await transacoes.FiltrarAsync(CurrentUserId, tipo, dataInicio, dataFim, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransacaoRespostaDto>> ObterPorId(int id, CancellationToken cancellationToken)
    {
        var item = await transacoes.ObterAsync(CurrentUserId, id, cancellationToken);
        return item is null ? NotFound($"Transação com o ID {id} não foi encontrada.") : Ok(item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> AtualizarTransacao(int id, [FromBody] TransacaoCriacaoDto dto, CancellationToken cancellationToken)
    {
        var atualizada = await transacoes.AtualizarAsync(CurrentUserId, id, dto, cancellationToken);
        return atualizada ? NoContent() : NotFound($"Transação com o ID {id} não foi encontrada.");
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> ExcluirTransacao(int id, CancellationToken cancellationToken)
    {
        var excluida = await transacoes.ExcluirAsync(CurrentUserId, id, cancellationToken);
        return excluida ? NoContent() : NotFound($"Transação com o ID {id} não foi encontrada.");
    }

    [HttpGet("saldo")]
    public async Task<ActionResult<decimal>> ObterSaldoConsolidado(CancellationToken cancellationToken) =>
        Ok(new { saldoConsolidado = await transacoes.ObterSaldoAsync(CurrentUserId, cancellationToken) });

    [HttpGet("evolucao-mensal")]
    public async Task<ActionResult<EvolucaoFinanceiraDto>> ObterEvolucaoMensal(
        [FromQuery] int ano,
        [FromQuery] int mes,
        CancellationToken cancellationToken)
    {
        if (mes is < 1 or > 12 || ano is < 1 or > 9998)
            return BadRequest("Informe um mês e ano válidos.");

        return Ok(await transacoes.ObterEvolucaoMensalAsync(CurrentUserId, ano, mes, cancellationToken));
    }

    private static bool PaginacaoValida(int page, int pageSize) =>
        page >= 1 && pageSize is >= 1 and <= 100 && (long)(page - 1) * pageSize <= int.MaxValue;
}
