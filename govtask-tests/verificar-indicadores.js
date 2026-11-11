// E2E: Indicadores — gráficos renderizam, números batem com o banco, permissões.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();

async function entrar(page, u, p) {
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', u); await page.fill('#Senha', p);
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const erros = [];
  page.on('pageerror', e => erros.push(e.message));
  page.on('dialog', async d => { erros.push('ALERT: ' + d.message()); await d.dismiss(); });
  await entrar(page, 'admin', 'Admin@123');

  await page.goto(`${BASE}/Indicadores?dias=365`);
  await page.waitForSelector('.apexcharts-canvas', { timeout: 15000 });
  await page.waitForTimeout(1500);
  console.log('[Gráficos] renderizados (esperado 4):', await page.locator('.apexcharts-canvas').count());

  // Números do painel x banco (período de 365 dias)
  const valores = await page.$$eval('.zt-stat-value', es => es.map(e => e.textContent.trim()));
  const total = Number(sql(`SELECT count(*) FROM atividades WHERE "CriadoEm" >= now() - interval '365 days';`));
  const atras = Number(sql(`SELECT count(*) FROM atividades a JOIN status_atividade s ON s."Id"=a."StatusId" WHERE s."Codigo"='ATRASADA' AND a."CriadoEm" >= now() - interval '365 days';`));
  const concl = Number(sql(`SELECT count(*) FROM atividades a JOIN status_atividade s ON s."Id"=a."StatusId" WHERE s."Codigo"='CONCL' AND a."CriadoEm" >= now() - interval '365 days';`));
  const negadas = Number(sql(`SELECT count(*) FROM atividades a JOIN status_atividade s ON s."Id"=a."StatusId" WHERE s."Codigo"='NEGADA' AND a."CriadoEm" >= now() - interval '365 days';`));
  const emAnd = Number(sql(`SELECT count(*) FROM atividades a JOIN status_atividade s ON s."Id"=a."StatusId" WHERE s."Codigo" IN ('PROG','EM_EXEC') AND a."CriadoEm" >= now() - interval '365 days';`));
  const taxaEsperada = (total - negadas) === 0 ? 0 : Math.round(concl * 1000 / (total - negadas)) / 10;
  console.log(`[Banco] total=${total} emAndamento=${emAnd} concluídas=${concl} atrasadas=${atras} negadas=${negadas}`);
  console.log('[Painel] cards:', JSON.stringify(valores));
  console.log('[Confere] em andamento:', Number(valores[0]) === emAnd,
    '| atrasadas:', Number(valores[3]) === atras,
    '| taxa de conclusão:', valores[1].replace(',', '.') === `${taxaEsperada}%`, `(esperado ${taxaEsperada}%)`);
  await page.screenshot({ path: path.join(OUT, '20-indicadores.png'), fullPage: true });

  // Troca de período
  await page.goto(`${BASE}/Indicadores?dias=7`);
  console.log('[Período] 7 dias:', (await page.textContent('h2')).trim());
  await page.goto(`${BASE}/Indicadores?dias=abc`);
  console.log('[Período] valor inválido cai para 30:', (await page.textContent('h2')).includes('30'));
  await page.goto(`${BASE}/Indicadores?dias=99999`);
  console.log('[Período] valor fora da lista cai para 30:', (await page.textContent('h2')).includes('30'));

  console.log('[JS] erros de página/alertas (esperado nenhum):', JSON.stringify(erros));

  // Servidor não acessa
  await page.goto(`${BASE}/Usuarios/Novo`);
  const login = 'si' + Date.now().toString().slice(-5);
  await page.fill('#NomeCompleto', 'Servidor Indicadores'); await page.fill('#Login', login);
  await page.fill('#Email', `${login}@teste.local`); await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123'); await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  await page.click('button[aria-label="Menu do usuário"]'); await page.click('button:has-text("Sair")');
  await page.waitForLoadState('networkidle');
  await entrar(page, login, 'Servidor@123');
  await page.goto(`${BASE}/Indicadores`);
  console.log('[Servidor] bloqueado:', page.url().includes('AcessoNegado'));
  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
