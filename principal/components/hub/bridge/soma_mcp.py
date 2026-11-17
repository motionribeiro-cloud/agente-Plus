"""Scoped stdio MCP endpoint for one Soma application. No third-party dependencies.

The desktop client launches this adapter. It reaches the existing authenticated
loopback Hub, which forwards only to the application selected on the command line.
No credentials are copied into client configs or sent to stdout.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
HERE = str(Path(__file__).resolve().parent)
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import soma_knowledge as knowledge

APPS = {
    "aftereffects": "After Effects", "photoshop": "Photoshop", "illustrator": "Illustrator",
    "premierepro": "Premiere Pro", "blender": "Blender", "cinema4d": "Cinema 4D", "figma": "Figma",
}
PROTOCOLS = {"2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"}
MAX_LINE = 16 * 1024 * 1024


def token_path():
    if sys.platform == "win32":
        return Path(os.environ.get("APPDATA", Path.home() / "AppData/Roaming")) / "AE Agent/hub-token.txt"
    return Path.home() / "Library/Application Support/AE Agent/hub-token.txt"


class Hub:
    def __init__(self, url="http://127.0.0.1:47821/mcp", token_file=None):
        parsed = urllib.parse.urlsplit(url)
        if parsed.scheme != "http" or parsed.hostname != "127.0.0.1" or parsed.username or parsed.password or parsed.path != "/mcp" or parsed.query or parsed.fragment:
            raise ValueError("O adaptador aceita somente o Hub HTTP local em 127.0.0.1 e caminho /mcp.")
        self.url = url
        self.token_file = Path(token_file) if token_file else token_path()
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect())

    def call(self, name, arguments):
        try:
            token = self.token_file.read_text(encoding="utf-8").strip()
        except OSError:
            raise RuntimeError("Soma Hub sem credencial local. Inicie o Hub e tente novamente.") from None
        if not token:
            raise RuntimeError("Credencial local do Hub vazia. Reinicie o Hub.")
        body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": name, "arguments": arguments}}).encode("utf-8")
        request = urllib.request.Request(self.url, data=body, headers={
            "Content-Type": "application/json", "Accept": "application/json, text/event-stream", "Authorization": "Bearer " + token,
        }, method="POST")
        try:
            with self.opener.open(request, timeout=300 if name == "call_app_tool" else 8) as response:
                reply = json.load(response)
        except urllib.error.HTTPError as error:
            if error.code in (401, 403):
                raise RuntimeError("Hub recusou a credencial local. Reinicie o Hub; não é o login do Claude.") from None
            raise RuntimeError("Hub retornou HTTP %s." % error.code) from None
        except (OSError, ValueError):
            raise RuntimeError("Soma Hub indisponível ou resposta inválida. Abra Iniciar-Soma-Hub.cmd.") from None
        if reply.get("error"):
            raise RuntimeError("Erro de protocolo no Soma Hub.")
        return reply.get("result") or {}

    @staticmethod
    def text_result(result):
        if result.get("isError"):
            # Do not print raw transport errors or authorization headers.
            raise RuntimeError("Aplicativo indisponível. Abra o aplicativo e o painel Soma; tente novamente.")
        for content in result.get("content", []):
            if content.get("type") == "text":
                return json.loads(content["text"])
        raise RuntimeError("Hub não retornou um resultado de texto válido.")


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise urllib.error.HTTPError(req.full_url, code, "Redirecionamento recusado", headers, fp)


class Adapter:
    def __init__(self, app, hub):
        if app not in APPS:
            raise ValueError("Aplicativo não suportado")
        self.app, self.hub = app, hub

    def tools(self):
        label = APPS[self.app]
        return [
            {"name": "soma_status", "description": "Verifica se %s está aberto com o Soma. Não altera documentos." % label,
             "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
             "annotations": {"readOnlyHint": True, "destructiveHint": False, "openWorldHint": False}},
            {"name": "soma_get_tools", "description": "Lista os comandos e schemas de %s. Leia antes de executar uma ação." % label,
             "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
             "annotations": {"readOnlyHint": True, "destructiveHint": False, "openWorldHint": False}},
            {"name": "soma_call_tool", "description": "Executa um comando SOMENTE em %s. Inspecione antes de editar e confira o resultado. Escritas dependem da autorização do usuário no cliente." % label,
             "inputSchema": {"type": "object", "properties": {"tool": {"type": "string"}, "arguments": {"type": "object"}}, "required": ["tool"], "additionalProperties": False},
             "annotations": {"readOnlyHint": False, "destructiveHint": True, "openWorldHint": True}},
        ] + knowledge.TOOLS

    @staticmethod
    def content(value, error=False):
        return {"content": [{"type": "text", "text": json.dumps(value, ensure_ascii=False)}], "isError": error}

    def handle(self, message):
        if not isinstance(message, dict) or message.get("jsonrpc") != "2.0":
            return {"jsonrpc": "2.0", "id": None, "error": {"code": -32600, "message": "Invalid Request"}}
        if "id" not in message:
            return None
        request_id = message["id"]

        def result(value):
            return {"jsonrpc": "2.0", "id": request_id, "result": value}

        def error(code, text):
            return {"jsonrpc": "2.0", "id": request_id, "error": {"code": code, "message": text}}

        method, params = message.get("method"), message.get("params", {})
        if not isinstance(params, dict):
            return error(-32602, "Invalid params")
        if method == "initialize":
            requested = params.get("protocolVersion")
            return result({"protocolVersion": requested if requested in PROTOCOLS else "2025-11-25",
                           "capabilities": {"tools": {"listChanged": False}},
                           "serverInfo": {"name": "soma_" + self.app, "version": "1.9.0-preview"},
                           "instructions": "Controle limitado a %s. Use soma_status, leia soma_get_tools e execute soma_call_tool apenas conforme o pedido do usuário. Abra o aplicativo e seu painel Soma antes de operar." % APPS[self.app] + " " + knowledge.INSTRUCTIONS})
        if method == "ping":
            return result({})
        if method == "tools/list":
            return result({"tools": self.tools()})
        if method != "tools/call":
            return error(-32601, "Method not found")
        name, arguments = params.get("name"), params.get("arguments", {})
        if not isinstance(arguments, dict):
            return error(-32602, "Arguments must be an object")
        knowledge_tool = next((item for item in knowledge.TOOLS if item["name"] == name), None)
        expected = set(knowledge_tool["inputSchema"]["properties"]) if knowledge_tool else ({"tool", "arguments"} if name == "soma_call_tool" else set())
        if set(arguments) - expected:
            return error(-32602, "Unsupported arguments; the application scope cannot be changed")
        try:
            if knowledge_tool:
                return result(self.content(knowledge.Library().tool(name, arguments)))
            if name == "soma_status":
                data = self.hub.text_result(self.hub.call("list_apps", {}))
                available = any(item.get("app") == self.app for item in data.get("apps", []))
                return result(self.content({"app": self.app, "available": available,
                                           "message": "Pronto para inspeção" if available else "Abra o aplicativo e seu painel Soma"}))
            if name == "soma_get_tools":
                data = self.hub.text_result(self.hub.call("get_app_tools", {"app": self.app}))
                return result(self.content(data))
            if name == "soma_call_tool":
                tool, tool_args = arguments.get("tool"), arguments.get("arguments", {})
                if not isinstance(tool, str) or not tool or not isinstance(tool_args, dict):
                    return error(-32602, "Expected tool name and object arguments")
                data = self.hub.text_result(self.hub.call("get_app_tools", {"app": self.app}))
                if tool not in {item.get("name") for item in data.get("tools", [])}:
                    return error(-32602, "Tool not exposed by this application")
                return result(self.hub.call("call_app_tool", {"app": self.app, "tool": tool, "arguments": tool_args}))
            return error(-32602, "Unknown tool")
        except (RuntimeError, ValueError, TypeError):
            return result(self.content({"app": self.app, "ok": False, "message": "Não foi possível acessar este aplicativo. Verifique o Soma Hub, a credencial local e se o painel está aberto."}, True))


def ensure_hub(hub):
    """Start only the bundled local helper; never an agent turn or an application."""
    try:
        hub.call("list_apps", {})
        return
    except RuntimeError:
        pass
    helper = Path(__file__).with_name("soma_hub.py")
    flags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
    subprocess.Popen([sys.executable, str(helper)], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                     cwd=str(helper.parent), creationflags=flags, close_fds=True)
    for _ in range(30):
        try:
            hub.call("list_apps", {})
            return
        except RuntimeError:
            time.sleep(0.2)
    raise RuntimeError("Não foi possível iniciar o Soma Hub local.")


def serve(adapter, source, target):
    while True:
        line = source.readline(MAX_LINE + 1)
        if not line:
            return
        if len(line) > MAX_LINE:
            raise ValueError("Mensagem MCP excede o limite")
        try:
            message = json.loads(line)
        except ValueError:
            reply = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "Parse error"}}
        else:
            reply = adapter.handle(message)
        if reply is not None:
            target.write(json.dumps(reply, ensure_ascii=False) + "\n")
            target.flush()


def main():
    parser = argparse.ArgumentParser(description="MCP local Soma por aplicativo")
    parser.add_argument("--app", choices=sorted(APPS), required=True)
    parser.add_argument("--hub-url", default="http://127.0.0.1:47821/mcp")
    parser.add_argument("--token-file")
    parser.add_argument("--ensure-hub", action="store_true")
    options = parser.parse_args()
    hub = Hub(options.hub_url, options.token_file)
    if options.ensure_hub:
        try:
            ensure_hub(hub)
        except RuntimeError:
            print("Soma Hub não iniciou; soma_status informará indisponibilidade.", file=sys.stderr)
    # Embedded Python may default to a Windows code page. MCP stdio is UTF-8.
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding="utf-8")
        sys.stdout.reconfigure(encoding="utf-8")
    serve(Adapter(options.app, hub), sys.stdin, sys.stdout)


if __name__ == "__main__":
    main()
