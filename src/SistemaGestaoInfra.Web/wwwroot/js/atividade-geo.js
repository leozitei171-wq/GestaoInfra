// Zitei — localização no cadastro de atividade:
//  (1) "Usar minha localização atual": GPS -> ponto no mapa -> endereço preenchido (geocodificação reversa)
//  (2) "Localizar endereço no mapa": endereço -> ponto no mapa (também automático ao avançar do passo 1)
// Depende de atividade-wizard.js (window.__zitei). Textos do servidor entram sempre via textContent.
(function () {
  'use strict';

  var cfg = document.getElementById('geo-config');
  var api = function () { return window.__zitei; };
  if (!cfg || !document.getElementById('btn-gps')) return;

  var URL_BUSCAR = cfg.dataset.buscar;
  var URL_REVERSO = cfg.dataset.reverso;

  var el = {
    gps: document.getElementById('btn-gps'),
    localizar: document.getElementById('btn-localizar'),
    status: document.getElementById('geo-status'),
    opcoes: document.getElementById('geo-opcoes'),
    aviso2: document.getElementById('geo-aviso-passo2'),
    rua: document.getElementById('Rua'),
    numero: document.getElementById('Numero'),
    bairro: document.getElementById('Bairro'),
    cep: document.getElementById('Cep')
  };

  // 'nenhuma' | 'auto' (veio de um endereço) | 'manual' (clique, arraste, GPS ou digitado: nunca é sobrescrito sozinho)
  var origem = api() && api().temPonto() ? 'manual' : 'nenhuma';
  var ultimaChave = null;      // endereço usado na última busca automática
  var requisicao = 0;          // descarta respostas antigas quando o usuário já pediu outra coisa

  document.addEventListener('zitei:ponto-manual', function () { origem = 'manual'; });

  // ---------------------------------------------------------------- utilidades de tela

  function mostrar(texto, tipo) {
    el.status.textContent = texto || '';
    el.status.className = 'small mb-2 ' + ({ ok: 'text-success', aviso: 'text-warning-emphasis', erro: 'text-danger' }[tipo] || 'text-muted');
  }

  function aviso2(texto) {
    el.aviso2.textContent = texto || '';
    el.aviso2.classList.toggle('d-none', !texto);
  }

  function ocupado(sim) {
    el.gps.disabled = sim;
    el.localizar.disabled = sim;
  }

  function limparOpcoes() {
    el.opcoes.textContent = '';
    el.opcoes.classList.add('d-none');
  }

  function enderecoCurto(r) {
    var rua = [r.rua, r.numero].filter(Boolean).join(', ');
    return [rua, r.bairro, r.cidade].filter(Boolean).join(' — ') || r.descricao || 'endereço sem nome';
  }

  function chaveEndereco() {
    return [el.rua, el.numero, el.bairro, el.cep].map(function (e) { return e.value.trim().toLowerCase(); }).join('|');
  }

  function temEndereco() {
    return !!(el.rua.value.trim() || el.bairro.value.trim() || el.cep.value.trim());
  }

  // ---------------------------------------------------------------- chamadas ao servidor

  function chamar(url, params) {
    var q = new URLSearchParams();
    Object.keys(params).forEach(function (k) { if (params[k] !== '' && params[k] != null) q.set(k, params[k]); });
    return fetch(url + '?' + q.toString(), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(function (r) {
        if (r.redirected || r.status === 401) { window.location.reload(); throw new Error('Sessão expirada.'); }
        return r.json().catch(function () { return {}; }).then(function (corpo) {
          if (!r.ok) throw new Error(corpo.erro || 'Não foi possível consultar o endereço agora.');
          return corpo;
        });
      });
  }

  function buscarEndereco() {
    return chamar(URL_BUSCAR, {
      rua: el.rua.value.trim(), numero: el.numero.value.trim(),
      bairro: el.bairro.value.trim(), cep: el.cep.value.trim()
    });
  }

  // ---------------------------------------------------------------- aplicar resultados

  function textoPrecisao(r) {
    if (r.foraDaCidade) return { t: 'Atenção: o endereço encontrado fica fora da cidade da prefeitura (' + (r.cidade || '?') + '). Confira no mapa.', tipo: 'aviso' };
    if (r.precisao === 'regiao') return { t: 'Só foi possível localizar a região, não o endereço exato. Ajuste o ponto no mapa (clique ou arraste).', tipo: 'aviso' };
    if (r.precisao === 'via') return { t: 'Rua localizada, mas não o número. O ponto é aproximado: arraste o marcador até o local exato.', tipo: 'aviso' };
    return { t: 'Endereço localizado: ' + enderecoCurto(r) + '. Se precisar, arraste o marcador para ajustar.', tipo: 'ok' };
  }

  function aplicarPonto(r) {
    api().definirPonto(r.lat, r.lng);
    origem = 'auto';
    var p = textoPrecisao(r);
    mostrar(p.t, p.tipo);
    aviso2(p.tipo === 'aviso' ? 'Localização aproximada: ' + p.t : '');
  }

  // Preenche os campos de endereço com o que o reverso devolveu (usado pelo GPS).
  function preencherEndereco(r) {
    el.rua.value = r.rua || '';
    el.numero.value = r.numero || '';
    el.bairro.value = r.bairro || '';
    el.cep.value = r.cep || '';
    ultimaChave = chaveEndereco();   // já corresponde ao ponto: não precisa buscar de novo ao avançar
  }

  function mostrarAlternativas(lista) {
    limparOpcoes();
    if (lista.length < 2) return;
    lista.slice(1).forEach(function (r) {
      var b = document.createElement('button');
      b.type = 'button';
      b.className = 'list-group-item list-group-item-action small';
      b.textContent = 'Ou: ' + enderecoCurto(r);
      b.addEventListener('click', function () { aplicarPonto(r); limparOpcoes(); });
      el.opcoes.appendChild(b);
    });
    el.opcoes.classList.remove('d-none');
  }

  // ---------------------------------------------------------------- (2) endereço -> ponto

  function localizarEndereco() {
    if (!temEndereco()) {
      mostrar('Informe ao menos a rua, o bairro ou o CEP para localizar no mapa.', 'erro');
      return Promise.resolve();
    }

    var minha = ++requisicao;
    ocupado(true);
    mostrar('Procurando o endereço no mapa…', null);
    limparOpcoes();

    return buscarEndereco()
      .then(function (corpo) {
        if (minha !== requisicao) return;
        ultimaChave = chaveEndereco();
        if (!corpo.encontrado || !corpo.resultados.length) {
          mostrar('Não encontramos esse endereço. Confira os dados ou clique no mapa para marcar o local.', 'aviso');
          aviso2('Não foi possível localizar o endereço no mapa. Você pode marcar o ponto manualmente ou seguir sem localização.');
          return;
        }
        aplicarPonto(corpo.resultados[0]);
        mostrarAlternativas(corpo.resultados);
      })
      .catch(function (e) {
        if (minha !== requisicao) return;
        mostrar(e.message + ' Você pode clicar no mapa para marcar o local.', 'erro');
        aviso2('O endereço não pôde ser localizado agora. Marque o ponto no mapa ou siga sem localização.');
      })
      .then(function () { if (minha === requisicao) ocupado(false); });
  }

  el.localizar.addEventListener('click', function () {
    origem = 'nenhuma';   // pedido explícito: pode substituir um ponto anterior
    localizarEndereco();
  });

  // Ao avançar do passo 1: localiza sozinho se há endereço e o usuário não marcou o ponto à mão.
  window.__ziteiAntesDeAvancar = function () {
    aviso2('');
    if (!api()) return null;
    if (origem === 'manual' && api().temPonto()) return null;                  // respeita o ajuste do usuário
    if (!temEndereco()) return null;                                           // nada para localizar (segue sem ponto)
    if (origem === 'auto' && api().temPonto() && chaveEndereco() === ultimaChave) return null;   // já localizado
    return localizarEndereco();
  };

  // ---------------------------------------------------------------- (1) GPS -> ponto -> endereço

  function erroGps(e) {
    switch (e && e.code) {
      case 1: return 'Permissão de localização negada. Libere o acesso à localização no navegador, ou informe o endereço.';
      case 2: return 'Não foi possível obter sua posição agora (sinal de GPS indisponível). Informe o endereço.';
      case 3: return 'A localização demorou demais. Tente de novo em um local aberto, ou informe o endereço.';
      default: return 'Não foi possível obter sua localização. Informe o endereço.';
    }
  }

  el.gps.addEventListener('click', function () {
    if (!('geolocation' in navigator)) {
      mostrar('Este navegador não oferece localização. Informe o endereço.', 'erro');
      return;
    }
    if (!window.isSecureContext) {
      mostrar('O navegador só permite usar a localização em conexão segura (HTTPS). Acesse o sistema por https:// ou informe o endereço.', 'erro');
      return;
    }

    var minha = ++requisicao;
    ocupado(true);
    limparOpcoes();
    aviso2('');
    mostrar('Obtendo sua localização… (autorize o acesso no navegador, se perguntar)', null);

    navigator.geolocation.getCurrentPosition(function (pos) {
      if (minha !== requisicao) return;
      var lat = pos.coords.latitude, lng = pos.coords.longitude, prec = Math.round(pos.coords.accuracy || 0);

      api().definirPonto(lat, lng);
      origem = 'manual';   // o GPS é a verdade do usuário: não é sobrescrito por busca de endereço
      mostrar('Localização obtida (precisão de cerca de ' + prec + ' m). Buscando o endereço…', null);

      chamar(URL_REVERSO, { lat: lat, lng: lng })
        .then(function (corpo) {
          if (minha !== requisicao) return;
          var aviso = prec > 100 ? ' A precisão do GPS está baixa (' + prec + ' m): confira e arraste o marcador, se precisar.' : '';
          if (!corpo.encontrado) {
            mostrar('Ponto marcado, mas não encontramos um endereço para ele. Preencha o endereço ou deixe só o ponto.' + aviso, 'aviso');
            return;
          }
          preencherEndereco(corpo.resultado);
          var p = corpo.resultado.foraDaCidade
            ? { t: 'Atenção: sua localização está fora da cidade da prefeitura (' + (corpo.resultado.cidade || '?') + ').', tipo: 'aviso' }
            : { t: 'Pronto: ' + enderecoCurto(corpo.resultado) + '.', tipo: 'ok' };
          mostrar(p.t + aviso, prec > 100 ? 'aviso' : p.tipo);
        })
        .catch(function (e) {
          if (minha !== requisicao) return;
          mostrar('Ponto marcado pelo GPS, mas o endereço não pôde ser preenchido agora (' + e.message + ') Preencha o endereço manualmente.', 'aviso');
        })
        .then(function () { if (minha === requisicao) ocupado(false); });
    }, function (e) {
      if (minha !== requisicao) return;
      mostrar(erroGps(e), 'erro');
      ocupado(false);
    }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
  });
})();
