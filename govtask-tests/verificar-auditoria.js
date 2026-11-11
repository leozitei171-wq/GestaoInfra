// E2E: Auditoria — registro de escritas, eventos de acesso, vazamento de senha e acesso à tela.
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
async function sair(page) {
  await page.click('button[aria-label="Menu do usuário"]');
  await page.click('button:has-text("Sair")');
  await page.waitForLoadState('networkidle');
}
async function linhasAuditoria(page, query) {
  await page.goto(`${BASE}/Auditoria?${query}`);
  return page.$$eval('tbody tr', trs => trs.map(tr => tr.textContent.replace(/\s+/g, ' ').trim()));
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

  // Tentativa de login com senha errada (usuário inexistente e existente)
  await entrar(page, `fantasma${s}`, 'qualquer');
  await entrar(page, 'admin', 'SenhaErrada!1');
  await entrar(page, 'admin', 'Admin@123');

  // Escritas auditadas: setor (criar/alterar/excluir) e usuário (criar com senha)
  await page.goto(`${BASE}/Setores/Novo`);
  await page.fill('#Codigo', `AUD_${s}`);
  await page.fill('#Descricao', `Setor auditado ${s}`);
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');

  await page.locator('tr', { hasText: `AUD_${s}` }).locator('a:has-text("Editar")').click();
  await page.fill('#Descricao', `Setor auditado ${s} EDITADO`);
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');

  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', `Gestor Auditoria ${s}`);
  await page.fill('#Login', `gaud${s}`);
  await page.fill('#Email', `gaud${s}@teste.local`);
  await page.selectOption('#Perfil', { label: 'Gestor' });
  await page.fill('#Senha', 'SenhaSecreta@123');
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');

  await page.goto(`${BASE}/Setores`);
  page.once('dialog', d => d.accept());
  await page.locator('tr', { hasText: `AUD_${s}` }).locator('button:has-text("Excluir")').click();
  await page.waitForLoadState('networkidle');

  // ---- Conferência ----
  const setor = await linhasAuditoria(page, 'Entidade=Setor');
  const meus = setor.filter(l => l.includes(`AUD_${s}`) || l.includes(`${s}`));
  console.log('[Setor] ações registradas:', ['Criou', 'Alterou', 'Excluiu'].map(a => `${a}=${setor.some(l => l.includes(a))}`).join(' '));
  console.log('[Setor] alteração mostra de/para:',
    setor.some(l => l.includes('Alterou') && l.includes('"de"') && l.includes('"para"') && l.includes('EDITADO')));

  await page.goto(`${BASE}/Auditoria?Entidade=Usuario`);
  const textoUsuario = await page.textContent('tbody');
  console.log('[Usuário] criação registrada:', textoUsuario.includes('Criou'));
  console.log('[Segurança] senha em texto NÃO aparece:', !textoUsuario.includes('SenhaSecreta'));
  console.log('[Segurança] hash NÃO aparece:', !/AQAAAA/.test(textoUsuario) && !textoUsuario.includes('PasswordHash'));

  const sessao = (await linhasAuditoria(page, 'Entidade=Sessao')).join('\n');
  console.log('[Acesso] LoginFalhou (usuário inexistente):', sessao.includes('usuário inexistente'));
  console.log('[Acesso] LoginFalhou (senha incorreta):', sessao.includes('senha incorreta'));
  console.log('[Acesso] Login bem-sucedido:', /\sLogin\s/.test(sessao));
  console.log('[Acesso] senha digitada NÃO aparece:', !sessao.includes('SenhaErrada') && !sessao.includes('qualquer'));

  // Filtros
  const soAlterou = await linhasAuditoria(page, 'Acao=alterou');
  console.log('[Filtro Ação contém "alterou"] só Alterou:', soAlterou.length > 0 && soAlterou.every(l => l.includes('Alterou')));
  const curinga = await linhasAuditoria(page, 'Acao=%25');
  console.log('[Filtro curinga % literal] não casa tudo (esperado 0 linhas de dados):', curinga.filter(l => !l.includes('Nenhum')).length === 0 || curinga.length === 1);
  await page.goto(`${BASE}/Auditoria`);
  await page.screenshot({ path: path.join(OUT, '15-auditoria.png'), fullPage: true });

  // ---- Perfis ----
  await sair(page);
  await entrar(page, `gaud${s}`, 'SenhaSecreta@123');
  await page.goto(`${BASE}/Auditoria`);
  console.log('[Gestor] acessa auditoria:', page.url().endsWith('/Auditoria'));
  await sair(page);

  const srvLogin = `saud${s}`;
  await entrar(page, 'admin', 'Admin@123');
  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', `Servidor Aud ${s}`);
  await page.fill('#Login', srvLogin);
  await page.fill('#Email', `${srvLogin}@teste.local`);
  await page.selectOption('#Perfil', { label: 'Servidor' });
  await page.fill('#Senha', 'Servidor@123');
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  await sair(page);
  await entrar(page, srvLogin, 'Servidor@123');
  await page.goto(`${BASE}/Auditoria`);
  console.log('[Servidor] bloqueado na auditoria:', page.url().includes('AcessoNegado'));

  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
