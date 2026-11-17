using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using SomaSetup;

class EngineTests {
    static int passed;
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static Package Package(string target, string text) {
        return new Package { Name = "test", Target = target, Stage = path => File.WriteAllText(Path.Combine(path, "main.txt"), text) };
    }
    static void Case(string name, Action<string> test) {
        string root = Path.Combine(Path.GetTempPath(), "soma-engine-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); Console.WriteLine("PASS " + name); passed++; }
        finally {
            // Only the unique, resolved temporary test directory can be deleted.
            if (Engine.Inside(root, Path.GetTempPath()) && Path.GetFileName(root).StartsWith("soma-engine-test-")) Directory.Delete(root, true);
        }
    }
    static void Fails(Action action) { bool failed = false; try { action(); } catch { failed = true; } Assert(failed, "Expected failure"); }
    static void Main(string[] args) {
        Case("install-update-diagnose-uninstall-preserves-user-data", root => {
            string target = Path.Combine(root, "component"), receipt = Path.Combine(root, "state", "install.json");
            var first = Engine.Install(new List<Package> { Package(target, "one") }, receipt, Console.WriteLine);
            Assert(Engine.Diagnose(first).Count == 0, "Fresh installation mismatch");
            File.WriteAllText(Path.Combine(target, "my-reference.txt"), "user data");
            var next = Engine.Install(new List<Package> { Package(target, "two") }, receipt, Console.WriteLine);
            Assert(File.ReadAllText(Path.Combine(next.Entries[0].Backup, "my-reference.txt")) == "user data", "Update lost user file");
            File.WriteAllText(Path.Combine(target, "main.txt"), "modified");
            Assert(Engine.Diagnose(next).Count == 1, "Modified file not diagnosed");
            File.Delete(Path.Combine(target, "main.txt"));
            Assert(Engine.Diagnose(next).Count == 1, "Missing file not diagnosed");
            File.WriteAllText(Path.Combine(target, "custom.txt"), "keep");
            Engine.Uninstall(receipt, Console.WriteLine);
            Assert(!Directory.Exists(target), "Uninstall left active directory");
            Assert(Directory.GetFiles(root, "custom.txt", SearchOption.AllDirectories).Length == 1, "Uninstall lost custom data");
            Assert(!File.Exists(receipt), "Active receipt left behind");
        });
        Case("all-components-rollback-when-later-target-is-locked", root => {
            string target = Path.Combine(root, "first"), blocked = Path.Combine(root, "blocked"), receipt = Path.Combine(root, "install.json");
            Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "main.txt"), "original");
            File.WriteAllText(blocked, "cannot become directory");
            Fails(() => Engine.Install(new List<Package> { Package(target, "new"), Package(blocked, "new") }, receipt, Console.WriteLine));
            Assert(File.ReadAllText(Path.Combine(target, "main.txt")) == "original", "Original not restored");
            Assert(File.Exists(blocked), "Unrelated blocking file changed");
            Assert(!File.Exists(receipt), "Failure wrote success receipt");
        });
        Case("receipt-write-failure-rolls-back-files", root => {
            string target = Path.Combine(root, "first"), receipt = Path.Combine(root, "receipt-is-directory");
            Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "main.txt"), "original");
            Directory.CreateDirectory(receipt);
            Fails(() => Engine.Install(new List<Package> { Package(target, "new") }, receipt, Console.WriteLine));
            Assert(File.ReadAllText(Path.Combine(target, "main.txt")) == "original", "Receipt failure did not restore old installation");
        });
        Case("staging-failure-keeps-installed-files", root => {
            string target = Path.Combine(root, "first"); Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "main.txt"), "original");
            Fails(() => Engine.Install(new List<Package> { new Package { Name = "bad", Target = target, Stage = _ => { throw new IOException("bad package"); } } }, Path.Combine(root, "install.json"), Console.WriteLine));
            Assert(File.ReadAllText(Path.Combine(target, "main.txt")) == "original", "Staging changed existing files");
            Assert(Directory.GetDirectories(root, "*.stage-*").Length == 0, "Staging not cleaned");
        });
        Case("reject-overlapping-targets", root => {
            Fails(() => Engine.Install(new List<Package> { Package(Path.Combine(root, "component"), "one"), Package(Path.Combine(root, "component", "child"), "two") }, Path.Combine(root, "install.json"), Console.WriteLine));
            Assert(!Directory.Exists(Path.Combine(root, "component")), "Overlapping targets created");
        });
        Case("reject-zip-path-traversal", root => {
            using (var memory = new MemoryStream()) {
                using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true)) {
                    using (var writer = new StreamWriter(archive.CreateEntry("../escaped.txt").Open())) writer.Write("escape");
                }
                memory.Position = 0;
                Fails(() => Engine.Extract(memory, Path.Combine(root, "stage")));
                Assert(!File.Exists(Path.Combine(root, "escaped.txt")), "Archive escaped destination");
            }
        });
        Case("reject-relative-installation-target", root => {
            Fails(() => Engine.Install(new List<Package> { Package("relative-target", "bad") }, Path.Combine(root, "install.json"), Console.WriteLine));
        });
        Case("never-downgrade-a-newer-adobe-panel", root => {
            string target = Path.Combine(root, "component");
            Directory.CreateDirectory(Path.Combine(target, "CSXS"));
            string manifest = Path.Combine(target, "CSXS", "manifest.xml");
            File.WriteAllText(manifest, "<ExtensionManifest ExtensionBundleVersion=\"1.7.11\" />");
            var package = new Package { Name = "adobe", Target = target, Stage = stage => {
                Directory.CreateDirectory(Path.Combine(stage, "CSXS"));
                File.WriteAllText(Path.Combine(stage, "CSXS", "manifest.xml"), "<ExtensionManifest ExtensionBundleVersion=\"1.4.1\" />");
            }};
            Fails(() => Engine.Install(new List<Package> { package }, Path.Combine(root, "install.json"), Console.WriteLine));
            Assert(File.ReadAllText(manifest).Contains("1.7.11"), "Newer panel overwritten");
        });
        Case("mcp-profiles-use-final-path-and-only-selected-apps", root => {
            string stage = Path.Combine(root, "stage"), final = Path.Combine(root, "Hub final");
            McpProfiles.Write(stage, final, McpProfiles.Apps(new [] { "adobe", "hub" }));
            string json = File.ReadAllText(Path.Combine(stage, "mcp-configs", "Claude-Desktop.json"));
            Assert(json.Contains("soma_aftereffects") && json.Contains("soma_figma"), "Selected app profile missing");
            Assert(!json.Contains("soma_blender"), "Unselected application exposed");
            Assert(!json.Contains("Bearer") && !json.Contains("hub-token"), "Client config contains a credential");
            Assert(!json.Contains("stage"), "Client config references temporary installation path");
            Assert(File.ReadAllText(Path.Combine(stage, "mcp-configs", "Codex.toml")).Contains("approval_mode = \"prompt\""), "Client approvals bypassed");
        });
        Case("uninstall-failure-restores-earlier-components", root => {
            string target = Path.Combine(root, "component"), receipt = Path.Combine(root, "install.json");
            var data = Engine.Install(new List<Package> { Package(target, "original") }, receipt, Console.WriteLine);
            data.Entries.Add(new Entry { Target = Path.GetPathRoot(root), Component = "invalid" });
            Engine.Write(receipt, data);
            Fails(() => Engine.Uninstall(receipt, Console.WriteLine));
            Assert(File.ReadAllText(Path.Combine(target, "main.txt")) == "original", "Uninstall failure lost earlier component");
            Assert(File.Exists(receipt), "Failed uninstall removed receipt");
        });
        if (args.Length == 1) Case("real-five-payload-install-diagnose-uninstall-in-sandbox", root => {
            var packages = new List<Package>();
            foreach (string file in Directory.GetFiles(args[0], "*.zip")) {
                string archivePath = file;
                packages.Add(new Package { Name = Path.GetFileNameWithoutExtension(file), Target = Path.Combine(root, Path.GetFileNameWithoutExtension(file)), Stage = stage => {
                    using (var source = File.OpenRead(archivePath)) Engine.Extract(source, stage);
                }});
            }
            Assert(packages.Count == 5, "Expected all five original payloads");
            string receipt = Path.Combine(root, "state", "install.json");
            var data = Engine.Install(packages, receipt, Console.WriteLine);
            Assert(Engine.Diagnose(data).Count == 0, "Real extracted payload has missing or changed files");
            Assert(File.Exists(Path.Combine(root, "python", "LICENSE.txt")), "Python license not preserved");
            Assert(File.Exists(Path.Combine(root, "hub", "bridge", "soma_hub.py")), "Hub source missing");
            Engine.Uninstall(receipt, Console.WriteLine);
            Assert(!File.Exists(receipt), "Real payload uninstall left receipt");
        });
        if (args.Length == 1) Case("installer-connects-knowledge-through-real-embedded-python", root => {
            string hub = Path.Combine(root, "Hub"), folder = Path.Combine(root, "Referências privadas"), config = Path.Combine(root, "state", "knowledge.json");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "saas.md"), "# SaaS\nInterface de produto.");
            using (var stream = File.OpenRead(Path.Combine(args[0], "hub.zip"))) Engine.Extract(stream, hub);
            using (var stream = File.OpenRead(Path.Combine(args[0], "python.zip"))) Engine.Extract(stream, Path.Combine(hub, "python"));
            string previous = Environment.GetEnvironmentVariable("SOMA_KNOWLEDGE_CONFIG");
            try {
                Environment.SetEnvironmentVariable("SOMA_KNOWLEDGE_CONFIG", config);
                KnowledgeSetup.Connect(hub, folder, true, Console.WriteLine);
                Assert(File.Exists(config), "Installer did not persist the library connection");
                Assert(File.ReadAllText(config).Contains("private"), "Private flag not recorded");
                Assert(File.ReadAllText(Path.Combine(folder, "saas.md")).Contains("Interface"), "Source document altered");
                var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(hub, "python", "python.exe")) {
                    Arguments = KnowledgeSetup.Quote(Path.Combine(hub, "bridge", "soma_mcp.py")) + " --app aftereffects",
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                };
                using (var child = System.Diagnostics.Process.Start(start)) {
                    var stdout = child.StandardOutput.ReadToEndAsync();
                    var stderr = child.StandardError.ReadToEndAsync();
                    child.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}");
                    child.StandardInput.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"soma_search_knowledge\",\"arguments\":{\"query\":\"SaaS\"}}}");
                    child.StandardInput.Close();
                    if (!child.WaitForExit(15000)) { child.Kill(); throw new Exception("Embedded MCP adapter timed out"); }
                    Assert(child.ExitCode == 0, "Embedded MCP adapter failed: " + stderr.Result);
                    Assert(stdout.Result.Contains("soma_read_knowledge") && stdout.Result.Contains("saas.md"), "Embedded MCP adapter did not expose/read knowledge");
                }
            } finally { Environment.SetEnvironmentVariable("SOMA_KNOWLEDGE_CONFIG", previous); }
        });
        Console.WriteLine("PASSED " + passed + " scenarios");
    }
}
