/**
 * Script completo - GovTask - Navegação em TODAS as páginas
 * Baseado no mapa de menus descoberto na primeira execução.
 * 
 * Uso: node navegar-completo.js
 */

const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

// ─── Configurações ───────────────────────────────────────────────────────────
const BASE_URL = 'https://conectagov.com.br/govtask/public';
const CREDENTIALS = { login: 'admin', senha: 'admin123' };

const OUTPUT_DIR = path.join(__dirname, 'capturas-completas');
const HTML_DIR = path.join(OUTPUT_DIR, 'html');
const SCREENSHOT_DIR = path.join(OUTPUT_DIR, 'screenshots');

// Mapa completo de todas as páginas do sistema (descoberto na primeira execução)
const PAGINAS = [
  // ─── Operacional ───
  { grupo: 'Operacional', nome: 'Dashboard', url: '/dashboard' },
  { grupo: 'Operacional', nome: 'Atividades', url: '/atividades' },
  { grupo: 'Operacional', nome: 'Programacao', url: '/programacao' },
  { grupo: 'Operacional', nome: 'Nova-Atividade', url: '/atividade-nova' },
  { grupo: 'Operacional', nome: 'Mapa-Atividades', url: '/mapa-atividades' },
  { grupo: 'Operacional', nome: 'Transparencia', url: '/transparencia' },

  // ─── Gestão ───
  { grupo: 'Gestao', nome: 'Indicadores', url: '/indicadores' },
  { grupo: 'Gestao', nome: 'Alertas-Contratos', url: '/alertas-contratos' },
  { grupo: 'Gestao', nome: 'Auditoria', url: '/auditoria' },
  { grupo: 'Gestao', nome: 'Rateio-Proporcional', url: '/rateios' },

  // ─── Cadastros Base ───
  { grupo: 'Cadastros', nome: 'Tipos-Medicao', url: '/tipos-medicao' },
  { grupo: 'Cadastros', nome: 'Setores', url: '/setores' },
  { grupo: 'Cadastros', nome: 'Tipos-Atividade', url: '/tipos-atividade' },
  { grupo: 'Cadastros', nome: 'Tipos-Servico', url: '/tipos-servico' },
  { grupo: 'Cadastros', nome: 'Atividade-Servicos', url: '/atividade-servicos' },
  { grupo: 'Cadastros', nome: 'Fornecedores', url: '/fornecedores' },
  { grupo: 'Cadastros', nome: 'Contratos', url: '/contratos' },
  { grupo: 'Cadastros', nome: 'Status-Atividade', url: '/status-atividade' },
  { grupo: 'Cadastros', nome: 'Tipos-Mao-Obra', url: '/tipos-mao-obra' },
  { grupo: 'Cadastros', nome: 'Funcoes-Servidor', url: '/funcoes-servidor' },
  { grupo: 'Cadastros', nome: 'Prioridades', url: '/prioridades' },
  { grupo: 'Cadastros', nome: 'Tipos-Custo', url: '/tipos-custo' },
  { grupo: 'Cadastros', nome: 'Tipos-Rateio', url: '/tipos-rateio' },
  { grupo: 'Cadastros', nome: 'Regioes', url: '/regioes' },
  { grupo: 'Cadastros', nome: 'Sub-Regioes', url: '/sub-regioes' },
  { grupo: 'Cadastros', nome: 'Bairros', url: '/bairros' },
  { grupo: 'Cadastros', nome: 'Logradouros', url: '/logradouros' },
  { grupo: 'Cadastros', nome: 'Locais', url: '/locais' },

  // ─── Administração ───
  { grupo: 'Admin', nome: 'Prefeituras-Tenants', url: '/prefeituras' },
  { grupo: 'Admin', nome: 'Usuarios-Perfis', url: '/usuarios' },
  { grupo: 'Admin', nome: 'Permissoes-RBAC', url: '/rbac-permissoes' },
  { grupo: 'Admin', nome: 'Wizard-Config', url: '/wizard-config' },
];

// ─── Utilidades ──────────────────────────────────────────────────────────────

