// E2E: atividade sem coordenadas -> aviso no mapa -> marcar no detalhe -> aparece no mapa.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const BASE = 'http://localhost:5080';
const s = Date.now().toString().slice(-5);
const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', 'admin'); await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');

  // Atividade de teste SEM coordenadas (só endereço), como a sua.
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.selectOption('#TipoAtividadeId', { label: 'CEC — Capina em canteiros' });
  await page.fill('#Rua', 'Rua do Teste'); await page.fill('#Bairro', `BairroLoc${s}`);
  await page.click('[data-step="1"] [data-next="2"]');
  await page.selectOption('#PrioridadeId', { label: 'NORM — Normal' });
  await page.fill('#Descricao', `Teste localizacao ${s}`);
  await page.click('[data-step="2"] [data-next="3"]'); await page.click('#btn-enviar');
  await page.waitForLoadState('networkidle');
  const id = page.url().match(/Detalhe\/(\d+)/)[1];
  console.log('[Criada] atividade de teste #' + id, '| coordenadas no banco:', JSON.stringify(sql(`SELECT "Latitude" FROM atividades WHERE "Id"=${id};`)));

  // 1) Mapa geral avisa
  await page.goto(`${BASE}/Mapa`);
  await page.waitForSelector('#mapa-aviso:not(.d-none)', { timeout: 10000 }).catch(() => {});
  const aviso = (await page.textContent('#mapa-aviso')).trim();
  console.log('[Mapa] aviso de atividades sem localização:', aviso.includes('não têm localização') || aviso.includes('não tem localização') ? 'SIM' : 'NÃO', '|', aviso.slice(0, 70));

  // 2) Detalhe mostra o aviso e deixa marcar
  await page.goto(`${BASE}/Atividades/Detalhe/${id}`);
  console.log('[Detalhe] aviso "não tem localização":', (await page.locator('text=não tem localização').count()) > 0);
  console.log('[Detalhe] botão salvar começa desabilitado:', await page.locator('#loc-salvar').isDisabled());
  await page.waitForSelector('#mapa-detalhe .leaflet-tile-loaded', { timeout: 15000 }).catch(() => {});
  await page.locator('#mapa-detalhe').click({ position: { x: 150, y: 120 } });
  console.log('[Detalhe] após clicar, botão habilitado:', await page.locator('#loc-salvar').isEnabled(), '|', (await page.textContent('#loc-texto')).trim());
  await page.click('#loc-salvar'); await page.waitForLoadState('networkidle');
  console.log('[Salvar] ->', ((await page.textContent('.alert-success').catch(() => '')) || '').trim());
  console.log('[Banco] coordenadas gravadas:', sql(`SELECT "Latitude" || ' / ' || "Longitude" FROM atividades WHERE "Id"=${id};`));
  console.log('[Auditoria] alteração registrada:', sql(`SELECT count(*) FROM auditoria WHERE "Entidade"='Atividade' AND "EntidadeId"='${id}' AND "Acao"='Alterou' AND "Payload" LIKE '%Latitude%';`));

  // 3) Agora aparece no mapa
  await page.goto(`${BASE}/Mapa`);
  await page.waitForSelector('path.leaflet-interactive', { timeout: 15000 });
  console.log('[Mapa] marcadores desenhados (>=1):', await page.locator('path.leaflet-interactive').count());
  await page.selectOption('#f_bairro', `BairroLoc${s}`); await page.waitForTimeout(1500);
  console.log('[Mapa] filtrando o bairro de teste -> marcadores (esperado 1):', await page.locator('path.leaflet-interactive').count());

  // 4) Coordenadas inválidas via POST direto são recusadas
  await page.goto(`${BASE}/Atividades/Detalhe/${id}`);
  const antes = sql(`SELECT "Latitude" FROM atividades WHERE "Id"=${id};`);
  await page.evaluate(async (id) => {
    const t = (await (await fetch(location.href)).text()).match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    await fetch(`/Atividades/DefinirLocalizacao/${id}`, { method: 'POST', redirect: 'manual',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ latitude: '999', longitude: '-47.8', __RequestVerificationToken: t }) });
  }, id);
  console.log('[Validação] latitude 999 recusada (valor não mudou):', antes === sql(`SELECT "Latitude" FROM atividades WHERE "Id"=${id};`));

  await browser.close();
  // limpeza: remove SÓ a atividade de teste (a sua #47 não é tocada)
  sql(`DELETE FROM atividades WHERE "Id"=${id} AND "Descricao"='Teste localizacao ${s}';`);
  console.log('[Limpeza] atividade de teste removida. Atividades restantes:', sql('SELECT string_agg("Id"::text, \',\') FROM atividades;'));
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
