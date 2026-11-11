// E2E: fluxo de status, programação, motor de atraso e permissões por perfil.
// Requer a app iniciada com MotorAtraso__IntervaloSegundos=2.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const s = Date.now().toString().slice(-5);

const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();

const iso = (dias) => { const d = new Date(); d.setDate(d.getDate() + dias); return d.toISOString().slice(0, 10); };

async function entrar(page, u, p) {
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', u); await page.fill('#Senha', p);
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');
}
async function sair(page) {
  await page.click('button[aria-label="Menu do usuário"]');
  await page.click('button:has-text("Sair")'); await page.waitForLoadState('networkidle');
}
const statusDe = (id) => sql(`SELECT s."Codigo" FROM atividades a JOIN status_atividade s ON s."Id"=a."StatusId" WHERE a."Id"=${id};`);

async function criarAtividade(page, desc) {
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.selectOption('#TipoAtividadeId', { label: 'CEC — Capina em canteiros' });
  await page.click('[data-step="1"] [data-next="2"]');
  await page.selectOption('#PrioridadeId', { label: 'NORM — Normal' });
  await page.fill('#Descricao', desc);
  await page.click('[data-step="2"] [data-next="3"]');
  await page.click('#btn-enviar'); await page.waitForLoadState('networkidle');
  return Number(page.url().match(/Detalhe\/(\d+)/)[1]);
}
const msg = async (page, tipo) => ((await page.textContent(`.alert-${tipo}`).catch(() => null)) || '').trim().replace(/\s+/g, ' ');
const botoes = async (page) => page.$$eval('.card-body button, .card-body .btn', bs => bs.map(b => b.textContent.trim()).filter(Boolean));

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await entrar(page, 'admin', 'Admin@123');

  // ---------- Fluxo feliz ----------
  const id = await criarAtividade(page, `Fluxo completo ${s}`);
  console.log(`[Fluxo] criada #${id} com status:`, statusDe(id), '(ABERTA)');
  await page.screenshot({ path: path.join(OUT, '16-acoes-aberta.png') });

  // Programar: data no passado é recusada
  await page.goto(`${BASE}/Atividades/Detalhe/${id}`);
  await page.evaluate(() => document.getElementById('prog-fim').removeAttribute('min'));
  await page.fill('#prog-fim', iso(-3));
  await page.click('button:has-text("Salvar programação")'); await page.waitForLoadState('networkidle');
  console.log('[Programar] prazo no passado ->', await msg(page, 'danger'), '| status:', statusDe(id));

  // Início depois do fim é recusado
  await page.fill('#prog-inicio', iso(10)); await page.fill('#prog-fim', iso(5));
  await page.click('button:has-text("Salvar programação")'); await page.waitForLoadState('networkidle');
  console.log('[Programar] início > fim ->', await msg(page, 'danger'));

  // Programação válida
  await page.fill('#prog-inicio', iso(0)); await page.fill('#prog-fim', iso(5));
  await page.click('button:has-text("Salvar programação")'); await page.waitForLoadState('networkidle');
  console.log('[Programar] ok ->', await msg(page, 'success'), '| status:', statusDe(id), '(PROG)');

  // Iniciar
  await page.click('button:has-text("Iniciar execução")'); await page.waitForLoadState('networkidle');
  console.log('[Iniciar] ->', await msg(page, 'success'), '| status:', statusDe(id), '(EM_EXEC)');

  // Progresso
  await page.evaluate(() => { const r = document.getElementById('prog-pct'); r.value = 60; r.dispatchEvent(new Event('input')); });
  await page.click('button:has-text("Atualizar progresso")'); await page.waitForLoadState('networkidle');
  console.log('[Progresso] ->', await msg(page, 'success'), '| banco:', sql(`SELECT "Progresso" FROM atividades WHERE "Id"=${id};`), '(60)');

  // ---------- Motor de atraso ----------
  sql(`UPDATE atividades SET "DataFimPrevisto" = CURRENT_DATE - 2 WHERE "Id"=${id};`);
  await page.waitForTimeout(6000); // motor roda a cada 2s
  console.log('[Motor] vencida e em execução ->', statusDe(id), '(ATRASADA)');
  const auditoriaMotor = sql(`SELECT "UsuarioNome" || '/' || "Modo" FROM auditoria WHERE "Entidade"='Atividade' AND "EntidadeId"='${id}' AND "Payload" LIKE '%StatusId%' ORDER BY "Id" DESC LIMIT 1;`);
  console.log('[Motor] auditado como:', auditoriaMotor, '(Sistema/sistema)');

  // Atrasada e já iniciada: pode atualizar progresso e concluir; não pode "iniciar"
  await page.goto(`${BASE}/Atividades/Detalhe/${id}`);
  const bAtrasada = (await botoes(page)).join(' | ');
  console.log('[Atrasada iniciada] botões:', bAtrasada);
  await page.screenshot({ path: path.join(OUT, '17-acoes-atrasada.png') });

  // Reprogramar tira de ATRASADA e volta a EM_EXEC (já havia sido iniciada)
  await page.fill('#prog-fim', iso(7));
  await page.click('button:has-text("Salvar programação")'); await page.waitForLoadState('networkidle');
  console.log('[Reprogramar] atrasada+iniciada ->', statusDe(id), '(EM_EXEC)');
  await page.waitForTimeout(5000);
  console.log('[Reprogramar] motor NÃO re-marca (prazo futuro) ->', statusDe(id), '(EM_EXEC)');

  // Concluir
  page.once('dialog', d => d.accept());
  await page.click('button:has-text("Concluir atividade")'); await page.waitForLoadState('networkidle');
  console.log('[Concluir] ->', await msg(page, 'success'), '| status:', statusDe(id),
    '| progresso:', sql(`SELECT "Progresso" FROM atividades WHERE "Id"=${id};`));
  await page.screenshot({ path: path.join(OUT, '18-concluida.png') });
  console.log('[Concluída] sem botões de ação:', (await page.locator('text=Ações').count()) === 0);

  // Concluída não é afetada pelo motor mesmo com prazo vencido
  sql(`UPDATE atividades SET "DataFimPrevisto" = CURRENT_DATE - 9 WHERE "Id"=${id};`);
  await page.waitForTimeout(5000);
  console.log('[Motor] ignora concluída ->', statusDe(id), '(CONCL)');

  // ---------- Negar ----------
  const idNeg = await criarAtividade(page, `Para negar ${s}`);
  await page.fill('#neg-motivo', 'abc');
  await page.evaluate(() => document.getElementById('neg-motivo').removeAttribute('minlength'));
  await page.click('button:has-text("Negar")'); await page.waitForLoadState('networkidle');
  console.log('[Negar] motivo curto ->', await msg(page, 'danger'));
  await page.fill('#neg-motivo', 'Fora da competência do município');
  await page.click('button:has-text("Negar")'); await page.waitForLoadState('networkidle');
  console.log('[Negar] ok ->', await msg(page, 'success'), '| status:', statusDe(idNeg), '(NEGADA)');

  // ---------- Barreiras no servidor (POST direto, sem botão) ----------
  const tentar = (rota, corpo) => page.evaluate(async ([rota, corpo]) => {
    const t = (await (await fetch(location.href)).text()).match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/);
    const r = await fetch(rota, { method: 'POST', redirect: 'manual',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ ...corpo, __RequestVerificationToken: t ? t[1] : '' }) });
    return r.status;
  }, [rota, corpo]);
  await page.goto(`${BASE}/Atividades/Detalhe/${idNeg}`);
  await tentar(`/Atividades/Concluir/${idNeg}`, {});
  console.log('[Servidor] concluir NEGADA via POST direto -> status segue:', statusDe(idNeg), '(NEGADA)');
  await tentar(`/Atividades/Iniciar/${idNeg}`, {});
  console.log('[Servidor] iniciar NEGADA via POST direto -> status segue:', statusDe(idNeg), '(NEGADA)');

  // ---------- Programação ----------
  const idProg = await criarAtividade(page, `Para a tela de programação ${s}`);
  await page.fill('#prog-inicio', iso(0)); await page.fill('#prog-fim', iso(1));
  await page.click('button:has-text("Salvar programação")'); await page.waitForLoadState('networkidle');

  await page.goto(`${BASE}/Programacao?periodo=semana&referencia=${iso(0)}`);
  const naSemana = await page.locator('tbody tr', { hasText: `programação ${s}` }).count();
  console.log('[Programação] semana atual (status padrão Programada) contém a atividade:', naSemana === 1);
  await page.screenshot({ path: path.join(OUT, '19-programacao.png') });
  await page.goto(`${BASE}/Programacao?periodo=dia&referencia=${iso(30)}&todos=true`);
  console.log('[Programação] dia daqui a 30 dias não contém:', (await page.locator('tbody tr', { hasText: `programação ${s}` }).count()) === 0);
  await page.goto(`${BASE}/Programacao?periodo=mes&referencia=${iso(0)}&statusId=&todos=true`);
  console.log('[Programação] mês + todos os status contém:', (await page.locator('tbody tr', { hasText: `programação ${s}` }).count()) === 1);

  // ---------- Perfil servidor ----------
  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', `Servidor Fluxo ${s}`); await page.fill('#Login', `sf${s}`);
  await page.fill('#Email', `sf${s}@teste.local`); await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123'); await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle'); await sair(page);

  await entrar(page, `sf${s}`, 'Servidor@123');
  await page.goto(`${BASE}/Atividades/Detalhe/${idProg}`);
  const bSrv = (await botoes(page)).join(' | ');
  console.log('[Servidor] atividade PROG -> botões:', bSrv, '(só Iniciar, sem Programar/Negar)');
  const programarBloqueado = await tentar(`/Atividades/Programar/${idProg}`, { fim: iso(20) });
  console.log('[Servidor] POST Programar direto -> HTTP', programarBloqueado, '| prazo segue:', sql(`SELECT "DataFimPrevisto" FROM atividades WHERE "Id"=${idProg};`), '(não mudou)');
  await page.click('button:has-text("Iniciar execução")'); await page.waitForLoadState('networkidle');
  console.log('[Servidor] inicia a execução ->', statusDe(idProg), '(EM_EXEC)');

  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
