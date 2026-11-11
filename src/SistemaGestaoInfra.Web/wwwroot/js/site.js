// Zitei — comportamento do layout
(function () {
  'use strict';

  var body = document.body;
  var botao = document.getElementById('zt-toggle-sidebar');
  var overlay = document.getElementById('zt-overlay');

  function alternar(abrir) {
    var aberta = typeof abrir === 'boolean' ? abrir : !body.classList.contains('zt-sidebar-aberta');
    body.classList.toggle('zt-sidebar-aberta', aberta);
    if (botao) botao.setAttribute('aria-expanded', String(aberta));
  }

  if (botao) botao.addEventListener('click', function () { alternar(); });
  if (overlay) overlay.addEventListener('click', function () { alternar(false); });

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') alternar(false);
  });
})();
