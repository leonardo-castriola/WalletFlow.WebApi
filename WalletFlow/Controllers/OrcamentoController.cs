using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletFlow.Application.Abstractions;
using WalletFlow.Application.DTOs;

namespace WalletFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class OrcamentoController(IOrcamentoMensalService orcamentos) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("A requisição não contém um usuário autenticado.");

    [HttpGet]
    public async Task<ActionResult<OrcamentoMensalDto>> Obter(
        [FromQuery] int ano,
        [FromQuery] int mes,
        CancellationToken cancellationToken)
    {
        if (!PeriodoValido(ano, mes))
            return BadRequest("Informe um mês e ano válidos.");
        return Ok(await orcamentos.ObterAsync(CurrentUserId, ano, mes, cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<OrcamentoMensalDto>> Salvar(
        [FromQuery] int ano,
        [FromQuery] int mes,
        [FromBody] AtualizarOrcamentoMensalDto dto,
        CancellationToken cancellationToken)
    {
        if (!PeriodoValido(ano, mes))
            return BadRequest("Informe um mês e ano válidos.");
        try
        {
            return Ok(await orcamentos.SalvarAsync(CurrentUserId, ano, mes, dto.Limite, cancellationToken));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("verificar-alerta")]
    public async Task<ActionResult<AlertaOrcamentoDto?>> VerificarAlerta(
        [FromQuery] int ano,
        [FromQuery] int mes,
        CancellationToken cancellationToken)
    {
        if (!PeriodoValido(ano, mes))
            return BadRequest("Informe um mês e ano válidos.");
        var alert = await orcamentos.TentarRegistrarAlertaAsync(CurrentUserId, ano, mes, cancellationToken);
        return alert is null ? NoContent() : Ok(alert);
    }

    private static bool PeriodoValido(int ano, int mes) => ano is >= 1 and <= 9998 && mes is >= 1 and <= 12;
}
