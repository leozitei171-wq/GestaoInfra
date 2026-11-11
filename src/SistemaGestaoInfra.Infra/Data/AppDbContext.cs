using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Domain.Common;
using SistemaGestaoInfra.Domain.Entities;

namespace SistemaGestaoInfra.Infra.Data;

/// <summary>
/// Contexto de dados da aplicação. Inclui as tabelas do ASP.NET Identity e
/// aplica filtro global de isolamento por prefeitura (multi-tenant).
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantProvider? _tenantProvider;
    private readonly IAuditContext? _auditContext;
    private bool _gravandoAuditoria;

    // Os dois serviços são opcionais: ferramentas de design-time (migrations) criam o
    // contexto sem eles. Em execução normal, o container de DI sempre os fornece.
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantProvider? tenantProvider = null,
        IAuditContext? auditContext = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _auditContext = auditContext;
    }

    public DbSet<Prefeitura> Prefeituras => Set<Prefeitura>();
    public DbSet<Setor> Setores => Set<Setor>();
    public DbSet<TipoAtividade> TiposAtividade => Set<TipoAtividade>();
    public DbSet<Prioridade> Prioridades => Set<Prioridade>();
    public DbSet<StatusAtividade> StatusAtividades => Set<StatusAtividade>();
    public DbSet<Atividade> Atividades => Set<Atividade>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<TipoMedicao> TiposMedicao => Set<TipoMedicao>();
    public DbSet<TipoServico> TiposServico => Set<TipoServico>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<ContratoServico> ContratoServicos => Set<ContratoServico>();
    public DbSet<AtividadeServico> AtividadeServicos => Set<AtividadeServico>();
    public DbSet<ContratoAlerta> ContratoAlertas => Set<ContratoAlerta>();
    public DbSet<TipoCusto> TiposCusto => Set<TipoCusto>();
    public DbSet<Rateio> Rateios => Set<Rateio>();
    public DbSet<RateioItem> RateioItens => Set<RateioItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigurarRateios(builder);
        ConfigurarContratos(builder);
        ConfigurarAuditoria(builder);
        ConfigurarIdentity(builder);
        ConfigurarPrefeitura(builder);
        ConfigurarSetor(builder);
        ConfigurarTipoAtividade(builder);
        ConfigurarPrioridade(builder);
        ConfigurarStatusAtividade(builder);
        ConfigurarAtividade(builder);

        AplicarFiltroMultiTenant(builder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarAuditoriaEtenant();

        var pendentes = _gravandoAuditoria ? [] : AuditoriaColetor.Coletar(ChangeTracker);
        if (pendentes.Count == 0)
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        // Alteração e auditoria na mesma transação: ou gravam as duas, ou nenhuma.
        using var transacao = Database.CurrentTransaction is null ? Database.BeginTransaction() : null;
        var linhas = base.SaveChanges(acceptAllChangesOnSuccess);

        _gravandoAuditoria = true;
        try
        {
            Auditorias.AddRange(pendentes.Select(CriarRegistroAuditoria));
            base.SaveChanges(acceptAllChangesOnSuccess);
        }
        finally
        {
            _gravandoAuditoria = false;
        }

        transacao?.Commit();
        return linhas;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarAuditoriaEtenant();

        var pendentes = _gravandoAuditoria ? [] : AuditoriaColetor.Coletar(ChangeTracker);
        if (pendentes.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        await using var transacao = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;
        var linhas = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        _gravandoAuditoria = true;
        try
        {
            Auditorias.AddRange(pendentes.Select(CriarRegistroAuditoria));
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        finally
        {
            _gravandoAuditoria = false;
        }

        if (transacao is not null)
        {
            await transacao.CommitAsync(cancellationToken);
        }

        return linhas;
    }

    /// <summary>
    /// Monta o registro após o primeiro save, quando os Ids gerados pelo banco já existem.
    /// </summary>
    private Auditoria CriarRegistroAuditoria(AuditoriaPendente p) => new()
    {
        QuandoEm = DateTime.UtcNow,
        PrefeituraId = AuditoriaColetor.PrefeituraDe(p.Entry),
        UsuarioId = _auditContext?.UsuarioId,
        UsuarioNome = _auditContext?.UsuarioNome,
        Acao = p.Acao,
        Entidade = p.Entidade,
        EntidadeId = p.ChaveAntes ?? AuditoriaColetor.Chave(p.Entry),
        Modo = _auditContext?.Modo ?? "sistema",
        Ip = _auditContext?.Ip,
        Payload = p.Payload
    };

    /// <summary>
    /// Preenche timestamps de auditoria e atribui a prefeitura do contexto em
    /// entidades novas que ainda não tenham tenant definido.
    /// </summary>
    private void AplicarAuditoriaEtenant()
    {
        var agora = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntidadeBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CriadoEm = agora;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.AtualizadoEm = agora;
            }
        }

        var prefeituraAtual = _tenantProvider?.PrefeituraId;
        if (prefeituraAtual is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<EntidadeTenant>())
        {
            if (entry.State == EntityState.Added && entry.Entity.PrefeituraId == 0)
            {
                entry.Entity.PrefeituraId = prefeituraAtual.Value;
            }
        }
    }

    /// <summary>
    /// Aplica query filter global em todas as entidades que herdam de EntidadeTenant.
    /// O filtro é avaliado por requisição, lendo o tenant do ITenantProvider.
    /// </summary>
    private void AplicarFiltroMultiTenant(ModelBuilder builder)
    {
        builder.Entity<Setor>()
            .HasQueryFilter(e => _tenantProvider == null
                || _tenantProvider.IgnorarFiltro
                || e.PrefeituraId == _tenantProvider.PrefeituraId);

        builder.Entity<TipoAtividade>()
            .HasQueryFilter(e => _tenantProvider == null
                || _tenantProvider.IgnorarFiltro
                || e.PrefeituraId == _tenantProvider.PrefeituraId);

        builder.Entity<Prioridade>()
            .HasQueryFilter(e => _tenantProvider == null
                || _tenantProvider.IgnorarFiltro
                || e.PrefeituraId == _tenantProvider.PrefeituraId);

        builder.Entity<StatusAtividade>()
            .HasQueryFilter(e => _tenantProvider == null
                || _tenantProvider.IgnorarFiltro
                || e.PrefeituraId == _tenantProvider.PrefeituraId);

        builder.Entity<Atividade>()
            .HasQueryFilter(e => _tenantProvider == null
                || _tenantProvider.IgnorarFiltro
                || e.PrefeituraId == _tenantProvider.PrefeituraId);
    }

    private void ConfigurarRateios(ModelBuilder builder)
    {
        builder.Entity<TipoCusto>(e =>
        {
            e.ToTable("tipos_custo");
            e.Property(t => t.Codigo).HasMaxLength(30).IsRequired();
            e.Property(t => t.Descricao).HasMaxLength(255).IsRequired();
            e.HasIndex(t => new { t.PrefeituraId, t.Codigo }).IsUnique();
        });

        builder.Entity<Rateio>(e =>
        {
            e.ToTable("rateios");
            e.Property(r => r.Descricao).HasMaxLength(255).IsRequired();
            e.Property(r => r.ValorTotal).HasPrecision(18, 2);
            e.Property(r => r.Modo).HasConversion<int>();
            e.Property(r => r.CriadoPorId).HasMaxLength(450);
            e.HasIndex(r => new { r.PrefeituraId, r.DataRateio });

            // Um tipo de custo em uso não pode ser apagado: o histórico financeiro depende dele.
            e.HasOne(r => r.TipoCusto).WithMany().HasForeignKey(r => r.TipoCustoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RateioItem>(e =>
        {
            e.ToTable("rateio_itens");
            e.Property(i => i.Base).HasPrecision(18, 4);
            e.Property(i => i.Percentual).HasPrecision(9, 4);
            e.Property(i => i.Valor).HasPrecision(18, 2);
            e.HasIndex(i => i.RateioId);
            e.HasIndex(i => i.ContratoId);
            e.HasIndex(i => i.AtividadeId);

            e.HasOne(i => i.Rateio).WithMany(r => r.Itens).HasForeignKey(i => i.RateioId).OnDelete(DeleteBehavior.Cascade);
            // Contratos e atividades com rateio não podem ser apagados (Restrict preserva o histórico).
            e.HasOne(i => i.Contrato).WithMany().HasForeignKey(i => i.ContratoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Atividade).WithMany().HasForeignKey(i => i.AtividadeId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TipoCusto>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<Rateio>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<RateioItem>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
    }

    private void ConfigurarContratos(ModelBuilder builder)
    {
        builder.Entity<Fornecedor>(e =>
        {
            e.ToTable("fornecedores");
            e.Property(f => f.RazaoSocial).HasMaxLength(255).IsRequired();
            e.Property(f => f.Cnpj).HasMaxLength(14).IsRequired();
            e.Property(f => f.Contato).HasMaxLength(255);
            e.HasIndex(f => new { f.PrefeituraId, f.Cnpj }).IsUnique();
        });

        builder.Entity<TipoMedicao>(e =>
        {
            e.ToTable("tipos_medicao");
            e.Property(t => t.Codigo).HasMaxLength(20).IsRequired();
            e.Property(t => t.Descricao).HasMaxLength(120).IsRequired();
            e.HasIndex(t => new { t.PrefeituraId, t.Codigo }).IsUnique();
        });

        builder.Entity<TipoServico>(e =>
        {
            e.ToTable("tipos_servico");
            e.Property(t => t.Codigo).HasMaxLength(40).IsRequired();
            e.Property(t => t.Descricao).HasMaxLength(255).IsRequired();
            e.HasIndex(t => new { t.PrefeituraId, t.Codigo }).IsUnique();
        });

        builder.Entity<Contrato>(e =>
        {
            e.ToTable("contratos");
            e.Property(c => c.Codigo).HasMaxLength(60).IsRequired();
            e.Property(c => c.Descricao).HasMaxLength(500).IsRequired();
            e.Property(c => c.ValorTotal).HasPrecision(18, 2);
            e.Property(c => c.ArquivoPdfPath).HasMaxLength(500);
            e.HasIndex(c => new { c.PrefeituraId, c.Codigo }).IsUnique();
            e.HasIndex(c => c.DataFim);

            e.HasOne(c => c.Fornecedor).WithMany().HasForeignKey(c => c.FornecedorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Setor).WithMany().HasForeignKey(c => c.SetorId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ContratoServico>(e =>
        {
            e.ToTable("contrato_servicos");
            e.Property(i => i.Quantidade).HasPrecision(18, 4);
            e.Property(i => i.ValorUnitario).HasPrecision(18, 4);
            e.Property(i => i.Limite).HasPrecision(18, 4);
            e.HasIndex(i => new { i.ContratoId, i.TipoServicoId }).IsUnique();

            e.HasOne(i => i.Contrato).WithMany(c => c.Servicos).HasForeignKey(i => i.ContratoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.TipoServico).WithMany().HasForeignKey(i => i.TipoServicoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.TipoMedicao).WithMany().HasForeignKey(i => i.TipoMedicaoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AtividadeServico>(e =>
        {
            e.ToTable("atividade_servicos");
            e.Property(i => i.Quantidade).HasPrecision(18, 4);
            e.Property(i => i.ValorUnitario).HasPrecision(18, 4);
            e.HasIndex(i => i.AtividadeId);
            e.HasIndex(i => i.ContratoServicoId);

            e.HasOne(i => i.Atividade).WithMany().HasForeignKey(i => i.AtividadeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.ContratoServico).WithMany().HasForeignKey(i => i.ContratoServicoId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContratoAlerta>(e =>
        {
            e.ToTable("contrato_alertas");
            e.Property(a => a.Nivel).HasMaxLength(20).IsRequired();
            e.Property(a => a.PercentualUso).HasPrecision(9, 2);
            e.Property(a => a.Mensagem).HasMaxLength(500).IsRequired();
            e.HasIndex(a => new { a.PrefeituraId, a.Lido });

            e.HasOne(a => a.Contrato).WithMany().HasForeignKey(a => a.ContratoId).OnDelete(DeleteBehavior.Cascade);
        });

        // Todas pertencem a uma prefeitura: mesmo isolamento das demais entidades.
        builder.Entity<Fornecedor>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<TipoMedicao>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<TipoServico>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<Contrato>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<ContratoServico>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<AtividadeServico>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
        builder.Entity<ContratoAlerta>().HasQueryFilter(e => _tenantProvider == null || _tenantProvider.IgnorarFiltro || e.PrefeituraId == _tenantProvider.PrefeituraId);
    }

    private void ConfigurarAuditoria(ModelBuilder builder)
    {
        builder.Entity<Auditoria>(e =>
        {
            e.ToTable("auditoria");
            e.HasKey(a => a.Id);
            e.Property(a => a.UsuarioId).HasMaxLength(450);
            e.Property(a => a.UsuarioNome).HasMaxLength(255);
            e.Property(a => a.Acao).HasMaxLength(60).IsRequired();
            e.Property(a => a.Entidade).HasMaxLength(100).IsRequired();
            e.Property(a => a.EntidadeId).HasMaxLength(100);
            e.Property(a => a.Modo).HasMaxLength(20).IsRequired();
            e.Property(a => a.Ip).HasMaxLength(64);

            e.HasIndex(a => new { a.PrefeituraId, a.QuandoEm });
            e.HasIndex(a => a.Entidade);

            // Gestor só enxerga a auditoria da própria prefeitura; admin (IgnorarFiltro) vê tudo,
            // incluindo eventos sem prefeitura (ex.: tentativa de login de usuário inexistente).
            e.HasQueryFilter(a => _tenantProvider == null
                || _tenantProvider.IgnorarFiltro
                || a.PrefeituraId == _tenantProvider.PrefeituraId);
        });
    }

    private static void ConfigurarIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("usuarios");
            e.Property(u => u.NomeCompleto).HasMaxLength(255).IsRequired();
            e.Property(u => u.Perfil).HasConversion<int>();

            e.HasOne(u => u.Prefeitura)
                .WithMany()
                .HasForeignKey(u => u.PrefeituraId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarPrefeitura(ModelBuilder builder)
    {
        builder.Entity<Prefeitura>(e =>
        {
            e.ToTable("prefeituras");
            e.Property(p => p.Nome).HasMaxLength(255).IsRequired();
            e.Property(p => p.Cidade).HasMaxLength(255).IsRequired();
            e.Property(p => p.Uf).HasMaxLength(2).IsRequired();
        });
    }

    private static void ConfigurarSetor(ModelBuilder builder)
    {
        builder.Entity<Setor>(e =>
        {
            e.ToTable("setores");
            e.Property(s => s.Codigo).HasMaxLength(60).IsRequired();
            e.Property(s => s.Descricao).HasMaxLength(255).IsRequired();

            e.HasIndex(s => new { s.PrefeituraId, s.Codigo }).IsUnique();

            e.HasOne(s => s.Prefeitura)
                .WithMany(p => p.Setores)
                .HasForeignKey(s => s.PrefeituraId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarTipoAtividade(ModelBuilder builder)
    {
        builder.Entity<TipoAtividade>(e =>
        {
            e.ToTable("tipos_atividade");
            e.Property(t => t.Codigo).HasMaxLength(40).IsRequired();
            e.Property(t => t.Descricao).HasMaxLength(255).IsRequired();

            e.HasIndex(t => new { t.PrefeituraId, t.Codigo }).IsUnique();

            e.HasOne(t => t.Prefeitura)
                .WithMany()
                .HasForeignKey(t => t.PrefeituraId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(t => t.Setor)
                .WithMany(s => s.TiposAtividade)
                .HasForeignKey(t => t.SetorId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigurarPrioridade(ModelBuilder builder)
    {
        builder.Entity<Prioridade>(e =>
        {
            e.ToTable("prioridades");
            e.Property(p => p.Codigo).HasMaxLength(20).IsRequired();
            e.Property(p => p.Descricao).HasMaxLength(100).IsRequired();

            e.HasIndex(p => new { p.PrefeituraId, p.Codigo }).IsUnique();

            e.HasOne(p => p.Prefeitura)
                .WithMany()
                .HasForeignKey(p => p.PrefeituraId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarStatusAtividade(ModelBuilder builder)
    {
        builder.Entity<StatusAtividade>(e =>
        {
            e.ToTable("status_atividade");
            e.Property(s => s.Codigo).HasMaxLength(20).IsRequired();
            e.Property(s => s.Descricao).HasMaxLength(100).IsRequired();
            e.Property(s => s.Cor).HasMaxLength(9).IsRequired();

            e.HasIndex(s => new { s.PrefeituraId, s.Codigo }).IsUnique();

            e.HasOne(s => s.Prefeitura)
                .WithMany()
                .HasForeignKey(s => s.PrefeituraId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarAtividade(ModelBuilder builder)
    {
        builder.Entity<Atividade>(e =>
        {
            e.ToTable("atividades");
            e.Property(a => a.Descricao).HasMaxLength(2000).IsRequired();
            e.Property(a => a.Rua).HasMaxLength(255);
            e.Property(a => a.Numero).HasMaxLength(20);
            e.Property(a => a.Bairro).HasMaxLength(255);
            e.Property(a => a.Cep).HasMaxLength(9);
            e.Property(a => a.FotoPath).HasMaxLength(500);
            e.Property(a => a.NumeroRequerimento).HasMaxLength(60);
            e.Property(a => a.ArquivoRequerimentoPath).HasMaxLength(500);
            e.Property(a => a.MotivoNegativa).HasMaxLength(500);

            e.Property(a => a.Latitude).HasPrecision(10, 7);
            e.Property(a => a.Longitude).HasPrecision(10, 7);

            e.HasIndex(a => new { a.PrefeituraId, a.StatusId });
            e.HasIndex(a => new { a.StatusId, a.DataFimPrevisto });
            e.HasIndex(a => new { a.PrefeituraId, a.CriadoEm });

            e.HasOne(a => a.Prefeitura)
                .WithMany(p => p.Atividades)
                .HasForeignKey(a => a.PrefeituraId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.TipoAtividade)
                .WithMany()
                .HasForeignKey(a => a.TipoAtividadeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Setor)
                .WithMany()
                .HasForeignKey(a => a.SetorId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(a => a.Prioridade)
                .WithMany()
                .HasForeignKey(a => a.PrioridadeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Status)
                .WithMany()
                .HasForeignKey(a => a.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
