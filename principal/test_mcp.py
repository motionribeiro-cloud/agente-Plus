import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import unittest
from unittest import mock
from types import SimpleNamespace
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = Path(__file__).parent
BRIDGE = ROOT / "components/hub/bridge/soma_mcp.py"
sys.path.insert(0, str(BRIDGE.parent))
spec = importlib.util.spec_from_file_location("soma_mcp", BRIDGE)
mcp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mcp)
health_spec = importlib.util.spec_from_file_location("health", ROOT / "components/hub/bridge/soma_core/claude_health.py")
health = importlib.util.module_from_spec(health_spec)
health_spec.loader.exec_module(health)


class FakeHub:
    def __init__(self):
        self.calls = []

    @staticmethod
    def text_result(result):
        return mcp.Hub.text_result(result)

    def call(self, name, args):
        self.calls.append((name, args))
        if name == "list_apps":
            return mcp.Adapter.content({"apps": [{"app": "aftereffects", "name": "After Effects"}]})
        if name == "get_app_tools":
            return mcp.Adapter.content({"app": args["app"], "tools": [{"name": "inspect", "inputSchema": {"type": "object"}}]})
        return {"content": [{"type": "text", "text": "ok"}, {"type": "image", "mimeType": "image/png", "data": "test"}], "isError": False}


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.hub = FakeHub()
        self.adapter = mcp.Adapter("aftereffects", self.hub)

    def call(self, name, args=None):
        return self.adapter.handle({"jsonrpc": "2.0", "id": 0, "method": "tools/call", "params": {"name": name, "arguments": args or {}}})

    def test_initialize_and_notification_and_zero_id(self):
        r = self.adapter.handle({"jsonrpc": "2.0", "id": 0, "method": "initialize", "params": {"protocolVersion": "2025-06-18"}})
        self.assertEqual(r["id"], 0)
        self.assertEqual(r["result"]["protocolVersion"], "2025-06-18")
        self.assertIsNone(self.adapter.handle({"jsonrpc": "2.0", "method": "notifications/initialized"}))

    def test_all_seven_apps_have_isolated_names_and_write_annotations(self):
        for app in mcp.APPS:
            instance = mcp.Adapter(app, self.hub)
            tools = instance.tools()
            self.assertTrue(tools[0]["annotations"]["readOnlyHint"])
            self.assertFalse(tools[2]["annotations"]["readOnlyHint"])
            self.assertNotIn("app", tools[2]["inputSchema"]["properties"])

    def test_tools_available_even_when_host_closed(self):
        r = mcp.Adapter("figma", self.hub).handle({"jsonrpc": "2.0", "id": 1, "method": "tools/list"})
        self.assertEqual(len(r["result"]["tools"]), 6)
        self.assertEqual(self.hub.calls, [])

    def test_reject_cross_application_override(self):
        r = self.call("soma_call_tool", {"app": "photoshop", "tool": "inspect"})
        self.assertEqual(r["error"]["code"], -32602)
        self.assertEqual(self.hub.calls, [])

    def test_reject_unknown_native_tool(self):
        r = self.call("soma_call_tool", {"tool": "unlisted"})
        self.assertEqual(r["error"]["code"], -32602)
        self.assertTrue(all(name != "call_app_tool" for name, _ in self.hub.calls))

    def test_forward_only_selected_app_and_preserve_images(self):
        r = self.call("soma_call_tool", {"tool": "inspect", "arguments": {"x": 1}})
        self.assertEqual(self.hub.calls[-1], ("call_app_tool", {"app": "aftereffects", "tool": "inspect", "arguments": {"x": 1}}))
        self.assertEqual(r["result"]["content"][1]["type"], "image")

    def test_status_reports_closed_app(self):
        r = mcp.Adapter("figma", self.hub).handle({"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": "soma_status"}})
        self.assertFalse(json.loads(r["result"]["content"][0]["text"])["available"])

    def test_errors_do_not_leak_credentials(self):
        self.hub.call = lambda *_: (_ for _ in ()).throw(RuntimeError("Bearer sensitive-test-value"))
        r = self.call("soma_status")
        self.assertTrue(r["result"]["isError"])
        self.assertNotIn("sensitive-test-value", json.dumps(r))

    def test_invalid_json_and_notifications_keep_stdout_clean(self):
        source = io.StringIO('not-json\n{"jsonrpc":"2.0","method":"notifications/initialized"}\n{"jsonrpc":"2.0","id":1,"method":"ping"}\n')
        target = io.StringIO()
        mcp.serve(self.adapter, source, target)
        lines = target.getvalue().splitlines()
        self.assertEqual(len(lines), 2)
        self.assertEqual(json.loads(lines[0])["error"]["code"], -32700)
        self.assertEqual(json.loads(lines[1])["result"], {})

    def test_cannot_send_auth_to_remote_server(self):
        for url in ["https://example.com/mcp", "http://localhost:47821/mcp", "http://127.0.0.1/mcp?token=bad", "http://127.0.0.1/other"]:
            with self.assertRaises(ValueError):
                mcp.Hub(url)

    def test_real_stdio_subprocess_handshake(self):
        incoming = '\n'.join(json.dumps(x) for x in [
            {"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2025-11-25"}},
            {"jsonrpc": "2.0", "method": "notifications/initialized"},
            {"jsonrpc": "2.0", "id": 2, "method": "tools/list"}]) + '\n'
        process = subprocess.run([sys.executable, str(BRIDGE), "--app", "aftereffects"], input=incoming, capture_output=True, text=True, timeout=10)
        self.assertEqual(process.returncode, 0)
        replies = [json.loads(line) for line in process.stdout.splitlines()]
        self.assertEqual(len(replies), 2)
        self.assertEqual(replies[0]["result"]["serverInfo"]["name"], "soma_aftereffects")

    def test_real_loopback_auth_and_read_token_again_after_rotation(self):
        seen = []
        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):
                seen.append(self.headers.get("Authorization"))
                request = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                body = json.dumps({"jsonrpc": "2.0", "id": request["id"], "result": mcp.Adapter.content({"apps": []})}).encode()
                self.send_response(200)
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            def log_message(self, *_):
                pass
        server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            with tempfile.TemporaryDirectory(prefix="soma-mcp-test-") as directory:
                token = Path(directory) / "token.txt"
                token.write_text("first", encoding="utf-8")
                hub = mcp.Hub("http://127.0.0.1:%d/mcp" % server.server_address[1], token)
                hub.call("list_apps", {})
                token.write_text("second", encoding="utf-8")
                hub.call("list_apps", {})
                self.assertEqual(seen, ["Bearer first", "Bearer second"])
        finally:
            server.shutdown()
            server.server_close()


