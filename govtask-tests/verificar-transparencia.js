// E2E: Consulta pública — sem login, sem dados pessoais, isolamento entre prefeituras e rate limit.
// Requer a app iniciada com Transparencia__LimitePorMinuto=30.
const { chromium, request } = require('playwright');
const { execSync } = require('child_process');
const path = require('path');
const BASE = 'http://localhost:5080';
const OUT = path.join(__dirname, 'verificacao-zitei');
const s = Date.now().toString().slice(-5);

const sql = (q) => execSync('docker exec -i sgi-postgres psql -U sgi -d sistema_gestao_infra -t -A',
  { input: q, encoding: 'utf8' }).trim();

// Valores "armadilha": nenhum deles pode aparecer na página pública.
const CPF = '529.982.247-25', EMAIL = `vazou${s}@exemplo.com`, FONE = '(16) 98877-6655';
const RUA = `RuaSecreta${s}`, LAT = '-21.55550', REQ = `REQSECRETO${s}`, FOTO = `fotosecreta${s}.png`;
const TOKEN = `ARMADILHA${s}`;

sql(`INSERT INTO prefeituras ("Nome","Cidade","Uf","Ativo","CriadoEm")
     SELECT 'Prefeitura B ${s}','Cidade B','SP',false,now() WHERE NOT EXISTS (SELECT 1 FROM prefeituras WHERE "Nome"='Prefeitura B ${s}');`);
const idB = sql(`SELECT "Id" FROM prefeituras WHERE "Nome"='Prefeitura B ${s}';`);

const inserirAtividade = (pref, desc, extra = '') => sql(`
  INSERT INTO atividades ("Descricao","TipoAtividadeId","SetorId","PrioridadeId","StatusId","Progresso","Requerimento","PrefeituraId","CriadoEm"
     ${extra ? ',"Rua","Numero","Bairro","Latitude","Longitude","FotoPath","NumeroRequerimento","UsuarioAberturaId"' : ''})
  SELECT '${desc}',(SELECT "Id" FROM tipos_atividade ORDER BY "Id" LIMIT 1),(SELECT "Id" FROM setores ORDER BY "Id" LIMIT 1),
         (SELECT "Id" FROM prioridades ORDER BY "Id" LIMIT 1),(SELECT "Id" FROM status_atividade WHERE "Codigo"='ABERTA' LIMIT 1),0,${extra ? 'true' : 'false'},${pref},now()
     ${extra ? `,'${RUA}','777','Bairro Publico ${s}',${LAT},-47.77770,'atividades/${FOTO}','${REQ}',(SELECT "Id" FROM usuarios WHERE "UserName"='admin')` : ''}
  RETURNING "Id";`).split('\n')[0];

const idSensivel = inserirAtividade(1, `${TOKEN} buraco. Morador ${CPF}, ${EMAIL}, ligar ${FONE}`, 'completo');
const idOutra = inserirAtividade(idB, `OUTRAPREFEITURA${s} nao pode aparecer`);
sql(`INSERT INTO atividades ("Descricao","TipoAtividadeId","SetorId","PrioridadeId","StatusId","Progresso","Requerimento","PrefeituraId","CriadoEm","Bairro")
     SELECT 'Paginacao '||g,(SELECT "Id" FROM tipos_atividade ORDER BY "Id" LIMIT 1),NULL,(SELECT "Id" FROM prioridades ORDER BY "Id" LIMIT 1),
            (SELECT "Id" FROM status_atividade WHERE "Codigo"='ABERTA' LIMIT 1),0,false,1,now()-(g||' minutes')::interval,'Bairro Publico ${s}'
     FROM generate_series(1,25) g;`);

const proibidos = [CPF, CPF.replace(/\D/g, ''), EMAIL, '98877-6655', RUA, LAT, '-47.7777', REQ, FOTO, 'Administrador', 'admin@sistema.local', 'fotosecreta'];
const vazamentos = (html) => proibidos.filter(p => html.includes(p));

