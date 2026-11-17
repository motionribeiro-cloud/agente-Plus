// Soma: propriedade NUMÉRICA com keyframes, por exemplo Rotation.
// Não usar em Position/Scale vetoriais sem adaptação. Não validado no AE.
var amplitude = 12;
var frequency = 3;
var damping = 6;
var result = value;
if (numKeys > 0 && typeof value === "number") {
  var start = key(numKeys).time;
  var elapsed = time - start;
  if (elapsed >= 0) {
    result = value + amplitude * Math.sin(2 * Math.PI * frequency * elapsed) * Math.exp(-damping * elapsed);
  }
}
result;
