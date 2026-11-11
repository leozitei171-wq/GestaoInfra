// Zitei — formulário de rateio: carrega itens elegíveis e mostra prévia da divisão.
// A prévia é só visual; o servidor recalcula tudo a partir do banco.
(function () {
  'use strict';

  var form = document.getElementById('form-rateio');
  if (!form) return;

  var url = document.getElementById('rateio-config').dataset.url;
  var corpo = document.getElementById('itens');
  var blocoModo = document.getElementById('bloco-modo');
  var blocoItens = document.getElementById('bloco-itens');
  var btnGerar = document.getElementById('btn-gerar');
  var moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
  var numero4 = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 });
  var itens = []; // [{id, rotulo, detalhe, base, jaRateado}]

  function valorTotal() {
    var t = document.getElementById('valor-total').value.trim();
    var p = t.lastIndexOf('.'), v = t.lastIndexOf(',');
    if (p >= 0 && v >= 0) t = v > p ? t.replace(/\./g, '').replace(',', '.') : t.replace(/,/g, '');
    else t = t.replace(',', '.');
    var n = parseFloat(t);
    return isFinite(n) && n > 0 ? n : 0;
  }

  function modo() { return form.querySelector('input[name=Modo]:checked').value; }

  function setoresMarcados() {
    return Array.prototype.map.call(document.querySelectorAll('.sel-setor:checked'), function (c) { return c.value; });
  }

  // Maior resto, em centavos inteiros (espelha o cálculo do servidor).
  function dividir(totalReais, pesos) {
    var soma = pesos.reduce(function (a, b) { return a + b; }, 0);
    var alvo = Math.round(totalReais * 100);
    if (!soma || !alvo) return pesos.map(function () { return 0; });
    var parcelas = pesos.map(function (p) { return Math.floor(alvo * p / soma); });
    var restos = pesos.map(function (p, i) { return alvo * p / soma - parcelas[i]; });
    var faltam = alvo - parcelas.reduce(function (a, b) { return a + b; }, 0);
    pesos.map(function (_, i) { return i; })
      .sort(function (a, b) { return restos[b] - restos[a] || a - b; })
      .slice(0, faltam).forEach(function (i) { parcelas[i]++; });
    return parcelas.map(function (c) { return c / 100; });
  }

  function marcados() { return corpo.querySelectorAll('.sel-item:checked'); }

  function atualizar() {
    var sel = marcados();
    var ids = Array.prototype.map.call(sel, function (c) { return c.value; });
    var selecionados = itens.filter(function (i) { return ids.indexOf(String(i.id)) >= 0; });
    var prev = dividir(valorTotal(), selecionados.map(function (i) { return i.base; }));
    var porId = {};
    selecionados.forEach(function (i, n) { porId[i.id] = prev[n]; });

    corpo.querySelectorAll('tr').forEach(function (tr) {
      var cel = tr.querySelector('.previa');
      cel.textContent = porId[tr.dataset.id] !== undefined && valorTotal() > 0 ? moeda.format(porId[tr.dataset.id]) : '—';
    });

    var somaBase = selecionados.reduce(function (a, i) { return a + i.base; }, 0);
    document.getElementById('resumo-sel').textContent = selecionados.length
      ? selecionados.length + ' item(ns) selecionado(s)' : 'Nenhum item selecionado.';
    document.getElementById('resumo-soma').textContent = selecionados.length
      ? 'Base total: ' + moeda.format(somaBase) : '';
    btnGerar.disabled = !(selecionados.length && valorTotal() > 0);
  }

  function celula(texto, classe) {
    var td = document.createElement('td');
    if (classe) td.className = classe;
    td.textContent = texto;
    return td;
  }

  function desenhar() {
    corpo.textContent = '';
    document.getElementById('vazio').classList.toggle('d-none', itens.length > 0);
    document.getElementById('col-rotulo').textContent = modo() === '1' ? 'Contrato' : 'Atividade';
    document.getElementById('col-base').textContent = modo() === '1' ? 'Valor total' : 'Custo base';
    document.getElementById('titulo-itens').textContent = modo() === '1' ? '3. Seleção de contratos' : '3. Seleção de atividades';

    itens.forEach(function (i) {
      var tr = document.createElement('tr');
      tr.dataset.id = i.id;
      var tdCheck = document.createElement('td');
      var cb = document.createElement('input');
      cb.type = 'checkbox'; cb.name = 'ItemIds'; cb.value = i.id; cb.className = 'form-check-input sel-item';
      cb.setAttribute('aria-label', 'Selecionar ' + i.rotulo);
      tdCheck.appendChild(cb); tr.appendChild(tdCheck);

      var tdRot = celula(i.rotulo + ' ', 'fw-semibold text-nowrap');
      if (i.jaRateado) {
        var b = document.createElement('span');
        b.className = 'badge text-bg-light border'; b.textContent = 'já rateado';
        tdRot.appendChild(b);
      }
      tr.appendChild(tdRot);
      tr.appendChild(celula(i.detalhe || '', 'text-truncate'));
      tr.lastChild.style.maxWidth = '280px';
      tr.appendChild(celula(moeda.format(i.base), 'text-end text-nowrap'));
      tr.appendChild(celula('—', 'text-end text-nowrap previa'));
      corpo.appendChild(tr);
    });
    atualizar();
  }

  function carregar() {
    var setores = setoresMarcados();
    blocoModo.classList.toggle('d-none', setores.length === 0);
    blocoItens.classList.toggle('d-none', setores.length === 0);
    if (!setores.length) { itens = []; desenhar(); return; }

    var q = new URLSearchParams();
    q.set('modo', modo());
    setores.forEach(function (s) { q.append('setores', s); });

    fetch(url + '?' + q.toString(), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function (dados) { itens = dados; desenhar(); })
      .catch(function () { itens = []; desenhar(); });
  }

  document.getElementById('setores').addEventListener('change', carregar);
  form.querySelectorAll('input[name=Modo]').forEach(function (r) { r.addEventListener('change', carregar); });
  document.getElementById('valor-total').addEventListener('input', atualizar);
  corpo.addEventListener('change', atualizar);

  function marcarTodos(filtro) {
    corpo.querySelectorAll('.sel-item').forEach(function (cb) {
      var item = itens.filter(function (i) { return String(i.id) === cb.value; })[0];
      cb.checked = filtro(item);
    });
    atualizar();
  }
  document.getElementById('btn-todos').addEventListener('click', function () { marcarTodos(function () { return true; }); });
  document.getElementById('btn-sem-rateio').addEventListener('click', function () { marcarTodos(function (i) { return !i.jaRateado; }); });
  document.getElementById('btn-nenhum').addEventListener('click', function () { marcarTodos(function () { return false; }); });

  carregar();
})();
