// E2E: Atividades — wizard, upload, filtros, acesso a arquivos e isolamento.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const TMP = path.join(__dirname, 'tmp-upload');
fs.mkdirSync(TMP, { recursive: true });

// PNG 1x1 válido
const PNG_OK = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64');
const fotoOk = path.join(TMP, 'foto.png');
const fotoFalsa = path.join(TMP, 'falsa.png');      // extensão .png, conteúdo de texto
const scriptDisfarcado = path.join(TMP, 'shell.php'); // extensão proibida
fs.writeFileSync(fotoOk, PNG_OK);
fs.writeFileSync(fotoFalsa, '<?php echo "nao sou imagem"; ?>');
fs.writeFileSync(scriptDisfarcado, '<?php system($_GET["c"]); ?>');

const s = Date.now().toString().slice(-5);

async function entrar(page, usuario, senha) {
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', usuario);
  await page.fill('#Senha', senha);
  await page.click('button[type=submit]');
  await page.waitForLoadState('networkidle');
}
async function sair(page) {
  await page.click('button[aria-label="Menu do usuário"]');
  await page.click('button:has-text("Sair")');
  await page.waitForLoadState('networkidle');
}
const txt = async (page, sel) => {
  const t = await page.textContent(sel).catch(() => null);
  return t ? t.trim().replace(/\s+/g, ' ') : null;
};

async function preencherPasso1(page, tipoLabel) {
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.selectOption('#TipoAtividadeId', { label: tipoLabel });
  await page.fill('#Rua', 'Rua Teste');
  await page.fill('#Numero', '123');
  await page.fill('#Bairro', 'Centro');
}

(async () => {
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await ctx.newPage();
  await entrar(page, 'admin', 'Admin@123');
  // ---- 1) Validação por passo no cliente ----
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.click('[data-next="2"]');
  console.log('[Wizard] passo 1 sem tipo bloqueia:',
    await page.locator('[data-step="1"]').isVisible() && (await page.locator('.wiz-erro').count()) > 0);

  await preencherPasso1(page, 'CEC — Capina em canteiros');
  await page.click('[data-step="1"] [data-next="2"]');
  await page.click('[data-step="2"] [data-next="3"]');
  console.log('[Wizard] passo 2 vazio bloqueia:', await page.locator('[data-step="2"]').isVisible());

  // ---- 2) Mapa: clique preenche lat/lng ----
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.waitForSelector('#mapa-picker .leaflet-tile-loaded', { timeout: 15000 }).catch(() => {});
  await page.locator('#mapa-picker').click({ position: { x: 200, y: 150 } });
  const lat = await page.inputValue('#Latitude');
  const lng = await page.inputValue('#Longitude');
  console.log('[Mapa] clique preencheu coordenadas:', lat !== '' && lng !== '', `(${lat}, ${lng})`);
  await page.screenshot({ path: path.join(OUT, '09-wizard-passo1.png') });

  // ---- 3) Fluxo completo com foto válida ----
  await preencherPasso1(page, 'CEC — Capina em canteiros');
  await page.locator('#mapa-picker').click({ position: { x: 220, y: 140 } });
  await page.click('[data-step="1"] [data-next="2"]');
  await page.selectOption('#PrioridadeId', { label: 'URG — Urgente' });
  await page.fill('#Descricao', `Mato alto no canteiro central ${s}`);
  await page.check('#Requerimento');
  await page.fill('#NumeroRequerimento', `REQ-${s}`);
  await page.click('[data-step="2"] [data-next="3"]');
  await page.setInputFiles('#Foto', fotoOk);
  console.log('[Foto] preview exibido:', await page.locator('#preview-foto').isVisible());
  await page.screenshot({ path: path.join(OUT, '10-wizard-passo3.png') });
  await page.click('#btn-enviar');
  await page.waitForLoadState('networkidle');
  console.log('[Criar] ->', await txt(page, '.alert-success'));
  const urlDetalhe = page.url();
  const idCriada = urlDetalhe.match(/Detalhe\/(\d+)/)?.[1];
  console.log('[Criar] redirecionou para detalhe:', /Detalhe\/\d+/.test(urlDetalhe), '| id:', idCriada);
  await page.screenshot({ path: path.join(OUT, '11-detalhe.png'), fullPage: true });

  // Foto é servida para usuário logado
  const respFoto = await page.request.get(`${BASE}/Atividades/Foto/${idCriada}`);
  console.log('[Foto] logado ->', respFoto.status(), respFoto.headers()['content-type'],
    '| nosniff:', respFoto.headers()['x-content-type-options']);

  // ---- 4) Uploads maliciosos ----
  for (const [rotulo, arquivo] of [['conteúdo falso .png', fotoFalsa], ['extensão .php', scriptDisfarcado]]) {
    await preencherPasso1(page, 'TB — Tapa buraco');
    await page.click('[data-step="1"] [data-next="2"]');
    await page.selectOption('#PrioridadeId', { label: 'NORM — Normal' });
    await page.fill('#Descricao', `Tentativa de upload malicioso ${rotulo}`);
    await page.click('[data-step="2"] [data-next="3"]');
    await page.setInputFiles('#Foto', arquivo);
    await page.click('#btn-enviar');
    await page.waitForLoadState('networkidle');
    const erro = await page.$$eval('span[data-valmsg-for="Foto"]', es =>
      es.map(e => e.textContent.trim()).filter(Boolean));
    console.log(`[Upload malicioso: ${rotulo}] recusado:`, page.url().includes('/Atividades/Nova'), '|', erro[0] ?? 'sem msg');
  }

  // ---- 5) Listagem e filtros ----
  await page.goto(`${BASE}/Atividades`);
  const total = await txt(page, '.card-header span.me-auto');
  console.log('[Lista] total:', total);
  await page.fill('#Busca', `canteiro central ${s}`);
  await page.click('button[aria-label="Filtrar"]');
  await page.waitForLoadState('networkidle');
  console.log('[Filtro busca] linhas:', await page.locator('tbody tr').count());
  await page.screenshot({ path: path.join(OUT, '12-lista.png') });

  await page.goto(`${BASE}/Atividades?StatusId=999`);
  console.log('[Filtro status inexistente] vazio:', (await page.locator('tbody tr').count()) === 0);

  // ---- 6) Isolamento: foto SEM login é bloqueada ----
  const anon = await browser.newContext();
  const respAnon = await anon.request.get(`${BASE}/Atividades/Foto/${idCriada}`, { maxRedirects: 0 });
  console.log('[Foto] sem login ->', respAnon.status(), '(esperado 302 para login)');
  const respStatic = await anon.request.get(`${BASE}/uploads/atividades/qualquer.png`, { maxRedirects: 0 });
  console.log('[Uploads] acesso estático direto ->', respStatic.status(), '(esperado 404)');
  await anon.close();

  // ---- 7) Perfil servidor pode registrar e consultar ----
  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', `Servidor ${s}`);
  await page.fill('#Login', `srv${s}`);
  await page.fill('#Email', `srv${s}@teste.local`);
  await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123');
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  await sair(page);
  await entrar(page, `srv${s}`, 'Servidor@123');
  await page.goto(`${BASE}/Atividades`);
  console.log('[Servidor] acessa lista:', page.url().endsWith('/Atividades'));
  await page.goto(`${BASE}/Atividades/Nova`);
  console.log('[Servidor] acessa wizard:', page.url().includes('/Atividades/Nova'));
  await sair(page);
  await browser.close();
  fs.rmSync(TMP, { recursive: true, force: true });
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
