// Soma: Opacity 0..100, iniciada no inPoint. Não requer keyframes.
// Exemplo sem validação nativa no After Effects.
var duration = 0.6;
var u = Math.max(0, Math.min(1, (time - inPoint) / duration));
100 * u * u * (3 - 2 * u);
