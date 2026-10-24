using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletFlow.WebApi.Data;
using WalletFlow.WebApi.DTOs;
using WalletFlow.WebApi.Entities;
using WalletFlow.WebApi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WalletFlow.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransacaoController : ControllerBase
    {
        private readonly WalletDbContext _context;
        private readonly IFinanceiroService _financeiroService;

        // Injeção de dependência do Banco e do Serviço de Regra de Negócio
        public TransacaoController(WalletDbContext context, IFinanceiroService financeiroService)
        {
            _context = context;
            _financeiroService = financeiroService;
        }

        // 1. POST: api/transacao (Cadastrar uma Movimentação)
        [HttpPost]
        public async Task<ActionResult<TransacaoRespostaDto>> CriarTransacao([FromBody] TransacaoCriacaoDto dto)
        {
            // Mapeamento: DTO de Entrada -> Entidade de Banco
            var novaTransacao = new Transacao
            {
                Descricao = dto.Descricao,
                Valor = dto.Valor,
                Tipo = dto.Tipo!.Value,
                DataCadastro = DateTime.UtcNow // Define a data atual no servidor
            };

            _context.Transacoes.Add(novaTransacao);
            await _context.SaveChangesAsync();

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
        public async Task<ActionResult<IEnumerable<TransacaoRespostaDto>>> ObterTodas()
        {
            var transacoes = await _context.Transacoes.ToListAsync();

            // Mapeando a lista usando LINQ para proteger o payload do banco
            var listaDtos = transacoes.Select(t => new TransacaoRespostaDto
            {
                Id = t.Id,
                Descricao = t.Descricao,
                Valor = t.Valor,
                TipoFormatado = t.Tipo.ToString(),
                DataCadastro = t.DataCadastro
            }).ToList();

            return Ok(listaDtos);
        }

        // 3. GET: api/transacao/{id} (Buscar transação por ID único)
        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransacaoRespostaDto>> ObterPorId(int id)
        {
            var transacao = await _context.Transacoes.FindAsync(id);

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
        public async Task<ActionResult<decimal>> ObterSaldoConsolidado()
        {
            decimal saldoAtual = await _financeiroService.CalcularSaldoConsolidadoAsync();
            return Ok(new { saldoConsolidado = saldoAtual });
        }
    }
}
