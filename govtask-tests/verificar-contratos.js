// E2E: Contratos — itens, valor total, PDF, validações e edição.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const TMP = path.join(__dirname, 'tmp-contrato');
fs.mkdirSync(TMP, { recursive: true });
const pdfOk = path.join(TMP, 'contrato.pdf');
const pdfFalso = path.join(TMP, 'falso.pdf');
fs.writeFileSync(pdfOk, '%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF');
fs.writeFileSync(pdfFalso, 'isto nao e um pdf');

const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();
const s = Date.now().toString().slice(-5);
const iso = (d) => { const x = new Date(); x.setDate(x.getDate() + d); return x.toISOString().slice(0, 10); };
const alerta = async (p, t) => ((await p.textContent(`.alert-${t}`).catch(() => null)) || '').trim().replace(/\s+/g, ' ');

async function preencherCabecalho(page, codigo, ini, fim) {
  await page.fill('#Codigo', codigo);
  await page.fill('#Descricao', `Contrato de teste ${s}`);
  await page.selectOption('#FornecedorId', { index: 1 });
  await page.fill('#DataInicio', ini);
  await page.fill('#DataFim', fim);
}
async function linha(page, n, { servico, medicao, qtd, valor, limite }) {
  const l = page.locator('.item-linha').nth(n);
  await l.locator('select').nth(0).selectOption({ label: servico });
  await l.locator('select').nth(1).selectOption({ label: medicao });
  await l.locator('input[name$=".Quantidade"]').fill(qtd);
  await l.locator('input[name$=".ValorUnitario"]').fill(valor);
  if (limite) await l.locator('input[name$=".Limite"]').fill(limite);
}
const erros = (p) => p.$$eval('.alert-danger li, .alert-danger, span.text-danger', es => es.map(e => e.textContent.trim().replace(/\s+/g, ' ')).filter(Boolean));

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', 'admin'); await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');

  // Pré-requisitos: fornecedor, 2 serviços e 1 medição
  const cnpj = '11222333000181';
  if (sql(`SELECT count(*) FROM fornecedores WHERE "Cnpj"='${cnpj}';`) === '0') {
    await page.goto(`${BASE}/Fornecedores/Novo`);
    await page.fill('#RazaoSocial', 'Fornecedor Contratos'); await page.fill('#Cnpj', cnpj);
    await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
  }
  for (const [rota, cod, desc] of [['TiposServico', `CAPINA${s}`, 'Capina manual'], ['TiposServico', `TAPA${s}`, 'Tapa-buraco'], ['TiposMedicao', `M2${s}`, 'Metro quadrado']]) {
    await page.goto(`${BASE}/${rota}/Novo`);
    await page.fill('#Codigo', cod); await page.fill('#Descricao', desc);
    await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
  }

  // ---- Criação válida ----
  const cod = `CT-${s}`;
  await page.goto(`${BASE}/Contratos/Novo`);
  await preencherCabecalho(page, cod, iso(-10), iso(300));
  await linha(page, 0, { servico: `CAPINA${s} — Capina manual`, medicao: `M2${s} — Metro quadrado`, qtd: '50', valor: '12,50', limite: '60' });
  await page.click('#btn-add-item');
  await linha(page, 1, { servico: `TAPA${s} — Tapa-buraco`, medicao: `M2${s} — Metro quadrado`, qtd: '100', valor: '3.00' });
  console.log('[Tela] total calculado no navegador:', (await page.textContent('#total-contrato')).replace(/\s/g, ' '));
  await page.setInputFiles('#ArquivoPdf', pdfOk);
  await page.screenshot({ path: path.join(OUT, '21-contrato-form.png'), fullPage: true });
  await page.click('button:has-text("Salvar contrato")'); await page.waitForLoadState('networkidle');
  console.log('[Criar] ->', await alerta(page, 'success'));
  const total = sql(`SELECT "ValorTotal" FROM contratos WHERE "Codigo"='${cod}';`);
  console.log('[Criar] ValorTotal no banco (esperado 925.00):', total);
  console.log('[Criar] itens no banco (esperado 2):', sql(`SELECT count(*) FROM contrato_servicos cs JOIN contratos c ON c."Id"=cs."ContratoId" WHERE c."Codigo"='${cod}';`));
  await page.screenshot({ path: path.join(OUT, '22-contrato-detalhe.png'), fullPage: true });

  // PDF servido
  const idContrato = sql(`SELECT "Id" FROM contratos WHERE "Codigo"='${cod}';`);
  const r = await page.request.get(`${BASE}/Contratos/Pdf/${idContrato}`);
  console.log('[PDF] logado ->', r.status(), r.headers()['content-type']);

  // ---- Validações ----
  const tentar = async (descricao, ini, fim, itens, pdf) => {
    await page.goto(`${BASE}/Contratos/Novo`);
    await preencherCabecalho(page, `X-${Date.now().toString().slice(-6)}`, ini, fim);
    for (let n = 0; n < itens.length; n++) {
      if (n > 0) await page.click('#btn-add-item');
      await linha(page, n, itens[n]);
    }
    if (pdf) await page.setInputFiles('#ArquivoPdf', pdf);
    await page.click('button:has-text("Salvar contrato")'); await page.waitForLoadState('networkidle');
    const ok = page.url().includes('/Detalhe');
    console.log(`[Validação] ${descricao} -> ${ok ? 'ACEITO (FALHA)' : 'recusado'}:`, JSON.stringify((await erros(page)).slice(0, 2)));
  };
  const servA = { servico: `CAPINA${s} — Capina manual`, medicao: `M2${s} — Metro quadrado`, qtd: '10', valor: '5' };
  await tentar('vigência invertida', iso(10), iso(1), [servA]);
  await tentar('valor unitário negativo', iso(0), iso(30), [{ ...servA, valor: '-5' }]);
  await tentar('quantidade zero', iso(0), iso(30), [{ ...servA, qtd: '0' }]);
  await tentar('mesmo serviço duas vezes', iso(0), iso(30), [servA, servA]);
  await tentar('PDF falso', iso(0), iso(30), [servA], pdfFalso);

  // código duplicado
  await page.goto(`${BASE}/Contratos/Novo`);
  await preencherCabecalho(page, cod.toLowerCase(), iso(0), iso(30));
  await linha(page, 0, servA);
  await page.click('button:has-text("Salvar contrato")'); await page.waitForLoadState('networkidle');
  console.log('[Validação] código duplicado ->', JSON.stringify((await erros(page)).slice(0, 2)));
  console.log('[Validação] nada indevido foi criado (esperado 1 contrato CT-):', sql(`SELECT count(*) FROM contratos WHERE "Codigo" LIKE 'CT-${s}' OR "Codigo" LIKE 'X-%';`));
  console.log('[Validação] só 1 PDF em disco (o do contrato válido):', fs.readdirSync(path.join(__dirname, '..', 'src', 'SistemaGestaoInfra.Web', 'App_Data', 'uploads', 'contratos')).length);

  // ---- Edição: alterar quantidade/valor e recalcular ----
  await page.goto(`${BASE}/Contratos/Editar/${idContrato}`);
  await page.locator('.item-linha').nth(0).locator('input[name$=".Quantidade"]').fill('80');
  await page.click('button:has-text("Salvar contrato")'); await page.waitForLoadState('networkidle');
  console.log('[Editar] novo ValorTotal (80x12,50 + 100x3 = 1300.00):', sql(`SELECT "ValorTotal" FROM contratos WHERE "Id"=${idContrato};`));

  // ---- Item consumido não pode ser removido nem ter a quantidade abaixo do executado ----
  sql(`INSERT INTO atividade_servicos ("AtividadeId","ContratoServicoId","Quantidade","ValorUnitario","PrefeituraId","CriadoEm")
       SELECT (SELECT "Id" FROM atividades ORDER BY "Id" LIMIT 1), cs."Id", 30, cs."ValorUnitario", cs."PrefeituraId", now()
       FROM contrato_servicos cs WHERE cs."ContratoId"=${idContrato} ORDER BY cs."Id" LIMIT 1;`);
  await page.goto(`${BASE}/Contratos/Editar/${idContrato}`);
  await page.locator('.item-linha').nth(0).locator('input[name$=".Quantidade"]').fill('10');
  await page.click('button:has-text("Salvar contrato")'); await page.waitForLoadState('networkidle');
  console.log('[Consumo] quantidade abaixo do executado ->', JSON.stringify((await erros(page)).slice(0, 2)));
  await page.goto(`${BASE}/Contratos/Editar/${idContrato}`);
  await page.locator('.item-linha').nth(0).locator('.btn-remover').click();
  await page.click('button:has-text("Salvar contrato")'); await page.waitForLoadState('networkidle');
  console.log('[Consumo] remover item já usado ->', JSON.stringify((await erros(page)).slice(0, 2)));

  await page.goto(`${BASE}/Contratos`);
  page.once('dialog', d => d.accept());
  await page.locator('tr', { hasText: cod }).locator('button[aria-label^="Excluir"]').click();
  await page.waitForLoadState('networkidle');
  console.log('[Consumo] excluir contrato já usado ->', await alerta(page, 'danger'));
  console.log('[Lista] uso % exibido:', (await page.locator('tr', { hasText: cod }).textContent()).replace(/\s+/g, ' ').match(/\d+[,.]?\d*%/)?.[0]);
  await page.screenshot({ path: path.join(OUT, '23-contratos-lista.png') });

  // ---- Segurança do PDF ----
  const anon = await browser.newContext();
  const ra = await anon.request.get(`${BASE}/Contratos/Pdf/${idContrato}`, { maxRedirects: 0 });
  console.log('[PDF] sem login ->', ra.status(), '(esperado 302)');

  await browser.close();
  fs.rmSync(TMP, { recursive: true, force: true });
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
