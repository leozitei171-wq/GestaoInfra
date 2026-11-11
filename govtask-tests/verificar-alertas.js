// E2E: lançamento de serviços na atividade, consumo do contrato e alertas.
const { chromium } = require('playwright');
const { execSync } = require('child_process');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const s = Date.now().toString().slice(-5);

const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();
const msg = async (p, t) => ((await p.textContent(`.alert-${t}`).catch(() => null)) || '').trim().replace(/\s+/g, ' ');

function criarContrato(codigo, fimOffset, qtd, valor) {
  sql(`INSERT INTO contratos ("Codigo","Descricao","FornecedorId","DataInicio","DataFim","ValorTotal","Ativo","PrefeituraId","CriadoEm")
       SELECT '${codigo}','Contrato de alertas',(SELECT "Id" FROM fornecedores ORDER BY "Id" LIMIT 1),CURRENT_DATE-10,CURRENT_DATE+${fimOffset},${qtd * valor},true,1,now();
       INSERT INTO contrato_servicos ("ContratoId","TipoServicoId","TipoMedicaoId","Quantidade","ValorUnitario","PrefeituraId","CriadoEm")
       SELECT c."Id",(SELECT "Id" FROM tipos_servico ORDER BY "Id" LIMIT 1),(SELECT "Id" FROM tipos_medicao ORDER BY "Id" LIMIT 1),${qtd},${valor},1,now()
       FROM contratos c WHERE c."Codigo"='${codigo}';`);
}
const alertasDe = (codigo) => sql(`SELECT string_agg(a."Nivel" || ':' || a."PercentualUso", ' | ' ORDER BY a."Id")
  FROM contrato_alertas a JOIN contratos c ON c."Id"=a."ContratoId" WHERE c."Codigo"='${codigo}';`);

