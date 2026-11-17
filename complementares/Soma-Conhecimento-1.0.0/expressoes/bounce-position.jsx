// Soma: Position 2D/3D. Rebote visual, não simulação física.
// Não usar com dimensões separadas. Não validado no AE.
var amplitude = 40;
var frequency = 3;
var damping = 5;
var elapsed = Math.max(0, time - inPoint);
var offsetY = time < inPoint ? 0 : amplitude * Math.abs(Math.sin(Math.PI * frequency * elapsed)) * Math.exp(-damping * elapsed);
var result = [];
for (var axis = 0; axis < value.length; axis++) {
  result[axis] = value[axis] - (axis === 1 ? offsetY : 0);
}
result;