(async () => {
  const browser = await chromium.launch();
  const anon = await browser.newContext({ viewport: { width: 1440, height: 1000 } });   // sem cookies = visitante anônimo
  const page = await anon.newPage();

  // ---- Acesso sem login ----
  let r = await page.goto(`${BASE}/Transparencia`);
  console.log('[Anônimo] /Transparencia ->', r.status(), page.url().includes('Login') ? '(redirecionou p/ login: FALHA)' : '(sem login)');
  console.log('[Anônimo] com 1 prefeitura ativa entra direto:', (await page.textContent('body')).includes('registro(s)'));

  // ---- Vazamento de dados pessoais ----
  const lista = await page.content();
  console.log('[Lista] descrição visível (mascarada):', lista.includes(TOKEN));
  console.log('[Lista] vazamentos (esperado []):', JSON.stringify(vazamentos(lista)));
  console.log('[Lista] marcadores de ocultação:', ['[CPF oculto]', '[e-mail oculto]', '[telefone oculto]'].map(m => `${m}=${lista.includes(m)}`).join(' '));
  console.log('[Lista] bairro (local genérico) é exibido:', lista.includes(`Bairro Publico ${s}`));
  await page.screenshot({ path: path.join(OUT, '27-transparencia.png'), fullPage: true });

  r = await page.goto(`${BASE}/Transparencia/Ver/${idSensivel}?prefeitura=1`);
  const detalhe = await page.content();
  console.log('[Detalhe] status:', r.status(), '| vazamentos (esperado []):', JSON.stringify(vazamentos(detalhe)));
  console.log('[Detalhe] sem "aberto por"/foto/mapa:', !/Aberto por|Foto de evid|leaflet|mapa-detalhe/i.test(detalhe.replace('Evidências fotográficas ficam restritas', '')));
  await page.screenshot({ path: path.join(OUT, '28-transparencia-detalhe.png'), fullPage: true });

  // Foto e requerimento continuam fechados para anônimo
  const resp = await anon.request.get(`${BASE}/Atividades/Foto/${idSensivel}`, { maxRedirects: 0 });
  console.log('[Foto] anônimo ->', resp.status(), '(esperado 302 para login)');
  const resp2 = await anon.request.get(`${BASE}/Atividades`, { maxRedirects: 0 });
  console.log('[Área interna] /Atividades anônimo ->', resp2.status(), '(esperado 302)');

  // ---- Isolamento entre prefeituras ----
  await page.goto(`${BASE}/Transparencia?prefeitura=1`);
  console.log('[Isolamento] lista da prefeitura 1 não contém a da B:', !(await page.content()).includes(`OUTRAPREFEITURA${s}`));
  r = await page.goto(`${BASE}/Transparencia/Ver/${idOutra}?prefeitura=1`);
  console.log('[Isolamento] id da prefeitura B consultado como prefeitura 1 ->', r.status(), '(esperado 404)');
  r = await page.goto(`${BASE}/Transparencia/Ver/${idOutra}?prefeitura=${idB}`);
  console.log('[Isolamento] prefeitura B inativa ->', r.status(), '(esperado 404)');
  r = await page.goto(`${BASE}/Transparencia?prefeitura=${idB}`);
  console.log('[Isolamento] listar prefeitura inativa não mostra dados:', !(await page.content()).includes(`OUTRAPREFEITURA${s}`));

  // Com duas prefeituras ativas, pede para escolher
  sql(`UPDATE prefeituras SET "Ativo"=true WHERE "Id"=${idB};`);
  await page.goto(`${BASE}/Transparencia`);
  console.log('[Seletor] 2 prefeituras ativas -> pede escolha:', (await page.textContent('body')).includes('Selecione uma prefeitura'));
  await page.goto(`${BASE}/Transparencia?prefeitura=${idB}`);
  const htmlB = await page.content();
  console.log('[Prefeitura B ativa] vê só a própria:', htmlB.includes(`OUTRAPREFEITURA${s}`) && !htmlB.includes(TOKEN));
  sql(`UPDATE prefeituras SET "Ativo"=false WHERE "Id"=${idB};`);

  // ---- Paginação e filtros ----
  await page.goto(`${BASE}/Transparencia?prefeitura=1`);
  const linhasP1 = await page.locator('tbody tr').count();
  await page.goto(`${BASE}/Transparencia?prefeitura=1&pagina=2`);
  const linhasP2 = await page.locator('tbody tr').count();
  console.log('[Paginação] página 1 tem 20 linhas:', linhasP1 === 20, '| página 2 tem o resto (>0):', linhasP2 > 0);
  await page.goto(`${BASE}/Transparencia?prefeitura=1&pagina=-5`);
  console.log('[Paginação] página inválida não quebra ->', (await page.locator('tbody tr').count()) === 20);

  // ---- Entradas maliciosas ----
  for (const q of [`bairro=${encodeURIComponent("' OR 1=1 --")}`, 'statusId=abc', `bairro=${encodeURIComponent('<script>alert(1)</script>')}`]) {
    r = await page.goto(`${BASE}/Transparencia?prefeitura=1&${q}`);
    console.log(`[Entrada maliciosa] ${decodeURIComponent(q).slice(0, 28)} ->`, r.status(), '| script injetado:', (await page.locator('script', { hasText: 'alert(1)' }).count()) > 0);
  }

  // ---- Cabeçalhos ----
  r = await page.goto(`${BASE}/Transparencia?prefeitura=1`);
  const h = r.headers();
  console.log('[Cabeçalhos] nosniff:', h['x-content-type-options'], '| frame:', h['x-frame-options'], '| referrer:', h['referrer-policy']);

  // ---- Rate limit (por último) ----
  const api = await request.newContext();
  const codigos = [];
  for (let i = 0; i < 45; i++) codigos.push((await api.get(`${BASE}/Transparencia?prefeitura=1`)).status());
  const ok = codigos.filter(c => c === 200).length, limitados = codigos.filter(c => c === 429).length;
  console.log(`[Rate limit] 45 requisições -> ${ok} ok, ${limitados} bloqueadas (429). Bloqueou:`, limitados > 0);
  const login = await api.get(`${BASE}/Conta/Login`);
  console.log('[Rate limit] não afeta o login (só a consulta pública):', login.status() === 200);

  await browser.close();
})().catch(e => { console.error('FALHOU:', e.message); process.exit(1); });
