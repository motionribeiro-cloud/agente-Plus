(function (root, factory) {
  var api = factory();
  if (typeof module === "object" && module.exports) { module.exports = api; }
  root.AEClaudeHealth = api;
}(typeof window !== "undefined" ? window : this, function () {
  "use strict";
  function classify(result, stderr, exitCode) {
    result = result || {};
    var detail = String(result.result || (result.errors || []).join(" ") || stderr || "");
    if (/oauth.*(?:revoked|expired)|access token.*(?:revoked|expired)/i.test(detail)) {
      return { ok: false, auth: true, code: "auth_revoked", message: "A autorização do Claude expirou ou foi revogada. Clique em Connect Claude, conclua o login e depois em Check again." };
    }
    if (/authentication_failed|failed to authenticate|invalid.*api.?key|\b401\b|unauthorized/i.test(detail)) {
      return { ok: false, auth: true, code: "auth_failed", message: "O Claude recusou a autenticação. Reconecte sua conta em Connect Claude e repita Check again." };
    }
    if (exitCode === 0 && result.type === "result" && result.is_error === false) {
      return { ok: true, auth: false, code: "authenticated", message: "Conexão com o Claude validada por uma chamada real." };
    }
    if (/rate.?limit|usage.?limit|\b429\b|credit balance|quota/i.test(detail)) {
      return { ok: false, auth: false, code: "usage_limit", message: "O Claude informou um limite de uso ou saldo. Isso não comprova falha de login." };
    }
    return { ok: false, auth: false, code: "connection_error", message: "Não foi possível validar a chamada ao Claude. Verifique rede, CLI e plano; o login salvo, sozinho, não confirma a conexão." };
  }
  return { classify: classify };
}));
