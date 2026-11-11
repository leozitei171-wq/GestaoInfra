// Zitei — mapa de atividades (Leaflet + OpenStreetMap)
(function () {
  'use strict';

  var elMapa = document.getElementById('mapa');
  if (!elMapa || typeof L === 'undefined') return;

  var INTERVALO = 10; // segundos entre atualizações automáticas
  var url = elMapa.dataset.url;

  var mapa = L.map('mapa').setView([-21.1775, -47.8103], 12); // Ribeirão Preto/SP
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, attribution: '&copy; OpenStreetMap'
  }).addTo(mapa);

  var camada = L.layerGroup().addTo(mapa);
  var reenquadrar = true; // ajusta o zoom no 1º carregamento e a cada mudança de filtro
  var carregando = false;
  var pendente = false;
  var restante = INTERVALO;
  var temporizador = null;

  var campos = {
    statusId: 'f_status', bairro: 'f_bairro', setorId: 'f_setor',
    tipoId: 'f_tipo', prioridadeId: 'f_prioridade', de: 'f_de', ate: 'f_ate'
  };

  function montarQuery() {
    var p = new URLSearchParams();
    Object.keys(campos).forEach(function (chave) {
      var v = document.getElementById(campos[chave]).value;
      if (v) p.set(chave, v);
    });
    return p.toString();
  }

  // Popup montado com nós DOM + textContent: descrição/endereço vêm de usuários
  // e nunca podem ser interpretados como HTML.
  function linha(rotulo, valor) {
    var d = document.createElement('div');
    var b = document.createElement('strong');
    b.textContent = rotulo + ': ';
    d.appendChild(b);
    d.appendChild(document.createTextNode(valor));
    return d;
  }

  function montarPopup(i) {
    var raiz = document.createElement('div');
    var titulo = document.createElement('div');
    titulo.className = 'fw-semibold mb-1';
    titulo.textContent = 'Atividade #' + i.id + ' · ' + i.tipo;
    raiz.appendChild(titulo);

    var desc = document.createElement('div');
    desc.className = 'mb-1';
    desc.textContent = i.descricao.length > 140 ? i.descricao.slice(0, 140) + '…' : i.descricao;
    raiz.appendChild(desc);

    raiz.appendChild(linha('Status', i.status));
    raiz.appendChild(linha('Prioridade', i.prioridade));
    raiz.appendChild(linha('Progresso', i.progresso + '%'));

    var partes = [];
    var ruaNum = [i.rua, i.numero].filter(Boolean).join(', ');
    if (ruaNum) partes.push(ruaNum);
    if (i.bairro) partes.push(i.bairro);
    if (partes.length) raiz.appendChild(linha('Endereço', partes.join(' — ')));

    var link = document.createElement('a');
    link.href = '/Atividades/Detalhe/' + encodeURIComponent(i.id);
    link.className = 'd-inline-block mt-2';
    link.textContent = 'Ver detalhes';
    raiz.appendChild(link);
    return raiz;
  }

  function avisar(texto) {
    var aviso = document.getElementById('mapa-aviso');
    aviso.textContent = texto || '';
    aviso.classList.toggle('d-none', !texto);
  }

  function carregar() {
    if (carregando) return Promise.resolve();
    carregando = true;
    var enquadrar = reenquadrar; // captura e zera: mudanças de filtro durante a carga geram nova carga
    reenquadrar = false;
    var query = montarQuery();

    return fetch(url + (query ? '?' + query : ''), { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
      .then(function (r) {
        if (r.redirected || r.status === 401) { window.location.reload(); throw new Error('sessão expirada'); }
        if (!r.ok) throw new Error('HTTP ' + r.status);
        return r.json();
      })
      .then(function (dados) {
        camada.clearLayers();
        var limites = [];
        dados.itens.forEach(function (i) {
          var pos = [Number(i.lat), Number(i.lng)];
          var m = L.circleMarker(pos, {
            radius: 8, color: i.cor, weight: 2, fillColor: i.cor, fillOpacity: 0.8
          });
          m.bindPopup(montarPopup(i));
          m.addTo(camada);
          limites.push(pos);
        });

        if (enquadrar && limites.length) {
          mapa.fitBounds(L.latLngBounds(limites).pad(0.2), { animate: false, maxZoom: 17 });
        }

        document.getElementById('mapa-contagem').textContent = dados.itens.length + ' no mapa';
        document.getElementById('mapa-ultima').textContent = new Date(dados.atualizadoEm).toLocaleTimeString('pt-BR');

        var avisos = [];
        if (dados.semLocalizacao > 0) {
          avisos.push(dados.semLocalizacao + (dados.semLocalizacao === 1
            ? ' atividade não aparece porque não tem localização. Abra-a em "Atividades" e clique no mapa para marcar o ponto.'
            : ' atividades não aparecem porque não têm localização. Abra cada uma em "Atividades" e clique no mapa para marcar o ponto.'));
        }
        if (dados.truncado) {
          avisos.push('Exibindo apenas as ' + dados.itens.length + ' atividades mais recentes de ' + dados.total + '. Use os filtros para refinar.');
        }
        avisar(avisos.join(' '));
      })
      .catch(function (e) {
        if (e.message !== 'sessão expirada') avisar('Não foi possível atualizar o mapa (' + e.message + '). Tentando novamente…');
      })
      .then(function () {
        carregando = false;
        if (pendente) { pendente = false; return carregar(); }
      });
  }

  // Chamado por mudanças de filtro: nunca é descartado, mesmo com uma carga em andamento.
  function solicitar() {
    reenquadrar = true;
    if (carregando) { pendente = true; return Promise.resolve(); }
    return carregar();
  }

  // ---------- Atualização automática ----------
  var chkAuto = document.getElementById('f_auto');
  var lblContagem = document.getElementById('mapa-countdown');

  function reiniciarContagem() {
    if (temporizador) clearInterval(temporizador);
    restante = INTERVALO;
    if (!chkAuto.checked) { lblContagem.textContent = 'atualização automática desativada'; return; }
    lblContagem.textContent = 'próxima atualização em ' + restante + 's';
    temporizador = setInterval(function () {
      if (document.hidden) return; // não consulta o servidor com a aba em segundo plano
      restante -= 1;
      if (restante <= 0) { carregar().then(reiniciarContagem); return; }
      lblContagem.textContent = 'próxima atualização em ' + restante + 's';
    }, 1000);
  }

  chkAuto.addEventListener('change', reiniciarContagem);

  Object.keys(campos).forEach(function (chave) {
    document.getElementById(campos[chave]).addEventListener('change', function () {
      solicitar().then(reiniciarContagem);
    });
  });

  document.getElementById('btn-limpar').addEventListener('click', function () {
    Object.keys(campos).forEach(function (chave) { document.getElementById(campos[chave]).value = ''; });
    solicitar().then(reiniciarContagem);
  });

  reenquadrar = true;
  carregar().then(reiniciarContagem);
})();