async function lancar(page, codigo, qtd) {
  const valorOpcao = await page.evaluate((cod) => {
    const o = [...document.querySelectorAll('#srv-item option')].find(x => x.textContent.startsWith(cod + ' ·'));
    return o ? o.value : null;
  }, codigo);
  if (!valorOpcao) return 'SEM-OPCAO';
  await page.selectOption('#srv-item', valorOpcao);
  await page.fill('input[name=quantidade]', qtd);
  await page.click('button:has-text("Lançar")'); await page.waitForLoadState('networkidle');
  return (await msg(page, 'success')) || (await msg(page, 'danger'));
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', 'admin'); await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');

  const ctUso = `ALT-${s}`;      // 10 un x R$100 = R$1000, vence longe
  const ctVenc = `VNC-${s}`;     // vence em 10 dias
  criarContrato(ctUso, 200, 10, 100);
  criarContrato(ctVenc, 10, 5, 50);

  // Atividade para receber os serviços
  await page.goto(`${BASE}/Atividades/Nova`);
  await page.selectOption('#TipoAtividadeId', { label: 'CEC — Capina em canteiros' });
  await page.click('[data-step="1"] [data-next="2"]');
  await page.selectOption('#PrioridadeId', { label: 'NORM — Normal' });
  await page.fill('#Descricao', `Atividade de alertas ${s}`);
  await page.click('[data-step="2"] [data-next="3"]'); await page.click('#btn-enviar');
  await page.waitForLoadState('networkidle');
  const idAtv = page.url().match(/Detalhe\/(\d+)/)[1];

  // 70% — sem alerta
  console.log('[Lançar 7 un] ->', await lancar(page, ctUso, '7'));
  console.log('[Alerta] a 70% (esperado vazio):', JSON.stringify(alertasDe(ctUso)));
  console.log('[Custo] exibido na atividade (R$ 700,00):', (await page.textContent('.card-header:has-text("Serviços de contrato")')).replace(/\s+/g, ' ').trim());

  // 85% — atenção
  console.log('[Lançar 1,5 un com vírgula] ->', await lancar(page, ctUso, '1,5'));
  console.log('[Alerta] a 85% (esperado atencao:85.00):', alertasDe(ctUso));

  // lançar mais que o saldo (1,5 restantes)
  console.log('[Lançar 2 un > saldo] ->', await lancar(page, ctUso, '2'));
  console.log('[Excesso] consumo no banco segue 8.5 un:', sql(`SELECT sum(a."Quantidade") FROM atividade_servicos a JOIN contrato_servicos cs ON cs."Id"=a."ContratoServicoId" JOIN contratos c ON c."Id"=cs."ContratoId" WHERE c."Codigo"='${ctUso}';`));

  // 100% — crítico
  console.log('[Lançar 1,5 un -> 100%] ->', await lancar(page, ctUso, '1.5'));
  console.log('[Alerta] a 100% (atencao + critico):', alertasDe(ctUso));
  console.log('[Opção] contrato esgotado some da lista:', (await lancar(page, ctUso, '1')) === 'SEM-OPCAO');

  // vencimento
  console.log('[Lançar no contrato que vence em 10 dias] ->', await lancar(page, ctVenc, '1'));
  console.log('[Alerta] vencimento:', alertasDe(ctVenc));

  // ---- Tela de alertas ----
  await page.goto(`${BASE}/AlertasContratos`);
  const linhas = await page.$$eval('tbody tr', trs => trs.map(t => t.textContent.replace(/\s+/g, ' ').trim()));
  const meus = linhas.filter(l => l.includes(ctUso) || l.includes(ctVenc));
  console.log('[Tela] alertas dos meus contratos (esperado 3):', meus.length);
  console.log('[Tela] rótulos:', ['Atenção', 'Crítico', 'Vencimento'].map(r => `${r}=${meus.some(l => l.includes(r))}`).join(' '));
  console.log('[Tela] contadores:', (await page.textContent('p.text-muted')).replace(/\s+/g, ' ').trim());
  await page.screenshot({ path: path.join(OUT, '24-alertas.png') });

  await page.locator('tr', { hasText: ctVenc }).locator('input[type=checkbox]').check();
  await page.click('button:has-text("Marcar selecionados como lidos")'); await page.waitForLoadState('networkidle');
  console.log('[Marcar lido] ->', await msg(page, 'success'));
  console.log('[Marcar lido] no banco:', sql(`SELECT "Lido" FROM contrato_alertas a JOIN contratos c ON c."Id"=a."ContratoId" WHERE c."Codigo"='${ctVenc}';`));
  await page.goto(`${BASE}/AlertasContratos`);
  await page.click('button:has-text("Marcar selecionados como lidos")'); await page.waitForLoadState('networkidle');
  console.log('[Marcar lido] sem seleção ->', await msg(page, 'danger'));

  // ---- Consumo no contrato ----
  await page.goto(`${BASE}/Contratos`);
  console.log('[Contratos] uso exibido:', (await page.locator('tr', { hasText: ctUso }).textContent()).replace(/\s+/g, ' ').match(/\d+[,.]?\d*%/)?.[0], '(100%)');

  // ---- Servidor não lança serviço ----
  await page.goto(`${BASE}/Usuarios/Novo`);
  const login = 'sa' + s;
  await page.fill('#NomeCompleto', 'Servidor Alertas'); await page.fill('#Login', login);
  await page.fill('#Email', `${login}@teste.local`); await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123'); await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
  await page.click('button[aria-label="Menu do usuário"]'); await page.click('button:has-text("Sair")'); await page.waitForLoadState('networkidle');
  await page.fill('#Login', login); await page.fill('#Senha', 'Servidor@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');
  await page.goto(`${BASE}/Atividades/Detalhe/${idAtv}`);
  console.log('[Servidor] vê formulário de lançamento:', (await page.locator('#srv-item').count()) > 0, '(esperado false)');
  const antes = sql('SELECT count(*) FROM atividade_servicos;');
  await page.evaluate(async (id) => {
    const t = (await (await fetch(location.href)).text()).match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
    await fetch(`/Atividades/LancarServico/${id}`, { method: 'POST', redirect: 'manual',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ contratoServicoId: '1', quantidade: '1', __RequestVerificationToken: t }) });
  }, idAtv);
  console.log('[Servidor] POST direto não grava:', antes === sql('SELECT count(*) FROM atividade_servicos;'));
  for (const rota of ['AlertasContratos', 'Contratos']) {
    await page.goto(`${BASE}/${rota}`);
    console.log(`[Servidor] ${rota} bloqueado:`, page.url().includes('AcessoNegado'));
  }
  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
