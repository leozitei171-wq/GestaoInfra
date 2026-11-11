using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Domain.Enums;
using SistemaGestaoInfra.Infra.Data;

namespace SistemaGestaoInfra.Infra.Data;

/// <summary>
/// Popula o banco com dados iniciais: perfis (roles), prefeitura padrão,
/// usuário administrador e cadastros base (status, prioridades, setores, tipos).
/// Idempotente — pode rodar a cada inicialização sem duplicar.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        string? senhaAdmin)
    {
        await db.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        var prefeitura = await SeedPrefeituraAsync(db);
        await SeedAdminAsync(userManager, prefeitura.Id, senhaAdmin);
        await SeedStatusAsync(db, prefeitura.Id);
        await SeedPrioridadesAsync(db, prefeitura.Id);
        await SeedSetoresETiposAsync(db, prefeitura.Id);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var perfil in Enum.GetNames<PerfilUsuario>())
        {
            if (!await roleManager.RoleExistsAsync(perfil))
            {
                await roleManager.CreateAsync(new IdentityRole(perfil));
            }
        }
    }

    private static async Task<Prefeitura> SeedPrefeituraAsync(AppDbContext db)
    {
        var prefeitura = await db.Prefeituras.FirstOrDefaultAsync();
        if (prefeitura is not null)
        {
            return prefeitura;
        }

        prefeitura = new Prefeitura
        {
            Nome = "Prefeitura Padrão (desenvolvimento)",
            Cidade = "Ribeirão Preto",
            Uf = "SP",
            Ativo = true
        };

        db.Prefeituras.Add(prefeitura);
        await db.SaveChangesAsync();
        return prefeitura;
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        int prefeituraId,
        string? senhaAdmin)
    {
        const string login = "admin";
        if (await userManager.FindByNameAsync(login) is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(senhaAdmin))
        {
            throw new InvalidOperationException(
                "O usuário 'admin' ainda não existe e nenhuma senha foi configurada. " +
                "Defina a variável de ambiente Seed__AdminPassword (mínimo de 8 caracteres) e inicie novamente.");
        }

        var admin = new ApplicationUser
        {
            UserName = login,
            Email = "admin@sistema.local",
            EmailConfirmed = true,
            NomeCompleto = "Administrador",
            Perfil = PerfilUsuario.Admin,
            PrefeituraId = prefeituraId,
            Ativo = true
        };

        var resultado = await userManager.CreateAsync(admin, senhaAdmin);
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                "Não foi possível criar o admin: " + string.Join("; ", resultado.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(admin, nameof(PerfilUsuario.Admin));
    }

    private static async Task SeedStatusAsync(AppDbContext db, int prefeituraId)
    {
        if (await db.StatusAtividades.IgnoreQueryFilters().AnyAsync(s => s.PrefeituraId == prefeituraId))
        {
            return;
        }

        var status = new[]
        {
            new StatusAtividade { PrefeituraId = prefeituraId, Codigo = "ABERTA", Descricao = "Aberta", Cor = "#AE6BFF" },
            new StatusAtividade { PrefeituraId = prefeituraId, Codigo = "PROG", Descricao = "Programada", Cor = "#EE6BFF" },
            new StatusAtividade { PrefeituraId = prefeituraId, Codigo = "EM_EXEC", Descricao = "Em execução", Cor = "#E6FF6B" },
            new StatusAtividade { PrefeituraId = prefeituraId, Codigo = "ATRASADA", Descricao = "Atrasada", Cor = "#FF6B6B" },
            new StatusAtividade { PrefeituraId = prefeituraId, Codigo = "NEGADA", Descricao = "Negada", Cor = "#767184" },
            new StatusAtividade { PrefeituraId = prefeituraId, Codigo = "CONCL", Descricao = "Concluída", Cor = "#049A15" }
        };

        db.StatusAtividades.AddRange(status);
        await db.SaveChangesAsync();
    }

    private static async Task SeedPrioridadesAsync(AppDbContext db, int prefeituraId)
    {
        if (await db.Prioridades.IgnoreQueryFilters().AnyAsync(p => p.PrefeituraId == prefeituraId))
        {
            return;
        }

        var prioridades = new[]
        {
            new Prioridade { PrefeituraId = prefeituraId, Codigo = "URG", Descricao = "Urgente", Ordem = 1 },
            new Prioridade { PrefeituraId = prefeituraId, Codigo = "NORM", Descricao = "Normal", Ordem = 2 }
        };

        db.Prioridades.AddRange(prioridades);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSetoresETiposAsync(AppDbContext db, int prefeituraId)
    {
        if (await db.Setores.IgnoreQueryFilters().AnyAsync(s => s.PrefeituraId == prefeituraId))
        {
            return;
        }

        var setorLimpeza = new Setor
        {
            PrefeituraId = prefeituraId,
            Codigo = "INFRA_SLU",
            Descricao = "Serviços de Limpeza Urbana"
        };
        var setorManutencao = new Setor
        {
            PrefeituraId = prefeituraId,
            Codigo = "INFRA_SMP",
            Descricao = "Serviços de Manutenção e Pavimentação"
        };

        db.Setores.AddRange(setorLimpeza, setorManutencao);
        await db.SaveChangesAsync();

        var tipos = new[]
        {
            new TipoAtividade { PrefeituraId = prefeituraId, Codigo = "CEC", Descricao = "Capina em canteiros", SetorId = setorLimpeza.Id },
            new TipoAtividade { PrefeituraId = prefeituraId, Codigo = "CLC", Descricao = "Coleta de lixo convencional", SetorId = setorLimpeza.Id },
            new TipoAtividade { PrefeituraId = prefeituraId, Codigo = "TB", Descricao = "Tapa buraco", SetorId = setorManutencao.Id },
            new TipoAtividade { PrefeituraId = prefeituraId, Codigo = "CPA", Descricao = "Corte/poda de árvore", SetorId = setorManutencao.Id }
        };

        db.TiposAtividade.AddRange(tipos);
        await db.SaveChangesAsync();
    }
}
