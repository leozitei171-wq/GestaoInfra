// Zitei — formulário de contrato: itens dinâmicos e total calculado.
// O servidor recalcula o total; aqui é só conveniência visual.
(function () {
  'use strict';

  var corpo = document.getElementById('itens');
  var modelo = document.getElementById('modelo-item');
  if (!corpo || !modelo) return;

  var moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

  // Aceita "1.234,56", "1234,56" e "1234.56".
  function numero(texto) {
    var t = String(texto || '').trim();
    if (!t) return NaN;
    var p = t.lastIndexOf('.'), v = t.lastIndexOf(',');
    if (p >= 0 && v >= 0) t = v > p ? t.replace(/\./g, '').replace(',', '.') : t.replace(/,/g, '');
    else t = t.replace(',', '.');
    return parseFloat(t);
  }

  function renumerar() {
    corpo.querySelectorAll('.item-linha').forEach(function (linha, n) {
      linha.querySelectorAll('[name]').forEach(function (campo) {
        campo.name = campo.name.replace(/Itens\[(\d+|__)\]/, 'Itens[' + n + ']');
      });
    });
  }

  function recalcular() {
    var total = 0;
    corpo.querySelectorAll('.item-linha').forEach(function (linha) {
      var q = numero(linha.querySelector('.item-qtd').value);
      var v = numero(linha.querySelector('.item-valor').value);
      var celula = linha.querySelector('.item-subtotal');
      if (isFinite(q) && isFinite(v)) {
        var sub = q * v;
        total += sub;
        celula.textContent = moeda.format(sub);
      } else {
        celula.textContent = '—';
      }
    });
    document.getElementById('total-contrato').textContent = moeda.format(total);
  }

  document.getElementById('btn-add-item').addEventListener('click', function () {
    corpo.appendChild(modelo.content.cloneNode(true));
    renumerar();
    var ultimo = corpo.querySelector('.item-linha:last-child select');
    if (ultimo) ultimo.focus();
  });

  corpo.addEventListener('click', function (e) {
    var botao = e.target.closest('.btn-remover');
    if (!botao) return;
    var linhas = corpo.querySelectorAll('.item-linha');
    if (linhas.length === 1) {
      // Mantém sempre uma linha: apenas limpa os campos.
      linhas[0].querySelectorAll('input:not([type=hidden]), select').forEach(function (c) { c.value = ''; });
    } else {
      botao.closest('.item-linha').remove();
      renumerar();
    }
    recalcular();
  });

  corpo.addEventListener('input', recalcular);
  recalcular();
})();
