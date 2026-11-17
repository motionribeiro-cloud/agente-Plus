using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SomaSetup {
    public static class KnowledgeSetup {
        public static void Connect(string hub, string folder, bool isPrivate, Action<string> log) {
            if (String.IsNullOrWhiteSpace(folder)) return;
            var serializer = new JavaScriptSerializer();
            string request = serializer.Serialize(new Dictionary<string,object> {
                { "action", "configure" }, { "folder", folder }, { "private", isPrivate }
            });
            // Write the request to a temporary stdin-free argument through standard Windows quoting.
            var start = new ProcessStartInfo(Path.Combine(hub, "python", "python.exe")) {
                Arguments = Quote(Path.Combine(hub, "bridge", "soma_knowledge.py")) + " --request " + Quote(request),
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(start)) {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000)) { process.Kill(); throw new IOException("A conexão da biblioteca excedeu o tempo limite."); }
                string output = stdoutTask.Result;
                var result = serializer.Deserialize<Dictionary<string,object>>(output);
                if (process.ExitCode != 0 || !result.ContainsKey("ok") || !(bool)result["ok"])
                    throw new IOException("Biblioteca não conectada. Os arquivos instalados foram preservados; selecione a pasta em Conhecimento no painel.");
            }
            log((isPrivate ? "Biblioteca privada conectada: " : "Complemento conectado: ") + folder);
        }
        public static void Locate(string hub) {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AE Agent");
            Directory.CreateDirectory(data);
            string file = Path.Combine(data, "hub-location.json"), stage = file + ".stage-" + Guid.NewGuid().ToString("N");
            try {
                File.WriteAllText(stage, new JavaScriptSerializer().Serialize(new Dictionary<string,string> { { "path", hub } }), Encoding.UTF8);
                if (File.Exists(file)) File.Replace(stage, file, null); else File.Move(stage, file);
            } finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        public static string Quote(string argument) {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in argument) {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') result.Append('\\', slashes * 2 + 1).Append('"');
                else result.Append('\\', slashes).Append(character);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
