// E2E: geolocalização no cadastro de atividade (GPS simulado + respostas simuladas do serviço de endereços).
// Opcional: REAL=1 faz também UMA consulta de verdade ao Nominatim no final.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const BASE = 'http://localhost:5080';
const s = Date.now().toString().slice(-5);
const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();

const RESULTADO = (extra = {}) => ({
  lat: -21.2000123, lng: -47.8200456, rua: 'Rua Mock', numero: '45', bairro: 'Centro', cidade: 'Ribeirão Preto',
  uf: 'SP', cep: '14015-000', descricao: 'Rua Mock, 45', precisao: 'endereco', foraDaCidade: false, ...extra
});
const json = (corpo, status = 200) => ({ status, contentType: 'application/json', body: JSON.stringify(corpo) });

async function login(page) {
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', 'admin'); await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');
}
async function abrirWizard(page) {
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.selectOption('#TipoAtividadeId', { label: 'CEC — Capina em canteiros' });
  await page.waitForFunction(() => window.__zitei);
}
const valor = (page, id) => page.inputValue(id);
const status = async (page) => ((await page.textContent('#geo-status')) || '').trim();
const passoVisivel = (page, n) => page.locator(`[data-step="${n}"]`).isVisible();

async function concluir(page, desc) {
  if (await passoVisivel(page, 1)) await page.click('[data-step="1"] [data-next="2"]');
  await page.selectOption('#PrioridadeId', { label: 'NORM — Normal' });
  await page.fill('#Descricao', desc);
  await page.click('[data-step="2"] [data-next="3"]');
  await page.click('#btn-enviar'); await page.waitForLoadState('networkidle');
  return page.url().match(/Detalhe\/(\d+)/)?.[1];
}

