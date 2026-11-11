// E2E: CRUD de usuários + permissões por perfil.
const { chromium } = require('playwright');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const sufixo = Date.now().toString().slice(-6);
const login = `gestor${sufixo}`;

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

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

  await entrar(page, 'admin', 'Admin@123');

  // 1) Admin cria um gestor
  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', `Gestor Teste ${sufixo}`);
  await page.fill('#Login', login);
  await page.fill('#Email', `${login}@teste.local`);
  await page.selectOption('#Perfil', { label: 'Gestor' });
  await page.fill('#Senha', 'curta');
  await page.click('button:has-text("Salvar")');
  await page.waitForTimeout(500);
  const bloqueou = page.url().includes('/Usuarios/Novo')
    && (await page.$$eval('span.text-danger', es => es.some(e => e.textContent.includes('8 caracteres'))));
  console.log('Senha fraca bloqueada:', bloqueou);

  // Recarrega o formulário: o jQuery validation só revalida em keyup,
  // então reaproveitar o form após um bloqueio travaria o próximo submit.
  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', `Gestor Teste ${sufixo}`);
  await page.fill('#Login', login);
  await page.fill('#Email', `${login}@teste.local`);
  await page.selectOption('#Perfil', { label: 'Gestor' });
  await page.fill('#Senha', 'Gestor@123');
  await page.click('button:has-text("Salvar")');
  await page.waitForLoadState('networkidle');
  console.log('Criação ->', (await page.textContent('.alert-success')).trim());
  await page.screenshot({ path: path.join(OUT, '05-usuarios-lista.png') });

  // 2) Login duplicado é recusado
  await page.goto(`${BASE}/Usuarios/Novo`);
  await page.fill('#NomeCompleto', 'Duplicado');
  await page.fill('#Login', login);
  await page.fill('#Email', `outro${sufixo}@teste.local`);
  await page.fill('#Senha', 'Gestor@123');
  await page.click('button:has-text("Salvar")');
  console.log('Duplicado ->', (await page.textContent('.alert-danger')).trim());
  await sair(page);
  // 3) Gestor entra e vê menu sem Administração
  await entrar(page, login, 'Gestor@123');
  const menuGestor = await page.$$eval('.zt-menu-link', els => els.map(e => e.textContent.trim()));
  console.log('Menu do gestor:', menuGestor.join(' | '));

  // 4) Gestor não acessa /Usuarios
  await page.goto(`${BASE}/Usuarios`);
  console.log('Gestor em /Usuarios ->', page.url());
  await page.screenshot({ path: path.join(OUT, '06-acesso-negado.png') });
  await sair(page);

  // 5) Admin desativa o gestor
  await entrar(page, 'admin', 'Admin@123');
  await page.goto(`${BASE}/Usuarios?busca=${login}`);
  await page.click('button:has-text("Desativar")');
  await page.waitForLoadState('networkidle');
  console.log('Desativação ->', (await page.textContent('.alert-success')).trim());

  // 6) Admin não pode desativar a si mesmo (sem botão na própria linha)
  await page.goto(`${BASE}/Usuarios?busca=admin`);
  const temBotaoProprio = await page.$$eval('tbody tr', trs =>
    trs.some(tr => tr.textContent.includes('admin') && tr.querySelector('button')));
  console.log('Botão desativar na própria conta:', temBotaoProprio);
  await sair(page);

  // 7) Usuário desativado não consegue entrar
  await entrar(page, login, 'Gestor@123');
  const erroLogin = await page.textContent('.alert-danger').catch(() => null);
  console.log('Login do desativado ->', erroLogin ? erroLogin.trim() : 'ENTROU (FALHA)');

  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });