// E2E: Mapa de atividades — marcadores, cores, filtros, XSS no popup e auto-refresh.
const { chromium } = require('playwright');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const s = Date.now().toString().slice(-5);

async function entrar(page) {
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', 'admin');
  await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]');
  await page.waitForLoadState('networkidle');
}

async function criar(page, { tipo, prio, desc, bairro, lat, lng }) {
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.selectOption('#TipoAtividadeId', { label: tipo });
  await page.fill('#Rua', 'Rua do Mapa');
  await page.fill('#Numero', '10');
  await page.fill('#Bairro', bairro);
  await page.fill('#Latitude', lat);
  await page.fill('#Longitude', lng);
  await page.click('[data-step="1"] [data-next="2"]');
  await page.selectOption('#PrioridadeId', { label: prio });
  await page.fill('#Descricao', desc);
  await page.click('[data-step="2"] [data-next="3"]');
  await page.click('#btn-enviar');
  await page.waitForLoadState('networkidle');
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  let dialogoXss = false;
  page.on('dialog', async d => { dialogoXss = true; await d.dismiss(); });
  await entrar(page);

  const xss = `<img src=x onerror=alert(1)> mapa ${s}`;
  await criar(page, { tipo: 'CEC — Capina em canteiros', prio: 'URG — Urgente', desc: xss, bairro: `Centro${s}`, lat: '-21.1775', lng: '-47.8103' });
  await criar(page, { tipo: 'TB — Tapa buraco', prio: 'NORM — Normal', desc: `Buraco na pista ${s}`, bairro: `Sul${s}`, lat: '-21.2000', lng: '-47.8200' });
  // Sem coordenadas: não pode aparecer no mapa
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.selectOption('#TipoAtividadeId', { label: 'CLC — Coleta de lixo convencional' });
  await page.click('[data-step="1"] [data-next="2"]');
  await page.selectOption('#PrioridadeId', { label: 'NORM — Normal' });
  await page.fill('#Descricao', `Sem coordenada ${s}`);
  await page.click('[data-step="2"] [data-next="3"]');
  await page.click('#btn-enviar');
  await page.waitForLoadState('networkidle');

  // ---- Mapa ----
  await page.goto(`${BASE}/Mapa`);
  await page.waitForSelector('.leaflet-interactive', { timeout: 15000 });
  const marcadores = await page.locator('path.leaflet-interactive').count();
  console.log('[Mapa] marcadores (esperado 2, sem a atividade sem coordenada):', marcadores);
  console.log('[Mapa] contador:', (await page.textContent('#mapa-contagem')).trim());
  const cores = await page.$$eval('path.leaflet-interactive', ps => ps.map(p => p.getAttribute('fill')));
  console.log('[Mapa] cor dos marcadores (ABERTA = #AE6BFF):', JSON.stringify([...new Set(cores)]));
  console.log('[Mapa] legenda com 6 status:', (await page.locator('[aria-label="Legenda"] .zt-status-dot').count()));
  await page.screenshot({ path: path.join(OUT, '13-mapa.png') });

  // Popup + XSS
  await page.locator('path.leaflet-interactive').first().click({ force: true });
  await page.waitForSelector('.leaflet-popup-content');
  const popup = await page.textContent('.leaflet-popup-content');
  const imgInjetada = await page.locator('.leaflet-popup-content img').count();
  console.log('[Popup] aberto:', popup.trim().slice(0, 70).replace(/\s+/g, ' '));
  console.log('[XSS] tag <img> injetada no popup (esperado 0):', imgInjetada, '| alert disparado (esperado false):', dialogoXss);
  await page.screenshot({ path: path.join(OUT, '14-mapa-popup.png') });

  // ---- Filtros ----
  await page.selectOption('#f_bairro', `Sul${s}`);
  await page.waitForTimeout(1200);
  console.log('[Filtro bairro Sul] marcadores (esperado 1):', await page.locator('path.leaflet-interactive').count());
  await page.selectOption('#f_bairro', '');
  await page.selectOption('#f_prioridade', { label: 'Urgente' });
  await page.waitForTimeout(1200);
  console.log('[Filtro prioridade Urgente] marcadores (esperado 1):', await page.locator('path.leaflet-interactive').count());
  await page.click('#btn-limpar');
  await page.waitForTimeout(1200);
  console.log('[Limpar filtros] marcadores (esperado 2):', await page.locator('path.leaflet-interactive').count());

  // Filtro de data (ISO) — dia de hoje deve manter os 2; dia futuro deve zerar
  const hoje = new Date().toISOString().slice(0, 10);
  await page.fill('#f_de', hoje); await page.dispatchEvent('#f_de', 'change');
  await page.waitForTimeout(1200);
  console.log('[Filtro De=hoje] marcadores (esperado 2):', await page.locator('path.leaflet-interactive').count());
  await page.fill('#f_de', '2999-01-01'); await page.dispatchEvent('#f_de', 'change');
  await page.waitForTimeout(1200);
  console.log('[Filtro De=2999] marcadores (esperado 0):', await page.locator('path.leaflet-interactive').count());
  await page.click('#btn-limpar');
  await page.waitForTimeout(1000);

  // ---- Auto-refresh ----
  const antes = await page.textContent('#mapa-ultima');
  await page.waitForTimeout(12500);
  const depois = await page.textContent('#mapa-ultima');
  console.log('[Auto-refresh] "última atualização" mudou:', antes !== depois, `(${antes} -> ${depois})`);
  await page.uncheck('#f_auto');
  console.log('[Auto-refresh] desligado ->', (await page.textContent('#mapa-countdown')).trim());

  // ---- Endpoint exige login ----
  const anon = await browser.newContext();
  const r = await anon.request.get(`${BASE}/Mapa/Dados`, { maxRedirects: 0 });
  console.log('[Dados sem login] ->', r.status(), '(esperado 302)');

  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