function ensureDirs() {
  [OUTPUT_DIR, HTML_DIR, SCREENSHOT_DIR].forEach(dir => {
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  });
}

async function esperarCarregar(page) {
  try {
    await page.waitForLoadState('networkidle', { timeout: 8000 });
  } catch {
    await page.waitForTimeout(2000);
  }
}

async function salvarPagina(page, nomeBase) {
  const html = await page.content();
  const htmlPath = path.join(HTML_DIR, `${nomeBase}.html`);
  fs.writeFileSync(htmlPath, html, 'utf-8');

  const screenshotPath = path.join(SCREENSHOT_DIR, `${nomeBase}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: true });

  console.log(`  ✓ ${nomeBase}`);
}

// ─── Main ────────────────────────────────────────────────────────────────────

async function main() {
  const startTime = Date.now();
  ensureDirs();

  console.log('═══════════════════════════════════════════════════════════');
  console.log('  GovTask - Navegação Completa (todas as páginas)');
  console.log('═══════════════════════════════════════════════════════════');

  const browser = await chromium.launch({
    headless: true,  // Sem janela para velocidade máxima
  });

  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
    locale: 'pt-BR'
  });

  const page = await context.newPage();

  try {
    // 1. Login
    console.log('\n🔑 Login...');
    await page.goto(`${BASE_URL}/login`);
    await esperarCarregar(page);
    await page.fill('input[type="text"]', CREDENTIALS.login);
    await page.fill('input[type="password"]', CREDENTIALS.senha);
    await page.click('button:has-text("Entrar")');
    await esperarCarregar(page);

    if (page.url().includes('login')) {
      throw new Error('Login falhou!');
    }
    console.log('  ✓ Login OK\n');

    // 2. Navegar cada página do mapa
    let grupoAtual = '';
    let contador = 0;

    for (const pag of PAGINAS) {
      if (pag.grupo !== grupoAtual) {
        grupoAtual = pag.grupo;
        console.log(`\n📂 ${grupoAtual}`);
      }

      contador++;
      const nomeArquivo = `${String(contador).padStart(2, '0')}_${pag.grupo}_${pag.nome}`;

      try {
        await page.goto(`${BASE_URL}${pag.url}`);
        await esperarCarregar(page);
        await salvarPagina(page, nomeArquivo);
      } catch (e) {
        console.log(`  ✗ ERRO em ${pag.nome}: ${e.message}`);
      }
    }

    // 3. Mapa de atividades - captura extra com espera para tiles carregarem
    console.log('\n\n🗺️  Mapa de Geolocalização (captura dedicada)...');
    await page.goto(`${BASE_URL}/mapa-atividades`);
    await esperarCarregar(page);
    await page.waitForTimeout(4000); // Espera tiles do mapa carregarem

    // Tenta extrair dados dos markers no mapa
    const mapaInfo = await page.evaluate(() => {
      const info = {};
      // Procura markers Leaflet
      const markerPane = document.querySelector('.leaflet-marker-pane');
      info.totalMarkers = markerPane ? markerPane.children.length : 0;

      // Procura popups abertos
      const popups = document.querySelectorAll('.leaflet-popup-content');
      info.popups = Array.from(popups).map(p => p.textContent.trim());

      // Procura o script que inicializa o mapa para pegar dados
      const scripts = document.querySelectorAll('script');
      for (const s of scripts) {
        const text = s.textContent || '';
        if (text.includes('L.map') || text.includes('setView')) {
          // Extrai coordenadas do setView
          const match = text.match(/setView\(\[([^\]]+)\]/);
          if (match) info.centro = match[1];

          // Conta ocorrências de coordenadas (possíveis markers)
          const coords = text.match(/\[-?\d+\.\d+,\s*-?\d+\.\d+\]/g);
          info.coordenadasNoScript = coords ? coords.length : 0;
        }
      }

      return info;
    });

    console.log(`  📍 Markers visíveis: ${mapaInfo.totalMarkers}`);
    console.log(`  🌍 Centro do mapa: ${mapaInfo.centro || 'não detectado'}`);
    console.log(`  📌 Coordenadas no script: ${mapaInfo.coordenadasNoScript}`);

    await salvarPagina(page, 'MAPA_geolocalizacao_full');

    // Tenta clicar num marker para abrir popup
    const marker = await page.$('.leaflet-marker-icon');
    if (marker) {
      await marker.click();
      await page.waitForTimeout(1500);
      await salvarPagina(page, 'MAPA_marker_popup_aberto');
      console.log('  ✓ Popup de marker capturado');
    }

    // 4. Gerar relatório final
    console.log('\n\n📊 Gerando relatório...');
    const duracao = ((Date.now() - startTime) / 1000).toFixed(1);
    const htmlFiles = fs.readdirSync(HTML_DIR).sort();
    const screenshotFiles = fs.readdirSync(SCREENSHOT_DIR).sort();

    const relatorio = `# Documentação GovTask - Captura Completa
> Gerado em: ${new Date().toLocaleString('pt-BR')}
> Duração: ${duracao}s
> Total de páginas: ${htmlFiles.length}

## Stack / Tecnologias detectadas
- **Frontend**: HTML/CSS com template Sneat (vertical-menu-template-free)
- **CSS Framework**: Bootstrap 5 + tema customizado
- **Ícones**: BoxIcons, FontAwesome, Flag-Icons
- **Mapas**: Leaflet.js (OpenStreetMap tiles)
- **Gráficos**: ApexCharts
- **Select**: Select2
- **Datepicker**: Bootstrap Datepicker
- **Scrollbar**: Perfect Scrollbar
- **Fonte**: Google Fonts (Public Sans)
- **Arquitetura**: Multi-tenant (por prefeitura), RBAC

## Mapa do Sistema

### Operacional
| # | Página | URL |
|---|--------|-----|
${PAGINAS.filter(p => p.grupo === 'Operacional').map((p, i) => `| ${i+1} | ${p.nome} | ${p.url} |`).join('\n')}

### Gestão
| # | Página | URL |
|---|--------|-----|
${PAGINAS.filter(p => p.grupo === 'Gestao').map((p, i) => `| ${i+1} | ${p.nome} | ${p.url} |`).join('\n')}

### Cadastros Base
| # | Página | URL |
|---|--------|-----|
${PAGINAS.filter(p => p.grupo === 'Cadastros').map((p, i) => `| ${i+1} | ${p.nome} | ${p.url} |`).join('\n')}

### Administração
| # | Página | URL |
|---|--------|-----|
${PAGINAS.filter(p => p.grupo === 'Admin').map((p, i) => `| ${i+1} | ${p.nome} | ${p.url} |`).join('\n')}

## Mapa de Geolocalização
- Markers visíveis: ${mapaInfo.totalMarkers}
- Centro: ${mapaInfo.centro || 'Brasil (zoom 4)'}
- Coordenadas em script: ${mapaInfo.coordenadasNoScript}

## Arquivos capturados

### HTMLs (${htmlFiles.length})
${htmlFiles.map(f => `- \`${f}\``).join('\n')}

### Screenshots (${screenshotFiles.length})
${screenshotFiles.map(f => `- \`${f}\``).join('\n')}
`;

    fs.writeFileSync(path.join(OUTPUT_DIR, 'DOCUMENTACAO-GOVTASK.md'), relatorio, 'utf-8');

    console.log('\n═══════════════════════════════════════════════════════════');
    console.log(`  ✅ Captura completa! ${htmlFiles.length} páginas salvas`);
    console.log(`  📁 ${OUTPUT_DIR}`);
    console.log(`  📄 ${path.join(OUTPUT_DIR, 'DOCUMENTACAO-GOVTASK.md')}`);
    console.log('═══════════════════════════════════════════════════════════');

  } catch (error) {
    console.error(`\n❌ Erro fatal: ${error.message}`);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'ERRO_FATAL.png'), fullPage: true });
  } finally {
    await browser.close();
  }
}

main();
