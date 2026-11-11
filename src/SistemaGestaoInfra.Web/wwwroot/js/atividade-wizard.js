// Zitei — wizard de nova atividade (3 passos)
(function () {
  'use strict';

  var form = document.getElementById('form-atividade');
  if (!form) return;

  var passos = form.querySelectorAll('[data-step]');
  var pills = document.querySelectorAll('[data-pill]');
  var passoAtual = 1;

  // ---------- Navegação entre passos ----------
  function mostrar(n) {
    passoAtual = n;
    passos.forEach(function (s) {
      s.classList.toggle('d-none', Number(s.dataset.step) !== n);
    });
    pills.forEach(function (p) {
      var num = Number(p.dataset.pill);
      p.classList.toggle('active', num === n);
      p.classList.toggle('disabled', num > n);
      if (num === n) p.setAttribute('aria-current', 'step'); else p.removeAttribute('aria-current');
    });
    if (n === 1 && window.__mapaPicker) {
      setTimeout(function () { window.__mapaPicker.invalidateSize(); }, 50);
    }
    var titulo = form.querySelector('[data-step="' + n + '"] h2');
    if (titulo) { titulo.setAttribute('tabindex', '-1'); titulo.focus(); }
  }

  // ---------- Validação por passo ----------
  function validarPasso(n) {
    var secao = form.querySelector('[data-step="' + n + '"]');
    var valido = true;

    secao.querySelectorAll('.wiz-erro').forEach(function (e) { e.remove(); });

    function erro(campo, msg) {
      valido = false;
      campo.classList.add('is-invalid');
      var aviso = document.createElement('div');
      aviso.className = 'text-danger small wiz-erro';
      aviso.textContent = msg;
      campo.insertAdjacentElement('afterend', aviso);
    }

    secao.querySelectorAll('.is-invalid').forEach(function (c) { c.classList.remove('is-invalid'); });

    if (n === 1) {
      var tipo = form.querySelector('#TipoAtividadeId');
      if (!tipo.value) erro(tipo, 'Selecione o tipo de atividade.');

      var lat = form.querySelector('#Latitude');
      var lng = form.querySelector('#Longitude');
      if ((lat.value && !lng.value) || (!lat.value && lng.value)) {
        erro(lat, 'Informe latitude e longitude juntas, ou deixe ambas em branco.');
      } else if (lat.value && lng.value) {
        var la = parseFloat(lat.value.replace(',', '.'));
        var lo = parseFloat(lng.value.replace(',', '.'));
        if (!isFinite(la) || la < -90 || la > 90) erro(lat, 'Latitude deve ser um número entre -90 e 90.');
        if (!isFinite(lo) || lo < -180 || lo > 180) erro(lng, 'Longitude deve ser um número entre -180 e 180.');
      }
    }

    if (n === 2) {
      var prio = form.querySelector('#PrioridadeId');
      if (!prio.value) erro(prio, 'Selecione a prioridade.');

      var desc = form.querySelector('#Descricao');
      if (desc.value.trim().length < 5) erro(desc, 'Descreva a atividade (mínimo de 5 caracteres).');

      var req = form.querySelector('#Requerimento');
      var num = form.querySelector('#NumeroRequerimento');
      if (req.checked && !num.value.trim()) erro(num, 'Informe o número do requerimento.');
    }

    if (!valido) {
      var primeiro = secao.querySelector('.is-invalid');
      if (primeiro) primeiro.focus();
    }
    return valido;
  }

  form.addEventListener('click', function (e) {
    var alvo = e.target.closest('[data-next],[data-prev]');
    if (!alvo) return;
    if (alvo.dataset.next) {
      if (!validarPasso(passoAtual)) return;
      var destino = Number(alvo.dataset.next);
      var gancho = window.__ziteiAntesDeAvancar;

      // Passo 1 -> 2: dá chance de localizar o endereço no mapa antes de seguir.
      // Qualquer falha do gancho nunca impede o usuário de continuar.
      if (passoAtual === 1 && typeof gancho === 'function') {
        alvo.disabled = true;
        Promise.resolve()
          .then(gancho)
          .catch(function () { /* segue mesmo assim */ })
          .then(function () { alvo.disabled = false; mostrar(destino); });
      } else {
        mostrar(destino);
      }
    } else {
      mostrar(Number(alvo.dataset.prev));
    }
  });

  // Enter nos campos de texto não deve enviar o formulário antes do último passo.
  form.addEventListener('keydown', function (e) {
    if (e.key === 'Enter' && passoAtual < 3 && e.target.tagName !== 'TEXTAREA') {
      e.preventDefault();
      var proximo = form.querySelector('[data-step="' + passoAtual + '"] [data-next]');
      if (proximo) proximo.click();
    }
  });

  // Bloqueia duplo clique no envio.
  form.addEventListener('submit', function () {
    var botao = document.getElementById('btn-enviar');
    if (botao) { botao.disabled = true; botao.textContent = 'Enviando…'; }
  });

  // ---------- Requerimento ----------
  var chkReq = document.getElementById('Requerimento');
  var blocoReq = document.getElementById('bloco-req');
  function atualizarReq() { blocoReq.classList.toggle('d-none', !chkReq.checked); }
  chkReq.addEventListener('change', atualizarReq);
  atualizarReq();

  // ---------- Preview da foto ----------
  var inputFoto = document.getElementById('Foto');
  var boxPreview = document.getElementById('preview-foto');
  var urlAtual = null;
  inputFoto.addEventListener('change', function () {
    if (urlAtual) { URL.revokeObjectURL(urlAtual); urlAtual = null; }
    var arquivo = inputFoto.files && inputFoto.files[0];
    if (!arquivo) { boxPreview.classList.add('d-none'); return; }
    urlAtual = URL.createObjectURL(arquivo);
    boxPreview.querySelector('img').src = urlAtual;
    boxPreview.classList.remove('d-none');
  });

  // ---------- Mapa (Leaflet + OpenStreetMap) ----------
  // Coordenadas são validadas pelo wizard e pelo servidor (aceita ponto ou vírgula);
  // a regra "number" do jQuery Validation usa a cultura do navegador e rejeitaria o ponto.
  ['Latitude', 'Longitude'].forEach(function (id) {
    var campo = document.getElementById(id);
    if (!campo) return;
    ['data-val-number', 'data-val-range', 'data-val-range-min', 'data-val-range-max'].forEach(function (a) {
      campo.removeAttribute(a);
    });
  });

  if (typeof L !== 'undefined') {
    var inputLat = document.getElementById('Latitude');
    var inputLng = document.getElementById('Longitude');
    var mapa = L.map('mapa-picker');
    window.__mapaPicker = mapa;

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap'
    }).addTo(mapa);

    var marcador = null;

    function avisarManual() { document.dispatchEvent(new CustomEvent('zitei:ponto-manual')); }

    function gravarCampos(latlng) {
      inputLat.value = latlng.lat.toFixed(7);
      inputLng.value = latlng.lng.toFixed(7);
    }

    function marcar(lat, lng, centralizar) {
      if (marcador) {
        marcador.setLatLng([lat, lng]);
      } else {
        // Arrastável: o usuário pode ajustar o ponto fino depois de localizar o endereço.
        marcador = L.marker([lat, lng], { draggable: true, title: 'Arraste para ajustar' }).addTo(mapa);
        marcador.on('dragend', function () {
          gravarCampos(marcador.getLatLng());
          avisarManual();
        });
      }
      if (centralizar) mapa.setView([lat, lng], Math.max(mapa.getZoom(), 16));
    }

    function lerCampos() {
      var lat = parseFloat(String(inputLat.value).replace(',', '.'));
      var lng = parseFloat(String(inputLng.value).replace(',', '.'));
      return (isFinite(lat) && isFinite(lng)) ? [lat, lng] : null;
    }

    var inicial = lerCampos();
    if (inicial) { mapa.setView(inicial, 16); marcar(inicial[0], inicial[1], false); }
    else mapa.setView([-21.1775, -47.8103], 12); // Ribeirão Preto/SP

    mapa.on('click', function (e) {
      gravarCampos(e.latlng);
      marcar(parseFloat(inputLat.value), parseFloat(inputLng.value), false);
      avisarManual();
    });

    function aoDigitar() {
      var p = lerCampos();
      if (p) { marcar(p[0], p[1], true); avisarManual(); }
    }
    inputLat.addEventListener('change', aoDigitar);
    inputLng.addEventListener('change', aoDigitar);

    function limpar() {
      inputLat.value = '';
      inputLng.value = '';
      if (marcador) { mapa.removeLayer(marcador); marcador = null; }
    }
    document.getElementById('btn-limpar-mapa').addEventListener('click', function () { limpar(); avisarManual(); });

    // API usada pelo geocodificador (atividade-geo.js). Não dispara "ponto manual".
    window.__zitei = {
      definirPonto: function (lat, lng) {
        inputLat.value = Number(lat).toFixed(7);
        inputLng.value = Number(lng).toFixed(7);
        marcar(Number(lat), Number(lng), true);
      },
      limparPonto: limpar,
      temPonto: function () { return lerCampos() !== null; }
    };
  }

  // Se o servidor devolveu erros, abre o primeiro passo que contém um deles.
  var comErro = form.querySelector('.field-validation-error, .input-validation-error');
  if (comErro) {
    var secaoErro = comErro.closest('[data-step]');
    if (secaoErro) mostrar(Number(secaoErro.dataset.step));
  }
})();
