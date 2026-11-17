"""Distinguish saved credentials from credentials accepted by an actual request."""
import re


def classify(result=None, stderr="", exit_code=1):
    result = result or {}
    detail = str(result.get("result") or " ".join(result.get("errors") or []) or stderr)
    if re.search(r"oauth.*(?:revoked|expired)|access token.*(?:revoked|expired)", detail, re.I):
        return {"ok": False, "auth": True, "code": "auth_revoked", "message": "A autorização do Claude expirou ou foi revogada. Clique em Conectar, conclua o login e repita a verificação."}
    if re.search(r"authentication_failed|failed to authenticate|invalid.*api.?key|\b401\b|unauthorized", detail, re.I):
        return {"ok": False, "auth": True, "code": "auth_failed", "message": "O Claude recusou a autenticação. Reconecte sua conta."}
    if exit_code == 0 and result.get("type") == "result" and result.get("is_error") is False:
        return {"ok": True, "auth": False, "code": "authenticated", "message": "Conexão com o Claude validada por uma chamada real."}
    if re.search(r"rate.?limit|usage.?limit|\b429\b|credit balance|quota", detail, re.I):
        return {"ok": False, "auth": False, "code": "usage_limit", "message": "O Claude informou um limite de uso ou saldo; isso não comprova falha de login."}
    return {"ok": False, "auth": False, "code": "connection_error", "message": "Chamada ao Claude não validada. Verifique rede, CLI e plano."}
