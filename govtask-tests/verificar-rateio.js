// E2E: Rateio proporcional — divisão exata, modos, adulteração, tipo de custo e permissões.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const s = Date.now().toString().slice(-5);

const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();
const msg = async (p, t) => ((await p.textContent(`.alert-${t}`).catch(() => null)) || '').trim().replace(/\s+/g, ' ');

(async () => {
  // ---- Dados de apoio: 3 contratos de valor igual no setor INFRA_SLU ----
  for (const n of [1, 2, 3]) {
    sql(`INSERT INTO contratos ("Codigo","Descricao","FornecedorId","SetorId","DataInicio","DataFim","ValorTotal","Ativo","PrefeituraId","CriadoEm")
         SELECT 'RT${n}-${s}','Contrato de rateio ${n}',(SELECT "Id" FROM fornecedores ORDER BY "Id" LIMIT 1),
                (SELECT "Id" FROM setores WHERE "Codigo"='INFRA_SLU'),CURRENT_DATE-5,CURRENT_DATE+100,1000,true,1,now();`);
  }
  // Uma atividade extra com custo conhecido (R$ 300) no mesmo setor da primeira atividade com serviços
  sql(`INSERT INTO atividades ("Descricao","TipoAtividadeId","SetorId","PrioridadeId","StatusId","Progresso","Requerimento","PrefeituraId","CriadoEm")
       SELECT 'Atividade rateio ${s}',"TipoAtividadeId","SetorId","PrioridadeId","StatusId",0,false,"PrefeituraId",now()
       FROM atividades WHERE "SetorId" IS NOT NULL ORDER BY "Id" LIMIT 1;
       INSERT INTO atividade_servicos ("AtividadeId","ContratoServicoId","Quantidade","ValorUnitario","PrefeituraId","CriadoEm")
       SELECT (SELECT "Id" FROM atividades WHERE "Descricao"='Atividade rateio ${s}'),
              (SELECT "Id" FROM contrato_servicos ORDER BY "Id" LIMIT 1),3,100,1,now();`);

  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', 'admin'); await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');

  // ---- Tipo de custo ----
  await page.goto(`${BASE}/TiposCusto/Novo`);
  await page.fill('#Codigo', `ADM${s}`); await page.fill('#Descricao', 'Custo administrativo');
  await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
  console.log('[Tipo de custo] criar ->', await msg(page, 'success'));

  // ---- Rateio por contrato: 100,00 / 3 ----
  await page.goto(`${BASE}/Rateios/Novo`);
  await page.fill('#valor-total', '100,00');
  await page.fill('#Descricao', `Rateio por contrato ${s}`);
  await page.selectOption('#TipoCustoId', { label: `ADM${s} — Custo administrativo` });
  await page.check('label:has-text("INFRA_SLU") >> xpath=preceding-sibling::input');
  await page.check('#modo-ctr', { force: true }).catch(async () => page.click('label[for="modo-ctr"]'));
  await page.waitForSelector(`tr:has-text("RT1-${s}")`, { timeout: 8000 });
  const linhasContrato = await page.locator('#itens tr').count();
  console.log('[Contrato] itens elegíveis listados (esperado 3):', linhasContrato);
  await page.click('#btn-todos');
  const previa = await page.$$eval('#itens .previa', cs => cs.map(c => c.textContent.replace(/\s/g, ' ').trim()));
  console.log('[Contrato] prévia na tela:', JSON.stringify(previa));
  await page.screenshot({ path: path.join(OUT, '25-rateio-form.png'), fullPage: true });
  await page.click('#btn-gerar'); await page.waitForLoadState('networkidle');
  console.log('[Contrato] criar ->', await msg(page, 'success'));
  const idCtr = page.url().match(/Detalhe\/(\d+)/)[1];
  const valores = sql(`SELECT string_agg("Valor"::text, ' | ' ORDER BY "Valor" DESC, "Id") FROM rateio_itens WHERE "RateioId"=${idCtr};`);
  console.log('[Contrato] valores no banco:', valores, '| soma:', sql(`SELECT sum("Valor") FROM rateio_itens WHERE "RateioId"=${idCtr};`), '(esperado 100.00)');
  console.log('[Contrato] % de cada um:', sql(`SELECT string_agg("Percentual"::text, ' | ') FROM rateio_itens WHERE "RateioId"=${idCtr};`));
  await page.screenshot({ path: path.join(OUT, '26-rateio-detalhe.png'), fullPage: true });

  // ---- Rateio por atividade ----
  await page.goto(`${BASE}/Rateios/Novo`);
  await page.fill('#valor-total', '1.000,01');
  await page.fill('#Descricao', `Rateio por atividade ${s}`);
  await page.check('label:has-text("INFRA_SLU") >> xpath=preceding-sibling::input');
  await page.waitForSelector('#itens tr', { timeout: 8000 });
  const nAtv = await page.locator('#itens tr').count();
  console.log('[Atividade] atividades com custo listadas (>=2):', nAtv);
  await page.click('#btn-todos');
  await page.click('#btn-gerar'); await page.waitForLoadState('networkidle');
  console.log('[Atividade] criar ->', await msg(page, 'success'));
  const idAtv = page.url().match(/Detalhe\/(\d+)/)[1];
  console.log('[Atividade] soma no banco (esperado 1000.01):', sql(`SELECT sum("Valor") FROM rateio_itens WHERE "RateioId"=${idAtv};`));
  console.log('[Atividade] item do rateio tem AtividadeId e não ContratoId:', sql(`SELECT bool_and("AtividadeId" IS NOT NULL AND "ContratoId" IS NULL) FROM rateio_itens WHERE "RateioId"=${idAtv};`));

  // ---- Já rateado + "Sem rateio" ----
  await page.goto(`${BASE}/Rateios/Novo`);
  await page.fill('#valor-total', '50');
  await page.check('label:has-text("INFRA_SLU") >> xpath=preceding-sibling::input');
  await page.click('label[for="modo-ctr"]');
  await page.waitForSelector(`tr:has-text("RT1-${s}")`, { timeout: 8000 });
  console.log('[Já rateado] selo exibido nos 3 contratos:', await page.locator('#itens .badge:has-text("já rateado")').count() >= 3);
  await page.click('#btn-sem-rateio');
  console.log('[Sem rateio] contratos já rateados ficam desmarcados:', await page.locator(`tr:has-text("RT1-${s}") .sel-item:checked`).count() === 0);

  // ---- Adulteração via POST direto ----
  const post = (corpo) => page.evaluate(async (corpo) => {
    const t = (await (await fetch('/Rateios/Novo')).text()).match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    const p = new URLSearchParams(); p.set('__RequestVerificationToken', t);
    for (const [k, v] of corpo) p.append(k, v);
    const r = await fetch('/Rateios/Novo', { method: 'POST', redirect: 'manual', body: p, headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
    return r.type === 'opaqueredirect' ? 'redirect' : r.status;
  }, corpo);
  const antes = sql('SELECT count(*) FROM rateios;');
  const base = [['Descricao', 'adulterado'], ['DataRateio', '2026-10-07'], ['Modo', '1']];
  console.log('[Adulteração] valor negativo ->', await post([...base, ['ValorTotal', '-100'], ['ItemIds', '1']]));
  console.log('[Adulteração] itens inexistentes ->', await post([...base, ['ValorTotal', '100'], ['ItemIds', '999999']]));
  console.log('[Adulteração] sem itens ->', await post([...base, ['ValorTotal', '100']]));
  console.log('[Adulteração] nada foi gravado:', antes === sql('SELECT count(*) FROM rateios;'));
  console.log('[Adulteração] campos Valor/Percentual enviados são ignorados:',
    await post([...base, ['ValorTotal', '10'], ['ItemIds', sql(`SELECT "Id" FROM contratos WHERE "Codigo"='RT1-${s}';`)], ['Itens[0].Valor', '99999']]) === 'redirect'
    && sql('SELECT max("Valor") FROM rateio_itens WHERE "RateioId"=(SELECT max("Id") FROM rateios);') === '10.00');

  // ---- Tipo de custo em uso e exclusão do rateio ----
  await page.goto(`${BASE}/TiposCusto`);
  page.once('dialog', d => d.accept());
  await page.locator('tr', { hasText: `ADM${s}` }).locator('button:has-text("Excluir")').click();
  await page.waitForLoadState('networkidle');
  console.log('[Tipo de custo] em uso não exclui ->', await msg(page, 'danger'));

  await page.goto(`${BASE}/Rateios/Detalhe/${idAtv}`);
  page.once('dialog', d => d.accept());
  await page.click('button:has-text("Excluir rateio")'); await page.waitForLoadState('networkidle');
  console.log('[Excluir] ->', await msg(page, 'success'), '| itens órfãos:', sql(`SELECT count(*) FROM rateio_itens WHERE "RateioId"=${idAtv};`), '(0)');
  console.log('[Auditoria] exclusão registrada:', sql(`SELECT count(*) FROM auditoria WHERE "Entidade"='Rateio' AND "Acao"='Excluiu' AND "EntidadeId"='${idAtv}';`));

  // ---- Servidor ----
  await page.goto(`${BASE}/Usuarios/Novo`);
  const login = 'sr' + s;
  await page.fill('#NomeCompleto', 'Servidor Rateio'); await page.fill('#Login', login);
  await page.fill('#Email', `${login}@teste.local`); await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123'); await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
  await page.click('button[aria-label="Menu do usuário"]'); await page.click('button:has-text("Sair")'); await page.waitForLoadState('networkidle');
  await page.fill('#Login', login); await page.fill('#Senha', 'Servidor@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');
  for (const rota of ['Rateios', 'Rateios/Novo', 'TiposCusto', 'Rateios/Itens?modo=1&setores=0']) {
    await page.goto(`${BASE}/${rota}`);
    console.log(`[Servidor] /${rota} bloqueado:`, page.url().includes('AcessoNegado'));
  }
  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
