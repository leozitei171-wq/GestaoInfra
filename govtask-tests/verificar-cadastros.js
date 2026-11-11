// E2E: CRUDs dos cadastros base (Setores, Tipos, Prioridades, Status).
const { chromium } = require('playwright');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const s = Date.now().toString().slice(-5);

async function entrar(page, usuario, senha) {
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', usuario);
  await page.fill('#Senha', senha);
  await page.click('button[type=submit]');
  await page.waitForLoadState('networkidle');
}

async function alerta(page, tipo) {
  const t = await page.textContent(`.alert-${tipo}`).catch(() => null);
  return t ? t.trim().replace(/\s+/g, ' ') : null;
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await entrar(page, 'admin', 'Admin@123');
  // ---- Setores ----
  await page.goto(`${BASE}/Setores/Novo`);
  await page.fill('#Codigo', `SET_${s}`);
  await page.fill('#Descricao', `Setor de teste ${s}`);
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  console.log('[Setor] criar ->', await alerta(page, 'success'));
  await page.screenshot({ path: path.join(OUT, '07-setores.png') });

  // Código duplicado
  await page.goto(`${BASE}/Setores/Novo`);
  await page.fill('#Codigo', `SET_${s}`);
  await page.fill('#Descricao', 'Duplicado');
  await page.click('button:has-text("Salvar")');
  await page.waitForTimeout(600);
  const dupSetor = await page.$$eval('span.text-danger', es => es.map(e => e.textContent.trim()).filter(Boolean));
  console.log('[Setor] duplicado ->', JSON.stringify(dupSetor));

  // Setor em uso (INFRA_SLU tem tipos vinculados) não pode ser excluído
  await page.goto(`${BASE}/Setores`);
  const linhaEmUso = page.locator('tr', { hasText: 'INFRA_SLU' });
  page.once('dialog', d => d.accept());
  await linhaEmUso.locator('button:has-text("Excluir")').click();
  await page.waitForLoadState('networkidle');
  console.log('[Setor] excluir em uso ->', await alerta(page, 'danger'));

  // Setor novo (sem uso) pode ser excluído
  const linhaNova = page.locator('tr', { hasText: `SET_${s}` });
  page.once('dialog', d => d.accept());
  await linhaNova.locator('button:has-text("Excluir")').click();
  await page.waitForLoadState('networkidle');
  console.log('[Setor] excluir sem uso ->', await alerta(page, 'success'));

  // ---- Tipos de atividade ----
  await page.goto(`${BASE}/TiposAtividade/Novo`);
  await page.fill('#Codigo', `TP${s}`);
  await page.fill('#Descricao', `Tipo de teste ${s}`);
  await page.selectOption('#SetorId', { index: 1 });
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  console.log('[Tipo] criar ->', await alerta(page, 'success'));

  // Tipo em uso? CEC não tem atividades ainda, então exclui o criado
  const linhaTipo = page.locator('tr', { hasText: `TP${s}` });
  page.once('dialog', d => d.accept());
  await linhaTipo.locator('button:has-text("Excluir")').click();
  await page.waitForLoadState('networkidle');
  console.log('[Tipo] excluir ->', await alerta(page, 'success'));

  // ---- Prioridades ----
  await page.goto(`${BASE}/Prioridades/Novo`);
  const ordemSugerida = await page.inputValue('#Ordem');
  await page.fill('#Codigo', `PR${s}`);
  await page.fill('#Descricao', `Prioridade ${s}`);
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  console.log(`[Prioridade] criar (ordem sugerida ${ordemSugerida}) ->`, await alerta(page, 'success'));

  const linhaPri = page.locator('tr', { hasText: `PR${s}` });
  page.once('dialog', d => d.accept());
  await linhaPri.locator('button:has-text("Excluir")').click();
  await page.waitForLoadState('networkidle');
  console.log('[Prioridade] excluir ->', await alerta(page, 'success'));

  // ---- Status: regras dos reservados ----
  await page.goto(`${BASE}/StatusAtividade`);
  await page.screenshot({ path: path.join(OUT, '08-status.png') });
  const semBotaoExcluirPadrao = await page.locator('tr', { hasText: 'ABERTA' })
    .locator('button:has-text("Excluir")').count();
  console.log('[Status] botões excluir em ABERTA (esperado 0):', semBotaoExcluirPadrao);

  // Editar padrão: código readonly e sem switch de ativo
  await page.locator('tr', { hasText: 'ABERTA' }).locator('a:has-text("Editar")').click();
  await page.waitForLoadState('networkidle');
  console.log('[Status] código readonly:', await page.getAttribute('#Codigo', 'readonly') !== null);
  console.log('[Status] switch Ativo oculto:', (await page.locator('#Ativo').count()) === 0);

  // Alterar cor e descrição do padrão funciona
  await page.fill('#Descricao', 'Aberta (sem programação)');
  await page.fill('#Cor', '#9b5cff');
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  console.log('[Status] editar padrão ->', await alerta(page, 'success'));

  // Tentar criar status com código reservado é recusado
  await page.goto(`${BASE}/StatusAtividade/Novo`);
  await page.fill('#Codigo', 'CONCL');
  await page.fill('#Descricao', 'Tentativa');
  await page.click('button:has-text("Salvar")');
  await page.waitForTimeout(600);
  const erroReservado = await page.$$eval('span.text-danger', es => es.map(e => e.textContent.trim()).filter(Boolean));
  console.log('[Status] código reservado ->', JSON.stringify(erroReservado));

  // Bypass por POST direto: alterar código de um status padrão não deve pegar
  await page.goto(`${BASE}/StatusAtividade`);
  const idAberta = await page.locator('tr', { hasText: 'ABERTA' })
    .locator('a:has-text("Editar")').getAttribute('href');
  const resultadoBypass = await page.evaluate(async (href) => {
    const r = await fetch(href);
    const html = await r.text();
    const token = html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    const id = html.match(/id="Id"[^>]*value="(\d+)"/)[1];
    const body = new URLSearchParams({
      Id: id, EhPadrao: 'false', Codigo: 'HACKEADO',
      Descricao: 'Tentativa de bypass', Cor: '#000000', Ativo: 'false',
      __RequestVerificationToken: token
    });
    const post = await fetch(`/StatusAtividade/Editar/${id}`, {
      method: 'POST', body,
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' }
    });
    return `${post.status} (id=${id})`;
  }, idAberta);
  await page.goto(`${BASE}/StatusAtividade`);
  const temHackeado = await page.locator('tbody').textContent();
  console.log('[Status] POST bypass status HTTP:', resultadoBypass,
    '| código alterado:', temHackeado.includes('HACKEADO'),
    '| ABERTA intacta:', temHackeado.includes('ABERTA'));

  // ---- Permissão: servidor não acessa cadastros ----
  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', `Servidor ${s}`);
  await page.fill('#Login', `srv${s}`);
  await page.fill('#Email', `srv${s}@teste.local`);
  await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123');
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');

  await page.click('button[aria-label="Menu do usuário"]');
  await page.click('button:has-text("Sair")');
  await page.waitForLoadState('networkidle');

  await entrar(page, `srv${s}`, 'Servidor@123');
  const menuServidor = await page.$$eval('.zt-menu-link', els => els.map(e => e.textContent.trim()));
  console.log('[Perfil] menu do servidor:', menuServidor.join(' | '));
  await page.goto(`${BASE}/Setores`);
  console.log('[Perfil] servidor em /Setores ->', page.url());
  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