class ClaudeHealthTests(unittest.TestCase):
    def test_revoked_oauth_even_when_subtype_says_success(self):
        value = health.classify({"type": "result", "subtype": "success", "is_error": True, "result": "Failed to authenticate. API Error: 401 OAuth access token has been revoked."})
        self.assertEqual(value["code"], "auth_revoked")
        self.assertTrue(value["auth"])

    def test_expired_session_unrefreshable(self):
        self.assertTrue(health.classify({"result": "OAuth session expired and could not be refreshed"})["auth"])

    def test_network_or_usage_limit_does_not_log_user_out(self):
        self.assertFalse(health.classify(stderr="429 rate limit")["auth"])
        self.assertFalse(health.classify(stderr="ECONNRESET")["auth"])

    def test_only_real_success_confirms_connection(self):
        self.assertFalse(health.classify({"loggedIn": True}, exit_code=0)["ok"])
        self.assertTrue(health.classify({"type": "result", "is_error": False}, exit_code=0)["ok"])


class ClaudeDiscoveryTests(unittest.TestCase):
    def exercise(self, explicit):
        spec = importlib.util.spec_from_file_location("discovery", ROOT / "components/hub/bridge/soma_core/system.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        with tempfile.TemporaryDirectory(prefix="soma-claude-discovery-") as directory:
            base = Path(directory)
            old = base / "home/.local/bin/claude.exe"
            new = base / "roaming/Claude/claude-code/2.1.325/hash/claude.exe"
            for binary in [old, new]:
                binary.parent.mkdir(parents=True, exist_ok=True)
                binary.write_text("fixture", encoding="utf-8")
            env = {"APPDATA": str(base / "roaming"), "LOCALAPPDATA": str(base / "local"), "CLAUDE_BIN": str(old) if explicit else ""}
            def version(args, **_):
                return SimpleNamespace(stdout="2.1.325 (Claude Code)" if args[0] == str(new) else "2.1.158 (Claude Code)")
            with mock.patch.object(module, "HOME", str(base / "home")), mock.patch.object(module, "WINDOWS", True), mock.patch.dict(module.os.environ, env), mock.patch.object(module.subprocess, "run", side_effect=version), mock.patch.object(module, "hidden_window_flags", return_value=0):
                self.assertEqual(module.find_claude(), str(old if explicit else new))

    def test_choose_newer_cli_instead_of_stale_standalone(self):
        self.exercise(False)

    def test_preserve_explicit_claude_binary_override(self):
        self.exercise(True)


if __name__ == "__main__":
    unittest.main(verbosity=2)
