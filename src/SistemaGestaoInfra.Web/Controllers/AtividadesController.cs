using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web.Models;
using SistemaGestaoInfra.Web.Services;

namespace SistemaGestaoInfra.Web.Controllers;

/// <summary>
/// Atividades (demandas). Qualquer usuário autenticado pode registrar e consultar;
/// o isolamento por prefeitura é garantido pelo filtro global do DbContext.
/// </summary>
public partial class AtividadesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public AtividadesController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> Index([FromQuery] AtividadeFiltro filtro, CancellationToken ct)
    {
        if (filtro.Pagina < 1)
        {
            filtro.Pagina = 1;
        }

        var query = _db.Atividades.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var termo = $"%{filtro.Busca.Trim()}%";
            query = query.Where(a =>
                EF.Functions.ILike(a.Descricao, termo)
                || (a.Rua != null && EF.Functions.ILike(a.Rua, termo))
                || (a.Bairro != null && EF.Functions.ILike(a.Bairro, termo)));
        }

        if (filtro.StatusId.HasValue)
        {
            query = query.Where(a => a.StatusId == filtro.StatusId);
        }

        if (filtro.TipoAtividadeId.HasValue)
        {
            query = query.Where(a => a.TipoAtividadeId == filtro.TipoAtividadeId);
        }

        if (filtro.PrioridadeId.HasValue)
        {
            query = query.Where(a => a.PrioridadeId == filtro.PrioridadeId);
        }

        if (filtro.De.HasValue)
        {
            var de = DateTime.SpecifyKind(filtro.De.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.CriadoEm >= de);
        }

        if (filtro.Ate.HasValue)
        {
            // Inclui o dia inteiro informado em "Até".
            var ate = DateTime.SpecifyKind(filtro.Ate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.CriadoEm < ate);
        }

        var total = await query.CountAsync(ct);

        var itens = await query
            .OrderByDescending(a => a.CriadoEm)
            .Skip((filtro.Pagina - 1) * AtividadeFiltro.TamanhoPagina)
            .Take(AtividadeFiltro.TamanhoPagina)
            .Select(a => new AtividadeListaItem(
                a.Id,
                a.Descricao,
                a.TipoAtividade!.Codigo,
                a.Setor != null ? a.Setor.Codigo : null,
                a.Status!.Descricao,
                a.Status.Cor,
                a.Prioridade!.Codigo,
                a.Progresso,
                a.FotoPath != null,
                a.CriadoEm))
            .ToListAsync(ct);

        var model = new AtividadeListaViewModel
        {
            Filtro = filtro,
            Itens = itens,
            Total = total,
            Status = await _db.StatusAtividades.AsNoTracking().OrderBy(s => s.Id)
                .Select(s => new SelectListItem(s.Descricao, s.Id.ToString())).ToListAsync(ct),
            Tipos = await _db.TiposAtividade.AsNoTracking().OrderBy(t => t.Codigo)
                .Select(t => new SelectListItem(t.Codigo + " — " + t.Descricao, t.Id.ToString())).ToListAsync(ct),
            Prioridades = await _db.Prioridades.AsNoTracking().OrderBy(p => p.Ordem)
                .Select(p => new SelectListItem(p.Descricao, p.Id.ToString())).ToListAsync(ct)
        };

        ViewData["Title"] = "Atividades";
        return View(model);
    }

    public async Task<IActionResult> Detalhe(int id, CancellationToken ct)
    {
        var a = await _db.Atividades
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Descricao, Tipo = x.TipoAtividade!.Codigo + " — " + x.TipoAtividade.Descricao,
                Setor = x.Setor != null ? x.Setor.Codigo : null,
                Status = x.Status!.Descricao, CorStatus = x.Status.Cor, StatusCodigo = x.Status.Codigo,
                Prioridade = x.Prioridade!.Descricao,
                x.Progresso, x.Rua, x.Numero, x.Bairro, x.Cep, x.Latitude, x.Longitude,
                TemFoto = x.FotoPath != null, x.Requerimento, x.NumeroRequerimento,
                TemArquivoReq = x.ArquivoRequerimentoPath != null,
                x.UsuarioAberturaId, x.CriadoEm,
                x.DataInicioPrevisto, x.DataFimPrevisto, x.DataInicioReal, x.DataConclusao, x.MotivoNegativa
            })
            .FirstOrDefaultAsync(ct);

        if (a is null)
        {
            return NotFound();
        }

        string? abertoPor = null;
        if (a.UsuarioAberturaId is not null)
        {
            abertoPor = await _db.Users.AsNoTracking()
                .Where(u => u.Id == a.UsuarioAberturaId)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(ct);
        }

        var partesEndereco = new[]
        {
            string.Join(", ", new[] { a.Rua, a.Numero }.Where(p => !string.IsNullOrWhiteSpace(p))),
            a.Bairro,
            a.Cep
        }.Where(p => !string.IsNullOrWhiteSpace(p));

        var servicos = await _db.AtividadeServicos.AsNoTracking()
            .Where(s => s.AtividadeId == id)
            .OrderBy(s => s.Id)
            .Select(s => new AtividadeServicoLinha(
                s.Id,
                s.ContratoServico!.Contrato!.Codigo,
                s.ContratoServico.TipoServico!.Codigo,
                s.ContratoServico.TipoMedicao!.Codigo,
                s.Quantidade, s.ValorUnitario))
            .ToListAsync(ct);

        var opcoes = new List<SelectListItem>();
        var podeLancar = (User.IsInRole("Admin") || User.IsInRole("Gestor")) && a.StatusCodigo != StatusCodigos.Negada;
        if (podeLancar)
        {
            var hojeD = DateOnly.FromDateTime(DateTime.Now);
            var executado = await ConsumoContratos.QuantidadePorItemAsync(_db, ct);
            var itens = await _db.ContratoServicos.AsNoTracking()
                .Where(i => i.Contrato!.Ativo && i.Contrato.DataInicio <= hojeD && i.Contrato.DataFim >= hojeD)
                .OrderBy(i => i.Contrato!.Codigo).ThenBy(i => i.TipoServico!.Codigo)
                .Select(i => new
                {
                    i.Id, Contrato = i.Contrato!.Codigo, Servico = i.TipoServico!.Codigo,
                    Medicao = i.TipoMedicao!.Codigo, i.Quantidade, i.Limite, i.ValorUnitario
                })
                .ToListAsync(ct);

            foreach (var i in itens)
            {
                var saldo = (i.Limite ?? i.Quantidade) - executado.GetValueOrDefault(i.Id);
                if (saldo > 0)
                {
                    opcoes.Add(new SelectListItem(
                        $"{i.Contrato} · {i.Servico} ({i.Medicao}) · saldo {saldo:0.####} · {i.ValorUnitario:C2}", i.Id.ToString()));
                }
            }
        }

        ViewData["Title"] = $"Atividade #{a.Id}";
        return View(new AtividadeDetalheViewModel
        {
            Id = a.Id,
            Descricao = a.Descricao,
            Tipo = a.Tipo,
            Setor = a.Setor,
            Status = a.Status,
            CorStatus = a.CorStatus,
            Prioridade = a.Prioridade,
            Progresso = a.Progresso,
            Endereco = string.Join(" — ", partesEndereco) is { Length: > 0 } e ? e : null,
            Latitude = a.Latitude,
            Longitude = a.Longitude,
            TemFoto = a.TemFoto,
            Requerimento = a.Requerimento,
            NumeroRequerimento = a.NumeroRequerimento,
            TemArquivoRequerimento = a.TemArquivoReq,
            AbertoPor = abertoPor,
            CriadoEm = a.CriadoEm,
            StatusCodigo = a.StatusCodigo,
            DataInicioPrevisto = a.DataInicioPrevisto,
            DataFimPrevisto = a.DataFimPrevisto,
            DataInicioReal = a.DataInicioReal,
            DataConclusao = a.DataConclusao,
            MotivoNegativa = a.MotivoNegativa,
            Servicos = servicos,
            OpcoesServico = opcoes
        });
    }
    [HttpGet]
    public async Task<IActionResult> Nova(CancellationToken ct)
    {
        var model = new AtividadeFormViewModel();
        await CarregarListasAsync(model, ct);
        ViewData["Title"] = "Nova atividade";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Nova(AtividadeFormViewModel model, CancellationToken ct)
    {
        if (model.Requerimento && string.IsNullOrWhiteSpace(model.NumeroRequerimento))
        {
            ModelState.AddModelError(nameof(model.NumeroRequerimento), "Informe o número do requerimento.");
        }

        if (model.Latitude.HasValue != model.Longitude.HasValue)
        {
            ModelState.AddModelError(nameof(model.Latitude), "Informe latitude e longitude juntas, ou deixe ambas em branco.");
        }

        // Tipo, prioridade e status precisam existir e estar ativos NA prefeitura do usuário
        // (o filtro global do DbContext garante o escopo do tenant).
        var tipo = model.TipoAtividadeId is int tipoId
            ? await _db.TiposAtividade.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tipoId && t.Ativo, ct)
            : null;
        if (model.TipoAtividadeId.HasValue && tipo is null)
        {
            ModelState.AddModelError(nameof(model.TipoAtividadeId), "Tipo de atividade inválido.");
        }

        var prioridadeValida = model.PrioridadeId is int prioId
            && await _db.Prioridades.AnyAsync(p => p.Id == prioId && p.Ativo, ct);
        if (model.PrioridadeId.HasValue && !prioridadeValida)
        {
            ModelState.AddModelError(nameof(model.PrioridadeId), "Prioridade inválida.");
        }

        if (model.Foto is { Length: > 0 })
        {
            var erro = await UploadValidator.ValidarFotoAsync(model.Foto, ct);
            if (erro is not null)
            {
                ModelState.AddModelError(nameof(model.Foto), erro);
            }
        }

        if (model.Requerimento && model.ArquivoRequerimento is { Length: > 0 })
        {
            var erro = await UploadValidator.ValidarRequerimentoAsync(model.ArquivoRequerimento, ct);
            if (erro is not null)
            {
                ModelState.AddModelError(nameof(model.ArquivoRequerimento), erro);
            }
        }

        var statusAberta = await _db.StatusAtividades
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Codigo == StatusCodigos.Aberta, ct);
        if (statusAberta is null)
        {
            ModelState.AddModelError(string.Empty, "O status padrão ABERTA não está configurado. Fale com o administrador.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarListasAsync(model, ct);
            ViewData["Title"] = "Nova atividade";
            return View(model);
        }

        string? fotoPath = null;
        string? reqPath = null;

        try
        {
            if (model.Foto is { Length: > 0 })
            {
                await using var s = model.Foto.OpenReadStream();
                fotoPath = await _storage.SalvarAsync(s, model.Foto.FileName, "atividades", ct);
            }

            if (model.Requerimento && model.ArquivoRequerimento is { Length: > 0 })
            {
                await using var s = model.ArquivoRequerimento.OpenReadStream();
                reqPath = await _storage.SalvarAsync(s, model.ArquivoRequerimento.FileName, "requerimentos", ct);
            }

            var atividade = new Atividade
            {
                Descricao = model.Descricao.Trim(),
                TipoAtividadeId = tipo!.Id,
                SetorId = tipo.SetorId,
                PrioridadeId = model.PrioridadeId!.Value,
                StatusId = statusAberta!.Id,
                Rua = model.Rua?.Trim(),
                Numero = model.Numero?.Trim(),
                Bairro = model.Bairro?.Trim(),
                Cep = model.Cep?.Trim(),
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                FotoPath = fotoPath,
                Requerimento = model.Requerimento,
                NumeroRequerimento = model.Requerimento ? model.NumeroRequerimento?.Trim() : null,
                ArquivoRequerimentoPath = reqPath,
                UsuarioAberturaId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };

            _db.Atividades.Add(atividade);
            await _db.SaveChangesAsync(ct);

            TempData["Sucesso"] = $"Atividade #{atividade.Id} registrada com sucesso.";
            return RedirectToAction(nameof(Detalhe), new { id = atividade.Id });
        }
        catch
        {
            // Não deixa arquivos órfãos se a gravação no banco falhar.
            if (fotoPath is not null) await _storage.RemoverAsync(fotoPath, CancellationToken.None);
            if (reqPath is not null) await _storage.RemoverAsync(reqPath, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Foto de evidência: só para usuários autenticados da mesma prefeitura.</summary>
    [HttpGet]
    public Task<IActionResult> Foto(int id, CancellationToken ct) =>
        ServirArquivoAsync(id, a => a.FotoPath, ct);

    [HttpGet]
    public Task<IActionResult> Requerimento(int id, CancellationToken ct) =>
        ServirArquivoAsync(id, a => a.ArquivoRequerimentoPath, ct);

    private async Task<IActionResult> ServirArquivoAsync(
        int id, System.Linq.Expressions.Expression<Func<Atividade, string?>> seletor, CancellationToken ct)
    {
        // A consulta passa pelo filtro multi-tenant: atividade de outra prefeitura => 404.
        var caminho = await _db.Atividades.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(seletor)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(caminho))
        {
            return NotFound();
        }

        var stream = await _storage.AbrirAsync(caminho, ct);
        if (stream is null)
        {
            return NotFound();
        }

        // O tipo é derivado da extensão que NÓS gravamos (nunca do que o cliente enviou).
        var contentType = Path.GetExtension(caminho).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, max-age=600";
        return File(stream, contentType);
    }

    private async Task CarregarListasAsync(AtividadeFormViewModel model, CancellationToken ct)
    {
        model.Tipos = await _db.TiposAtividade.AsNoTracking()
            .Where(t => t.Ativo)
            .OrderBy(t => t.Codigo)
            .Select(t => new SelectListItem(t.Codigo + " — " + t.Descricao, t.Id.ToString()))
            .ToListAsync(ct);

        model.Prioridades = await _db.Prioridades.AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Ordem)
            .Select(p => new SelectListItem(p.Codigo + " — " + p.Descricao, p.Id.ToString()))
            .ToListAsync(ct);
    }
}
