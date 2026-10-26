using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.DTOs;
using WalletFlow.WebApi.Entities;
using WalletFlow.WebApi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Data.Sqlite;
using WalletFlow.WebApi.Enums;
using System.Security.Claims;

namespace WalletFlow.WebApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class TransacaoController : ControllerBase
    {
        private readonly WalletDbContext _context;
        private readonly IFinanceiroService _financeiroService;
        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("A requisição não contém um usuário autenticado.");

        // Injeção de dependência do Banco e do Serviço de Regra de Negócio
        public TransacaoController(WalletDbContext context, IFinanceiroService financeiroService)
        {
            _context = context;
            _financeiroService = financeiroService;
        }

        // 1. POST: api/transacao (Cadastrar uma Movimentação)
        [HttpPost]
        public async Task<ActionResult<TransacaoRespostaDto>> CriarTransacao(
            [FromBody] TransacaoCriacaoDto dto,
            CancellationToken cancellationToken)
        {
            // Mapeamento: DTO de Entrada -> Entidade de Banco
            var novaTransacao = new Transacao
            {
                UserId = CurrentUserId,
                Descricao = dto.Descricao,
                Valor = dto.Valor,
                Tipo = dto.Tipo!.Value,
                DataCadastro = DateTimeOffset.UtcNow // Define a data atual no servidor
            };

            _context.Transacoes.Add(novaTransacao);
            await _context.SaveChangesAsync(cancellationToken);

            // Mapeamento: Entidade de Banco -> DTO de Saída
            var respostaDto = new TransacaoRespostaDto
            {
                Id = novaTransacao.Id,
                Descricao = novaTransacao.Descricao,
                Valor = novaTransacao.Valor,
                TipoFormatado = novaTransacao.Tipo.ToString(),
                DataCadastro = novaTransacao.DataCadastro
            };

            // Retorna o status HTTP 201 (Created)
            return CreatedAtAction(nameof(ObterPorId), new { id = respostaDto.Id }, respostaDto);
        }

        // 2. GET: api/transacao (Listar todo o Extrato)
        [HttpGet]
        public async Task<ActionResult<ResultadoPaginadoDto<TransacaoRespostaDto>>> ObterTodas(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            {
                return BadRequest("Informe page maior que zero e pageSize entre 1 e 100.");
            }

            var userId = CurrentUserId;
            var userTransactions = _context.Transacoes.Where(t => t.UserId == userId);
            var totalCount = await userTransactions.CountAsync(cancellationToken);
            var transacoes = await userTransactions
                .AsNoTracking()
                .OrderByDescending(t => t.Id)
                .Skip((int)((long)(page - 1) * pageSize))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // Mapeando a lista usando LINQ para proteger o payload do banco
            var listaDtos = transacoes.Select(t => new TransacaoRespostaDto
            {
                Id = t.Id,
                Descricao = t.Descricao,
                Valor = t.Valor,
                TipoFormatado = t.Tipo.ToString(),
                DataCadastro = t.DataCadastro
            }).ToList();

            return Ok(new ResultadoPaginadoDto<TransacaoRespostaDto>
            {
                Items = listaDtos,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        // GET: api/transacao/filtro — os filtros são aplicados antes da paginação.
        [HttpGet("filtro")]
        public async Task<ActionResult<ResultadoPaginadoDto<TransacaoRespostaDto>>> ObterFiltrado(
            [FromQuery] TipoTransacao? tipo = null,
            [FromQuery] DateTimeOffset? dataInicio = null,
            [FromQuery] DateTimeOffset? dataFim = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
                return BadRequest("Informe page maior que zero e pageSize entre 1 e 100.");
            if (tipo.HasValue && !Enum.IsDefined(tipo.Value))
                return BadRequest("Tipo deve ser 0 (Despesa) ou 1 (Receita).");
            if (dataInicio.HasValue && dataFim.HasValue && dataInicio > dataFim)
                return BadRequest("dataInicio deve ser anterior ou igual a dataFim.");

            // SQLite armazena DateTimeOffset como texto e não suporta comparações EF nessa coluna.
            // julianday compara os instantes no banco sem materializar o histórico.
            var inicio = dataInicio?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            var fim = dataFim?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            var userId = CurrentUserId;
            var query = _context.Transacoes.FromSqlRaw(
                "SELECT * FROM \"Transacoes\" WHERE (@inicio IS NULL OR julianday(\"DataCadastro\") >= julianday(@inicio)) AND (@fim IS NULL OR julianday(\"DataCadastro\") <= julianday(@fim))",
                new SqliteParameter("@inicio", (object?)inicio ?? DBNull.Value),
                new SqliteParameter("@fim", (object?)fim ?? DBNull.Value)).AsNoTracking()
                .Where(t => t.UserId == userId);
            if (tipo.HasValue)
                query = query.Where(t => t.Tipo == tipo.Value);

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query.OrderByDescending(t => t.Id)
                .Skip((int)((long)(page - 1) * pageSize)).Take(pageSize)
                .Select(t => new TransacaoRespostaDto
                {
                    Id = t.Id,
                    Descricao = t.Descricao,
                    Valor = t.Valor,
                    TipoFormatado = t.Tipo.ToString(),
                    DataCadastro = t.DataCadastro
                }).ToListAsync(cancellationToken);

            return Ok(new ResultadoPaginadoDto<TransacaoRespostaDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        // 3. GET: api/transacao/{id} (Buscar transação por ID único)
        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransacaoRespostaDto>> ObterPorId(int id, CancellationToken cancellationToken)
        {
            var transacao = await _context.Transacoes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId, cancellationToken);

            if (transacao == null)
            {
                return NotFound($"Transação com o ID {id} não foi encontrada.");
            }

            var respostaDto = new TransacaoRespostaDto
            {
                Id = transacao.Id,
                Descricao = transacao.Descricao,
                Valor = transacao.Valor,
                TipoFormatado = transacao.Tipo.ToString(),
                DataCadastro = transacao.DataCadastro
            };

            return Ok(respostaDto);
        }

        // 4. GET: api/transacao/saldo (Calcular saldo consolidado via Service)
        [HttpGet("saldo")]
        public async Task<ActionResult<decimal>> ObterSaldoConsolidado(CancellationToken cancellationToken)
        {
            decimal saldoAtual = await _financeiroService.CalcularSaldoConsolidadoAsync(CurrentUserId, cancellationToken);
            return Ok(new { saldoConsolidado = saldoAtual });
        }
    }
}
