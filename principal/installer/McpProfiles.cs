using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SomaSetup {
    public static class McpProfiles {
        public static List<string> Apps(IEnumerable<string> components) {
            var selected = new HashSet<string>(components);
            var apps = new List<string>();
            if (selected.Contains("adobe")) apps.AddRange(new [] { "aftereffects", "photoshop", "illustrator", "premierepro" });
            if (selected.Contains("blender")) apps.Add("blender");
            if (selected.Contains("cinema4d")) apps.Add("cinema4d");
            if (selected.Contains("hub")) apps.Add("figma");
            return apps;
        }
        public static void Write(string stagedHub, string installedHub, IEnumerable<string> apps) {
            string directory = Path.Combine(stagedHub, "mcp-configs");
            Directory.CreateDirectory(directory);
            string python = Path.Combine(installedHub, "python", "python.exe");
            string script = Path.Combine(installedHub, "bridge", "soma_mcp.py");
            var serializer = new JavaScriptSerializer();
            var servers = new Dictionary<string,object>();
            var toml = new StringBuilder("# Soma: acrescente ao config.toml sem apagar configurações existentes.\n# As ações continuam sujeitas à aprovação do cliente.\n\n");
            foreach (string app in apps.Distinct()) {
                string name = "soma_" + app;
                string[] arguments = { script, "--app", app, "--ensure-hub" };
                servers[name] = new Dictionary<string,object> { { "command", python }, { "args", arguments } };
                toml.Append("[mcp_servers.").Append(name).Append("]\ncommand = ").Append(serializer.Serialize(python));
                toml.Append("\nargs = ").Append(serializer.Serialize(arguments));
                toml.Append("\nstartup_timeout_sec = 30\ntool_timeout_sec = 300\ndefault_tools_approval_mode = \"prompt\"\n\n");
            }
            string json = serializer.Serialize(new Dictionary<string,object> { { "mcpServers", servers } });
            File.WriteAllText(Path.Combine(directory, "Claude-Desktop.json"), json, Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "Claude-Code.json"), json, Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "Codex.toml"), toml.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "LEIA-ME.txt"),
                "Perfis locais MCP por aplicativo. Mescle as entradas ao arquivo do cliente, preservando os servidores existentes.\r\n" +
                "Claude Desktop: mcpServers de Claude-Desktop.json. Claude Code: --mcp-config Claude-Code.json. Codex/ChatGPT desktop local: importe os servidores ou acrescente o fragmento Codex.toml. Reinicie/recarregue o cliente após configurar.\r\n" +
                "Não copie estes perfis para o ChatGPT web: ele precisa de uma conexão remota ou Secure MCP Tunnel, configurada e autorizada separadamente. Nenhum túnel é aberto por este instalador.\r\n" +
                "O adaptador usa a credencial do Hub local, sem copiá-la para este arquivo. Abra o aplicativo e o painel Soma. As ferramentas de cada perfil só operam no aplicativo correspondente.\r\n" +
                "soma_status verifica presença; soma_get_tools lista ferramentas nativas; soma_call_tool executa uma delas. Peça primeiro uma inspeção sem alterações.\r\n", Encoding.UTF8);
        }
    }
}
