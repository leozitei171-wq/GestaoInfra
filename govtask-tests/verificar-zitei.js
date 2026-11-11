// Verificação visual do Zitei local: login + dashboard (desktop e mobile).
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
fs.mkdirSync(OUT, { recursive: true });

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });

  await page.goto(`${BASE}/`);
  console.log('Sem login, redirecionou para:', page.url());
  await page.screenshot({ path: path.join(OUT, '01-login.png') });

  await page.fill('#Login', 'admin');
  await page.fill('#Senha', 'senhaerrada');
  await page.click('button[type=submit]');
  const erro = await page.textContent('.alert-danger').catch(() => null);
  console.log('Senha errada ->', erro ? erro.trim() : 'SEM MENSAGEM');

  await page.fill('#Senha', 'Admin@123');
  await page.click('button[type=submit]');
  await page.waitForLoadState('networkidle');
  console.log('Após login:', page.url(), '| título:', await page.title());
  await page.screenshot({ path: path.join(OUT, '02-dashboard.png'), fullPage: true });

  const menu = await page.$$eval('.zt-menu-link', els => els.map(e => e.textContent.trim()));
  console.log('Itens do menu:', menu.join(' | '));

  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: path.join(OUT, '03-dashboard-mobile.png') });
  await page.click('#zt-toggle-sidebar');
  await page.waitForTimeout(400);
  await page.screenshot({ path: path.join(OUT, '04-menu-mobile.png') });

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.keyboard.press('Escape');
  await page.click('button[aria-label="Menu do usuário"]');
  await page.click('button:has-text("Sair")');
  await page.waitForLoadState('networkidle');
  console.log('Após logout:', page.url());

  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
