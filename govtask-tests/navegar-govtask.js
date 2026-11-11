/**
 * Script de automação - GovTask
 * Faz login, navega pelos menus, captura HTML e screenshots de cada página.
 * 
 * Uso: node navegar-govtask.js
 */

const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

// ─── Configurações ───────────────────────────────────────────────────────────
const BASE_URL = 'https://conectagov.com.br/govtask';
const LOGIN_URL = `${BASE_URL}/public/login`;
const CREDENTIALS = { login: 'admin', senha: 'admin123' };

// Diretório onde os HTMLs e screenshots serão salvos
const OUTPUT_DIR = path.join(__dirname, 'capturas');
const HTML_DIR = path.join(OUTPUT_DIR, 'html');
const SCREENSHOT_DIR = path.join(OUTPUT_DIR, 'screenshots');

// ─── Utilidades ──────────────────────────────────────────────────────────────

function ensureDirs() {
  [OUTPUT_DIR, HTML_DIR, SCREENSHOT_DIR].forEach(dir => {
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  });
}

function sanitizeFilename(text) {
  return text.replace(/[^a-zA-Z0-9_-]/g, '_').substring(0, 80);
}

async function salvarPagina(page, nomeBase) {
  const filename = sanitizeFilename(nomeBase);
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const fullName = `${filename}_${timestamp}`;

  // Salva HTML
  const html = await page.content();
  const htmlPath = path.join(HTML_DIR, `${fullName}.html`);
  fs.writeFileSync(htmlPath, html, 'utf-8');

  // Salva screenshot
  const screenshotPath = path.join(SCREENSHOT_DIR, `${fullName}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: true });

  console.log(`  ✓ Salvo: ${fullName}`);
  return { htmlPath, screenshotPath };
}

async function esperarCarregar(page, timeout = 5000) {
  try {
    await page.waitForLoadState('networkidle', { timeout });
  } catch {
    // Se timeout, continua mesmo assim
    await page.waitForTimeout(2000);
  }
}

// ─── Etapas de navegação ─────────────────────────────────────────────────────

async function fazerLogin(page) {
  console.log('\n🔑 Fazendo login...');
  await page.goto(LOGIN_URL);
  await esperarCarregar(page);

  // Preenche credenciais
  await page.fill('input[name="login"], input[name="usuario"], input[type="text"]', CREDENTIALS.login);
  await page.fill('input[name="senha"], input[name="password"], input[type="password"]', CREDENTIALS.senha);

  // Clica no botão de login
  await page.click('button[type="submit"], input[type="submit"], button:has-text("Entrar")');
  await esperarCarregar(page);

  // Verifica se login foi bem-sucedido
  const url = page.url();
  if (url.includes('login')) {
    throw new Error('Login falhou! Verifique as credenciais.');
  }

  console.log('  ✓ Login realizado com sucesso!');
  console.log(`  URL pós-login: ${url}`);
  await salvarPagina(page, '01_pos-login_dashboard');
}

async function descobrirMenus(page) {
  console.log('\n📋 Descobrindo menus disponíveis...');

  // Tenta encontrar itens de navegação/menu
  const menuSelectors = [
    'nav a',
    '.sidebar a',
    '.menu a',
    '.nav a',
    '[role="navigation"] a',
    '.navbar a',
    'aside a',
    '.sidebar-menu a',
    'ul.nav a',
    'a[href*="/govtask"]'
  ];

  let links = [];
  for (const selector of menuSelectors) {
    const found = await page.$$eval(selector, els =>
      els.map(el => ({
        texto: (el.textContent || '').trim(),
        href: el.href || el.getAttribute('href') || '',
        visible: el.offsetParent !== null
      })).filter(l => l.texto && l.href && l.visible)
    ).catch(() => []);

    if (found.length > 0) {
      links.push(...found);
    }
  }

  // Remove duplicados
  const uniqueLinks = [...new Map(links.map(l => [l.href, l])).values()];
  
  // Filtra links internos relevantes
  const menuLinks = uniqueLinks.filter(l =>
    l.href.includes('/govtask') &&
    !l.href.includes('login') &&
    !l.href.includes('logout') &&
    !l.href.includes('#')
  );

  console.log(`  Encontrados ${menuLinks.length} links de menu:`);
  menuLinks.forEach(l => console.log(`    - ${l.texto}: ${l.href}`));

  return menuLinks;
}

async function navegarMenus(page, menuLinks) {
  console.log('\n🗂️  Navegando pelos menus...');

  let contador = 2;
  for (const link of menuLinks) {
    try {
      console.log(`\n  → Navegando para: ${link.texto} (${link.href})`);
      await page.goto(link.href);
      await esperarCarregar(page);

      const nomeArquivo = `${String(contador).padStart(2, '0')}_menu_${link.texto}`;
      await salvarPagina(page, nomeArquivo);
      contador++;

      // Verifica se há sub-menus ou abas dentro desta página
      const subLinks = await page.$$eval('a[href*="/govtask"]', els =>
        els.map(el => ({
          texto: (el.textContent || '').trim(),
          href: el.href || ''
        })).filter(l => l.texto && l.href && !l.href.includes('login') && !l.href.includes('logout'))
      ).catch(() => []);

      // Navega nos primeiros 3 sub-links que são diferentes
      const visitados = new Set(menuLinks.map(m => m.href));
      const subNovos = subLinks.filter(s => !visitados.has(s.href)).slice(0, 3);

      for (const sub of subNovos) {
        try {
          console.log(`    ↳ Sub-link: ${sub.texto}`);
          await page.goto(sub.href);
          await esperarCarregar(page);

          const nomeSubArquivo = `${String(contador).padStart(2, '0')}_sub_${link.texto}_${sub.texto}`;
          await salvarPagina(page, nomeSubArquivo);
          visitados.add(sub.href);
          contador++;
        } catch (e) {
          console.log(`    ⚠ Erro ao acessar sub-link: ${e.message}`);
        }
      }
    } catch (e) {
      console.log(`  ⚠ Erro ao navegar para ${link.texto}: ${e.message}`);
    }
  }
}

async function explorarMapa(page) {
  console.log('\n🗺️  Procurando mapa de geolocalização...');

  // Tenta encontrar link ou seção do mapa
  const mapaSelectors = [
    'a:has-text("Mapa")',
    'a:has-text("mapa")',
    'a:has-text("Geolocalização")',
    'a:has-text("geo")',
    'a[href*="mapa"]',
    'a[href*="map"]',
    'a[href*="geo"]',
  ];

  let mapaEncontrado = false;

  for (const selector of mapaSelectors) {
    try {
      const link = await page.$(selector);
      if (link) {
        const href = await link.getAttribute('href');
        console.log(`  Encontrado link do mapa: ${href}`);
        await link.click();
        await esperarCarregar(page);
        await page.waitForTimeout(3000); // Espera extra para mapa carregar

        mapaEncontrado = true;
        await salvarPagina(page, 'mapa_geolocalizacao');

        // Tenta interagir com o mapa (zoom, markers)
        const mapContainer = await page.$('.leaflet-container, .map-container, #map, [class*="map"]');
        if (mapContainer) {
          console.log('  ✓ Container de mapa encontrado (provavelmente Leaflet)');

          // Captura informações dos markers
          const markers = await page.$$eval('.leaflet-marker-icon, .marker', els => els.length).catch(() => 0);
          console.log(`  📍 Markers encontrados: ${markers}`);

          // Tenta clicar em um marker para ver detalhes
          const markerEl = await page.$('.leaflet-marker-icon');
          if (markerEl) {
            await markerEl.click();
            await page.waitForTimeout(1500);
            await salvarPagina(page, 'mapa_marker_detalhe');
          }
        }

        break;
      }
    } catch {
      continue;
    }
  }

  if (!mapaEncontrado) {
    // Tenta URLs comuns para mapa
    const urlsMapa = [
      `${BASE_URL}/mapa`,
      `${BASE_URL}/admin/mapa`,
      `${BASE_URL}/demandas/mapa`,
      `${BASE_URL}/painel/mapa`,
    ];

    for (const url of urlsMapa) {
      try {
        console.log(`  Tentando URL: ${url}`);
        const response = await page.goto(url);
        if (response && response.status() === 200) {
          await esperarCarregar(page);
          await page.waitForTimeout(3000);
          await salvarPagina(page, 'mapa_geolocalizacao');
          mapaEncontrado = true;
          console.log('  ✓ Mapa encontrado!');
          break;
        }
      } catch {
        continue;
      }
    }
  }

  if (!mapaEncontrado) {
    console.log('  ⚠ Mapa não encontrado automaticamente. Verifique os HTMLs capturados para localizar o link correto.');
  }
}

async function gerarRelatorio(startTime) {
  console.log('\n📊 Gerando relatório...');

  const htmlFiles = fs.readdirSync(HTML_DIR).filter(f => f.endsWith('.html'));
  const screenshotFiles = fs.readdirSync(SCREENSHOT_DIR).filter(f => f.endsWith('.png'));

  const relatorio = `# Relatório de Navegação - GovTask
Data: ${new Date().toLocaleString('pt-BR')}
Duração: ${((Date.now() - startTime) / 1000).toFixed(1)}s

## Páginas capturadas (${htmlFiles.length})
${htmlFiles.map(f => `- ${f}`).join('\n')}

## Screenshots (${screenshotFiles.length})
${screenshotFiles.map(f => `- ${f}`).join('\n')}

## Diretórios
- HTMLs: ${HTML_DIR}
- Screenshots: ${SCREENSHOT_DIR}
`;

  fs.writeFileSync(path.join(OUTPUT_DIR, 'relatorio.md'), relatorio, 'utf-8');
  console.log(`  ✓ Relatório salvo em: ${path.join(OUTPUT_DIR, 'relatorio.md')}`);
}

// ─── Main ────────────────────────────────────────────────────────────────────

async function main() {
  const startTime = Date.now();
  ensureDirs();

  console.log('═══════════════════════════════════════════════════');
  console.log('  GovTask - Automação de Testes e Documentação');
  console.log('═══════════════════════════════════════════════════');

  const browser = await chromium.launch({
    headless: false,  // Abre o navegador para visualizar (mude para true para rodar sem janela)
    slowMo: 500       // Delay entre ações para visualização (remova para velocidade máxima)
  });

  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
    locale: 'pt-BR'
  });

  const page = await context.newPage();

  try {
    // 1. Login
    await fazerLogin(page);

    // 2. Descobrir e navegar pelos menus
    const menuLinks = await descobrirMenus(page);
    await navegarMenus(page, menuLinks);

    // 3. Explorar mapa de geolocalização
    await explorarMapa(page);

    // 4. Gerar relatório
    await gerarRelatorio(startTime);

    console.log('\n═══════════════════════════════════════════════════');
    console.log('  ✅ Navegação concluída com sucesso!');
    console.log(`  📁 Capturas salvas em: ${OUTPUT_DIR}`);
    console.log('═══════════════════════════════════════════════════');

  } catch (error) {
    console.error(`\n❌ Erro: ${error.message}`);
    // Salva screenshot do erro
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'ERRO.png'), fullPage: true });
  } finally {
    await browser.close();
  }
}

main();
