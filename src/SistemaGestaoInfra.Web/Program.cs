using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using SistemaGestaoInfra.Application.Common.Interfaces;
using SistemaGestaoInfra.Domain.Entities;
using SistemaGestaoInfra.Infra;
using SistemaGestaoInfra.Infra.Data;
using SistemaGestaoInfra.Web;
using SistemaGestaoInfra.Web.Controllers;
using SistemaGestaoInfra.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Pasta de dados persistentes (uploads e chaves). Em container, monte um volume em /app/App_Data.
var raizDados = builder.Configuration["Storage:RaizDados"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");

// Uploads ficam FORA do wwwroot: não são servidos como arquivo estático público.
// O acesso é feito por action autenticada (ex: Atividades/Foto/{id}).
var raizUploads = Path.Combine(raizDados, "uploads");

// Chaves que protegem o cookie de login. Sem persisti-las, todo reinício do container
// (ou escalar para 2 instâncias) invalidaria as sessões de todos os usuários.
builder.Services.AddDataProtection()
    .SetApplicationName("Zitei")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(raizDados, "keys")));

// Atrás de um proxy reverso (nginx, Traefik, Cloudflare), o IP real e o esquema HTTPS vêm em
// cabeçalhos X-Forwarded-*. Só confie neles se houver mesmo um proxy: do contrário, qualquer
// cliente poderia forjar o IP e driblar o limite de requisições e a auditoria.
if (builder.Configuration.GetValue("ReverseProxy:Habilitar", false))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Infraestrutura: DbContext (Npgsql) + storage de arquivos.
builder.Services.AddInfraestrutura(builder.Configuration, raizUploads);

// Multi-tenant: resolve a prefeitura do usuário autenticado por requisição.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<IAuditContext, WebAuditContext>();
builder.Services.AddHostedService<MotorAtrasoService>();

// ASP.NET Identity com perfis (roles).
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Usuários desativados ou com perfil alterado perdem a sessão em até 1 minuto.
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Conta/Login";
    options.LogoutPath = "/Conta/Logout";
    options.AccessDeniedPath = "/Conta/AcessoNegado";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Consulta pública: por IP, para impedir varredura em massa das demandas.
    options.AddPolicy(TransparenciaController.PoliticaPublica, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("Transparencia:LimitePorMinuto", 60),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    // Geocodificação: por USUÁRIO (cada busca pode virar uma chamada ao serviço externo, que tem cota própria).
    options.AddPolicy(GeocodificacaoController.PoliticaLimite, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("Geocodificacao:LimitePorMinuto", 30),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Politicas.GestaoCadastros, p =>
        p.RequireRole(Politicas.PerfisGestaoCadastros));
    options.AddPolicy(Politicas.Execucao, p =>
        p.RequireRole(Politicas.PerfisExecucao));
});

builder.Services.AddControllersWithViews(options =>
{
    // Todas as páginas exigem login, exceto as marcadas com [AllowAnonymous].
    var politica = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(politica));
});

var app = builder.Build();

if (builder.Configuration.GetValue("ReverseProxy:Habilitar", false))
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Em container o TLS costuma ser terminado por um proxy; defina RedirecionarHttps=false nesse caso.
if (builder.Configuration.GetValue("RedirecionarHttps", true))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.Use(async (context, next) =>
{
    // Cabeçalhos básicos de proteção em todas as respostas.
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Saúde para orquestradores/healthcheck: responde 200 só se o banco está acessível. Não expõe dados.
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct) ? Results.Text("ok") : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous()
    .DisableRateLimiting();

// Seed inicial do banco (migrations + dados base).
// A senha do admin vem da configuração (Seed:AdminPassword). O padrão público só vale em Development;
// fora dele, criar o admin sem senha configurada é recusado para não publicar um login conhecido.
var senhaAdmin = app.Configuration["Seed:AdminPassword"]
    ?? (app.Environment.IsDevelopment() ? "Admin@123" : null);

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
    await DbSeeder.SeedAsync(db, userManager, roleManager, senhaAdmin);
}

app.Run();