(async () => {
  const browser = await chromium.launch();
  const ctx = await browser.newContext({
    viewport: { width: 1440, height: 1100 },
    permissions: ['geolocation'],
    geolocation: { latitude: -21.1775, longitude: -47.8103, accuracy: 20 }
  });
  const page = await ctx.newPage();
  const dialogos = [];
  page.on('dialog', async d => { dialogos.push(d.message()); await d.dismiss(); });
  await login(page);
  const criadas = [];

  // ---------- 1) GPS: ponto + endereço preenchido + grava ----------
  await page.route('**/Geocodificacao/Reverso*', r => r.fulfill(json({ encontrado: true, resultado: RESULTADO() })));
  await abrirWizard(page);
  await page.click('#btn-gps');
  await page.waitForFunction(() => document.getElementById('Rua').value !== '', null, { timeout: 8000 });
  console.log('[GPS] ponto marcado:', await valor(page, '#Latitude'), await valor(page, '#Longitude'));
  console.log('[GPS] endereço preenchido:', [await valor(page, '#Rua'), await valor(page, '#Numero'), await valor(page, '#Bairro'), await valor(page, '#Cep')].join(' | '));
  console.log('[GPS] mensagem:', await status(page));
  const id1 = await concluir(page, `GPS ${s}`); criadas.push(id1);
  console.log('[GPS] gravado no banco:', sql(`SELECT "Latitude" || ' / ' || "Longitude" || ' / ' || "Rua" FROM atividades WHERE "Id"=${id1};`));

  // ---------- 2) GPS: precisão baixa e fora da cidade ----------
  await ctx.setGeolocation({ latitude: -21.1775, longitude: -47.8103, accuracy: 400 });
  await page.unroute('**/Geocodificacao/Reverso*');
  await page.route('**/Geocodificacao/Reverso*', r => r.fulfill(json({ encontrado: true, resultado: RESULTADO({ cidade: 'Campinas', foraDaCidade: true }) })));
  await abrirWizard(page); await page.click('#btn-gps');
  await page.waitForFunction(() => /fora da cidade/.test(document.getElementById('geo-status').textContent), null, { timeout: 8000 });
  console.log('[GPS] fora da cidade e precisão 400 m avisados:', /fora da cidade/.test(await status(page)) && /precisão/.test(await status(page)));

  // ---------- 3) GPS: serviço de endereços fora do ar -> ponto fica, usuário é avisado ----------
  await ctx.setGeolocation({ latitude: -21.1775, longitude: -47.8103, accuracy: 20 });
  await page.unroute('**/Geocodificacao/Reverso*');
  await page.route('**/Geocodificacao/Reverso*', r => r.fulfill(json({ erro: 'O serviço de endereços demorou demais para responder.' }, 503)));
  await abrirWizard(page); await page.click('#btn-gps');
  await page.waitForFunction(() => /manualmente/.test(document.getElementById('geo-status').textContent), null, { timeout: 8000 });
  console.log('[GPS] reverso 503 -> ponto mantido:', (await valor(page, '#Latitude')) !== '', '| aviso:', (await status(page)).slice(0, 60));

  // ---------- 4) GPS: permissão negada (contexto sem permissão) ----------
  const ctxNegado = await browser.newContext({ viewport: { width: 1440, height: 1100 } });
  const pn = await ctxNegado.newPage(); await login(pn); await abrirWizard(pn); await pn.click('#btn-gps');
  await pn.waitForFunction(() => document.getElementById('geo-status').textContent.length > 0, null, { timeout: 8000 });
  console.log('[GPS] permissão negada -> mensagem clara:', (await status(pn)).slice(0, 80), '| botão reabilitado:', await pn.locator('#btn-gps').isEnabled());
  await ctxNegado.close();

  // ---------- 5) Endereço -> ponto automático ao avançar ----------
  await page.unroute('**/Geocodificacao/Reverso*');
  let chamadasBuscar = 0;
  await page.route('**/Geocodificacao/Buscar*', r => { chamadasBuscar++; r.fulfill(json({ encontrado: true, resultados: [RESULTADO()] })); });
  await abrirWizard(page);
  await page.fill('#Rua', 'Rua Mock'); await page.fill('#Numero', '45'); await page.fill('#Bairro', 'Centro');
  await page.click('[data-step="1"] [data-next="2"]');
  await page.waitForSelector('[data-step="2"]:not(.d-none)', { timeout: 8000 });
  console.log('[Endereço] avançou e marcou o ponto sozinho:', await valor(page, '#Latitude'), await valor(page, '#Longitude'), '| buscas:', chamadasBuscar);
  const id5 = await concluir(page, `Endereco auto ${s}`); criadas.push(id5);
  console.log('[Endereço] gravado:', sql(`SELECT "Latitude" FROM atividades WHERE "Id"=${id5};`));

  // ---------- 6) Botão "Localizar endereço" + alternativas (com XSS) ----------
  await page.unroute('**/Geocodificacao/Buscar*');
  await page.route('**/Geocodificacao/Buscar*', r => r.fulfill(json({ encontrado: true, resultados: [
    RESULTADO(), RESULTADO({ lat: -21.21, lng: -47.83, rua: '<img src=x onerror=alert(1)>', numero: null, descricao: 'x' })] })));
  await abrirWizard(page);
  await page.fill('#Rua', 'Rua Mock'); await page.click('#btn-localizar');
  await page.waitForSelector('#geo-opcoes:not(.d-none) button', { timeout: 8000 });
  console.log('[Alternativas] exibidas (1):', await page.locator('#geo-opcoes button').count(),
    '| tag <img> injetada:', await page.locator('#geo-opcoes img, #geo-status img').count(), '| alert disparado:', dialogos.length > 0);
  await page.click('#geo-opcoes button');
  console.log('[Alternativas] escolher outra move o ponto:', await valor(page, '#Latitude'));

  // ---------- 7) Endereço não encontrado -> segue, com aviso no passo 2 ----------
  await page.unroute('**/Geocodificacao/Buscar*');
  await page.route('**/Geocodificacao/Buscar*', r => r.fulfill(json({ encontrado: false, resultados: [] })));
  await abrirWizard(page);
  await page.fill('#Rua', 'Rua Inexistente'); await page.click('[data-step="1"] [data-next="2"]');
  await page.waitForSelector('[data-step="2"]:not(.d-none)', { timeout: 8000 });
  console.log('[Não achou] avançou:', true, '| aviso no passo 2:', await page.locator('#geo-aviso-passo2').isVisible(), '| sem ponto:', (await valor(page, '#Latitude')) === '');
  const id7 = await concluir(page, `Sem local ${s}`); criadas.push(id7);
  console.log('[Não achou] atividade criada mesmo assim:', !!id7);

  // ---------- 8) Serviço fora do ar ao avançar -> nunca trava o usuário ----------
  await page.unroute('**/Geocodificacao/Buscar*');
  await page.route('**/Geocodificacao/Buscar*', r => r.fulfill(json({ erro: 'Muitas consultas.' }, 503)));
  await abrirWizard(page);
  await page.fill('#Rua', 'Rua Mock'); await page.click('[data-step="1"] [data-next="2"]');
  await page.waitForSelector('[data-step="2"]:not(.d-none)', { timeout: 8000 });
  console.log('[503] usuário avança normalmente:', true, '| aviso:', await page.locator('#geo-aviso-passo2').isVisible());

  // ---------- 9) Ponto marcado à mão NÃO é sobrescrito pela busca automática ----------
  await page.unroute('**/Geocodificacao/Buscar*');
  let buscasManual = 0;
  await page.route('**/Geocodificacao/Buscar*', r => { buscasManual++; r.fulfill(json({ encontrado: true, resultados: [RESULTADO({ lat: -21.5, lng: -47.5 })] })); });
  await abrirWizard(page);
  await page.waitForSelector('#mapa-picker .leaflet-tile-loaded', { timeout: 15000 }).catch(() => {});
  await page.locator('#mapa-picker').click({ position: { x: 200, y: 150 } });
  const manual = await valor(page, '#Latitude');
  await page.fill('#Rua', 'Rua Mock'); await page.click('[data-step="1"] [data-next="2"]');
  await page.waitForSelector('[data-step="2"]:not(.d-none)');
  console.log('[Manual] ponto clicado preservado:', manual === await valor(page, '#Latitude'), '| buscas feitas (esperado 0):', buscasManual);

  // ---------- 10) Arrastar o marcador ajusta as coordenadas ----------
  await abrirWizard(page);
  await page.waitForSelector('#mapa-picker .leaflet-tile-loaded', { timeout: 15000 }).catch(() => {});
  await page.locator('#mapa-picker').click({ position: { x: 200, y: 150 } });
  const antes = await valor(page, '#Latitude');
  const caixa = await page.locator('.leaflet-marker-icon').first().boundingBox();
  await page.mouse.move(caixa.x + caixa.width / 2, caixa.y + caixa.height / 2);
  await page.mouse.down(); await page.mouse.move(caixa.x + 120, caixa.y + 90, { steps: 8 }); await page.mouse.up();
  console.log('[Arrastar] coordenadas mudaram:', antes !== await valor(page, '#Latitude'));

  // ---------- 11) Validação REAL do servidor (sem mocks) ----------
  await page.unrouteAll();
  const get = async (rota) => { const r = await page.request.get(`${BASE}${rota}`); return r.status(); };
  console.log('[Servidor] Buscar sem dados ->', await get('/Geocodificacao/Buscar'), '(400)');
  console.log('[Servidor] Buscar com rua gigante ->', await get(`/Geocodificacao/Buscar?rua=${'x'.repeat(300)}`), '(400)');
  console.log('[Servidor] Reverso lat=999 ->', await get('/Geocodificacao/Reverso?lat=999&lng=1'), '(400)');
  console.log('[Servidor] Reverso lat=abc ->', await get('/Geocodificacao/Reverso?lat=abc&lng=1'), '(400)');
  const anon = await browser.newContext();
  const ra = await anon.request.get(`${BASE}/Geocodificacao/Buscar?rua=x`, { maxRedirects: 0 });
  console.log('[Servidor] sem login ->', ra.status(), '(302)');

  // ---------- 12) Teste real mínimo no Nominatim (opcional) ----------
  if (process.env.REAL === '1') {
    const r = await page.request.get(`${BASE}/Geocodificacao/Buscar?rua=${encodeURIComponent('Avenida Presidente Vargas')}&numero=2000`);
    const c = await r.json().catch(() => ({}));
    const p = c.resultados?.[0];
    console.log('[REAL] Nominatim ->', r.status(), p ? `${p.lat}, ${p.lng} | ${p.rua} | ${p.cidade}` : JSON.stringify(c).slice(0, 120));
    console.log('[REAL] ponto dentro da região de Ribeirão Preto:', !!p && p.lat < -21.0 && p.lat > -21.4 && p.lng < -47.6 && p.lng > -48.0);
  }

  console.log('[XSS] alertas disparados no total (esperado 0):', dialogos.length);
  await browser.close();
  sql(`DELETE FROM atividades WHERE "Id" IN (${criadas.filter(Boolean).join(',') || 0});`);
  console.log('[Limpeza] atividades de teste removidas:', criadas.filter(Boolean).length);
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
