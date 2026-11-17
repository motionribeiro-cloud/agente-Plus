"use strict";
var assert = require("assert"), fs = require("fs"), path = require("path"), vm = require("vm");
var root = process.argv[2] || path.join(__dirname, "..", "soma-conhecimento-1.0.0");
function evaluate(file, context) { return vm.runInNewContext(fs.readFileSync(path.join(root, "expressoes", file), "utf8"), context, { timeout: 1000 }); }
assert.strictEqual(evaluate("ease-opacity.jsx", { time: -1, inPoint: 0 }), 0);
assert.strictEqual(evaluate("ease-opacity.jsx", { time: 0.3, inPoint: 0 }), 50);
assert.strictEqual(evaluate("ease-opacity.jsx", { time: 2, inPoint: 0 }), 100);
assert.strictEqual(evaluate("overshoot-scalar.jsx", { value: 25, numKeys: 0, time: 0 }), 25);
assert.strictEqual(evaluate("overshoot-scalar.jsx", { value: 25, numKeys: 1, time: 0, key: function () { return { time: 1 }; } }), 25);
var sampled = evaluate("overshoot-scalar.jsx", { value: 25, numKeys: 1, time: 1.1, key: function () { return { time: 1 }; } });
assert(Number.isFinite(sampled) && sampled > 25);
var before = evaluate("bounce-position.jsx", { time: -1, inPoint: 0, value: [100, 200, 50] });
assert.strictEqual(before.join(","), "100,200,50");
var bounce = evaluate("bounce-position.jsx", { time: 0.1, inPoint: 0, value: [100, 200, 50] });
assert.strictEqual(bounce[0], 100); assert.strictEqual(bounce[2], 50); assert(bounce[1] < 200 && bounce[1] > 160);
console.log("PASS: expressões em amostras JavaScript; isso não valida o motor nativo do After Effects.");
