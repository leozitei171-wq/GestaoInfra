// E2E: Fornecedores, Tipos de medição e Tipos de serviço.
const { chromium } = require('playwright');
const BASE = 'http://localhost:5080';
const s = Date.now().toString().slice(-5);
const CNPJ_OK = '11.222.333/0001-81'; // dígitos verificadores válidos

async function alerta(page, tipo) {
  const t = await page.textContent(`.alert-${tipo}`).catch(() => null);
  return t ? t.trim().replace(/\s+/g, ' ') : null;
}
const erros = (page) => page.$$eval('span.text-danger', es => es.map(e => e.textContent.trim()).filter(Boolean));

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await page.goto(`${BASE}/Conta/Login`);
  await page.fill('#Login', 'admin'); await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');

  // ---- Fornecedores ----
  const salvarFornecedor = async (razao, cnpj) => {
    await page.goto(`${BASE}/Fornecedores/Novo`);
    await page.fill('#RazaoSocial', razao);
    await page.fill('#Cnpj', cnpj);
    await page.click('button:has-text("Salvar")');
    await page.waitForLoadState('networkidle');
  };

  await salvarFornecedor('Empresa Inválida', '11.222.333/0001-82');
  console.log('[Fornecedor] CNPJ com dígito errado ->', JSON.stringify(await erros(page)));
  await salvarFornecedor('Empresa Repetida', '11.111.111/1111-11');
  console.log('[Fornecedor] CNPJ repetido (1111...) ->', JSON.stringify(await erros(page)));

  await page.goto(`${BASE}/Fornecedores/Novo`);
  await page.fill('#Cnpj', '112223330001');
  console.log('[Fornecedor] máscara aplicada ao digitar:', await page.inputValue('#Cnpj'));
  await salvarFornecedor(`Asfaltos Teste ${s}`, CNPJ_OK);
  console.log('[Fornecedor] criar ->', await alerta(page, 'success'));
  console.log('[Fornecedor] CNPJ exibido formatado na lista:', (await page.textContent('tbody')).includes(CNPJ_OK));
  await salvarFornecedor('Outra Razão', '11222333000181'); // mesmo CNPJ sem máscara
  console.log('[Fornecedor] CNPJ duplicado (sem máscara) ->', JSON.stringify(await erros(page)));

  // ---- Tipos de medição / serviço (CRUD genérico) ----
  for (const [rota, nome, codigo, longo] of [
    ['TiposMedicao', 'medição', `M${s}`, 'X'.repeat(21)],
    ['TiposServico', 'serviço', `SRV${s}`, 'Y'.repeat(41)]
  ]) {
    await page.goto(`${BASE}/${rota}/Novo`);
    await page.fill('#Codigo', codigo); await page.fill('#Descricao', `Teste ${nome}`);
    await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
    console.log(`[${rota}] criar ->`, await alerta(page, 'success'));

    await page.goto(`${BASE}/${rota}/Novo`);
    await page.fill('#Codigo', codigo.toLowerCase()); await page.fill('#Descricao', 'duplicado');
    await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
    console.log(`[${rota}] código duplicado (minúsculo) ->`, JSON.stringify(await erros(page)));

    await page.goto(`${BASE}/${rota}/Novo`);
    await page.evaluate(() => document.getElementById('Codigo').removeAttribute('maxlength'));
    await page.fill('#Codigo', longo); await page.fill('#Descricao', 'longo');
    await page.click('button:has-text("Salvar")'); await page.waitForLoadState('networkidle');
    console.log(`[${rota}] código maior que o limite ->`, JSON.stringify(await erros(page)));

    await page.goto(`${BASE}/${rota}`);
    page.once('dialog', d => d.accept());
    await page.locator('tr', { hasText: codigo }).locator('button:has-text("Excluir")').click();
    await page.waitForLoadState('networkidle');
    console.log(`[${rota}] excluir ->`, await alerta(page, 'success'));
  }

  // ---- Perfil servidor ----
  await page.goto(`${BASE}/Usuarios/Novo`);
  const login = 'sc' + s;
  await page.fill('#NomeCompleto', 'Servidor Cadastros'); await page.fill('#Login', login);
  await page.fill('#Email', `${login}@teste.local`); await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123'); await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  await page.click('button[aria-label="Menu do usuário"]'); await page.click('button:has-text("Sair")');
  await page.waitForLoadState('networkidle');
  await page.fill('#Login', login); await page.fill('#Senha', 'Servidor@123');
  await page.click('button[type=submit]'); await page.waitForLoadState('networkidle');
  for (const rota of ['Fornecedores', 'TiposMedicao', 'TiposServico']) {
    await page.goto(`${BASE}/${rota}`);
    console.log(`[Servidor] ${rota} bloqueado:`, page.url().includes('AcessoNegado'));
  }
  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
