// Zitei — gráficos da tela de indicadores (ApexCharts)
(function () {
  'use strict';

  var fonte = document.getElementById('dados-indicadores');
  if (!fonte || typeof ApexCharts === 'undefined') return;

  var d = JSON.parse(fonte.textContent);
  var verde = '#0f766e', ambar = '#f59e0b';
  var fonteBase = { fontFamily: 'Public Sans, system-ui, sans-serif' };

  function barras(seletor, serie, cor) {
    new ApexCharts(document.querySelector(seletor), {
      chart: Object.assign({ type: 'bar', height: 300, toolbar: { show: false } }, fonteBase),
      series: [{ name: 'Demandas', data: serie.map(function (p) { return p.valor; }) }],
      xaxis: { categories: serie.map(function (p) { return p.rotulo; }), labels: { rotate: -35, trimWords: false } },
      yaxis: { labels: { formatter: function (v) { return Math.round(v); } }, forceNiceScale: true },
      plotOptions: { bar: { borderRadius: 4, columnWidth: '55%' } },
      colors: [cor], dataLabels: { enabled: true }, grid: { borderColor: '#e5e7eb' }
    }).render();
  }

  barras('#graf-setor', d.setor, verde);
  barras('#graf-tipo', d.tipo, ambar);

  new ApexCharts(document.querySelector('#graf-status'), {
    chart: Object.assign({ type: 'donut', height: 300 }, fonteBase),
    series: d.status.map(function (p) { return p.valor; }),
    labels: d.status.map(function (p) { return p.rotulo; }),
    colors: d.cores,
    legend: { position: 'bottom' },
    dataLabels: { enabled: true }
  }).render();

  new ApexCharts(document.querySelector('#graf-linha'), {
    chart: Object.assign({ type: 'line', height: 300, toolbar: { show: false } }, fonteBase),
    series: [
      { name: 'Abertas', data: d.abertas.map(function (p) { return p.valor; }) },
      { name: 'Concluídas', data: d.concluidas.map(function (p) { return p.valor; }) }
    ],
    xaxis: { categories: d.abertas.map(function (p) { return p.rotulo; }), tickAmount: 10, labels: { rotate: 0 } },
    yaxis: { labels: { formatter: function (v) { return Math.round(v); } }, forceNiceScale: true },
    colors: [verde, '#049a15'], stroke: { width: 3, curve: 'smooth' },
    markers: { size: 3 }, legend: { position: 'top' }, grid: { borderColor: '#e5e7eb' }
  }).render();
})();
