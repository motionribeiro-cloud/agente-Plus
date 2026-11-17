using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Soma Agente - Instalador experimental")]
[assembly: AssemblyProduct("Soma Agente")]
[assembly: AssemblyVersion("1.9.0.0")]
[assembly: AssemblyFileVersion("1.9.0.0")]

namespace SomaSetup {
    static class Program {
        [STAThread] static void Main() {
            bool created;
            using (var gate = new Mutex(true, @"Local\SomaAgenteInstaller", out created)) {
                if (!created) { MessageBox.Show("Já existe uma janela do instalador Soma aberta."); return; }
                try {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new SetupForm());
                } finally { gate.ReleaseMutex(); }
            }
        }
    }
    class SetupForm : Form {
        readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Soma Agente", "Installer");
        readonly Dictionary<string,CheckBox> choices = new Dictionary<string,CheckBox>();
        readonly Dictionary<string,TextBox> targets = new Dictionary<string,TextBox>();
        readonly TextBox output = new TextBox();
        readonly TextBox publicKnowledge = new TextBox(), privateKnowledge = new TextBox();
        readonly CheckBox enableCep = new CheckBox();
        readonly Button install = new Button(), diagnose = new Button(), uninstall = new Button();
        string ReceiptPath { get { return Path.Combine(root, "installation.json"); } }
        public SetupForm() {
            Text = "Soma Agente | Instalador 1.9.0 preview";
            ClientSize = new Size(890, 760);
            MinimumSize = new Size(800, 700);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 9, AutoScroll = true };
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "SOMA AGENTE", Font = new Font("Segoe UI", 23, FontStyle.Bold), AutoSize = true });
            layout.Controls.Add(new Label { Text = "Instalador experimental 1.9.0 · MCP por aplicativo · bibliotecas conectadas · instalador com backup", AutoSize = true });
            layout.Controls.Add(new Label { Text = "Escolha os componentes e confira os destinos. Feche os aplicativos selecionados antes de instalar.\nAtualizações mantêm backups. Contas, conversas e referências não são apagadas.", AutoSize = true, Padding = new Padding(0, 10, 0, 10) });
            var grid = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Top };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            layout.Controls.Add(grid);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Row(grid, "adobe", "Adobe (AE, PS, AI, Premiere)", Path.Combine(roaming, "Adobe", "CEP", "extensions", "com.framepilot.aeagent"));
            Row(grid, "blender", "Blender", Detect(Path.Combine(roaming, "Blender Foundation", "Blender"), "scripts", "addons", "soma_agente"));
            Row(grid, "cinema4d", "Cinema 4D", Detect(Path.Combine(roaming, "Maxon"), "plugins", "soma_agente"));
            Row(grid, "hub", "Soma Hub + MCPs + Figma", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Soma Agente", "Hub"));
            var libraries = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 10, 0, 10) };
            libraries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            libraries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            libraries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            KnowledgeRow(libraries, "Complemento público (opcional)", publicKnowledge);
            KnowledgeRow(libraries, "Brain / pasta privada (opcional)", privateKnowledge);
            layout.Controls.Add(libraries);
            enableCep.Text = "Adobe: permitir painéis de desenvolvimento no CEP 12 (altera configuração deste usuário)";
            enableCep.AutoSize = true;
            layout.Controls.Add(enableCep);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
            install.Text = "Instalar / atualizar"; install.AutoSize = true; install.Click += InstallClick;
            diagnose.Text = "Diagnosticar"; diagnose.AutoSize = true; diagnose.Click += DiagnoseClick;
            uninstall.Text = "Desinstalar"; uninstall.AutoSize = true; uninstall.Click += UninstallClick;
            var profiles = new Button { Text = "Perfis MCP", AutoSize = true };
            profiles.Click += delegate {
                try {
                    if (!File.Exists(ReceiptPath)) throw new IOException("Instale os componentes antes de abrir os perfis MCP.");
                    var entry = Engine.Read(ReceiptPath).Entries.FirstOrDefault(e => e.Component == "hub");
                    if (entry == null) throw new IOException("O Soma Hub ainda não foi instalado por esta versão.");
                    string directory = Path.Combine(entry.Target, "mcp-configs");
                    if (!Directory.Exists(directory)) throw new IOException("Perfis MCP não encontrados. Atualize o Soma Hub.");
                    Process.Start("explorer.exe", directory);
                } catch (Exception error) { Log(error.Message); }
            };
            actions.Controls.AddRange(new Control[] { install, diagnose, uninstall, profiles });
            layout.Controls.Add(actions);
            output.Multiline = true; output.ReadOnly = true; output.ScrollBars = ScrollBars.Vertical;
            output.Dock = DockStyle.Fill; output.MinimumSize = new Size(600, 180);
            layout.Controls.Add(output);
            layout.RowStyles.Clear();
            for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { AutoSize = true, Text = "O instalador não inicia agentes, não altera login/MCP do Antigravity e não instala inicialização automática." });
            Log("Pronto para conferir destinos. Nenhum componente está selecionado por padrão.");
        }
        void KnowledgeRow(TableLayoutPanel grid, string label, TextBox field) {
            field.Dock = DockStyle.Fill;
            var browse = new Button { Text = "Escolher", AutoSize = true };
            browse.Click += delegate {
                using (var dialog = new FolderBrowserDialog { Description = "Escolha a pasta das instruções. A pasta será consultada, não copiada ou publicada." })
                    if (dialog.ShowDialog() == DialogResult.OK) field.Text = dialog.SelectedPath;
            };
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
            grid.Controls.Add(field); grid.Controls.Add(browse);
        }
        static string Detect(string parent, params string[] suffix) {
            if (!Directory.Exists(parent)) return "";
            var versions = Directory.GetDirectories(parent).OrderByDescending(p => p).ToArray();
            if (versions.Length != 1) return ""; // Do not silently choose a version when several are present.
            return suffix.Aggregate(versions[0], Path.Combine);
        }
        void Row(TableLayoutPanel grid, string name, string label, string target) {
            var check = new CheckBox { Text = label, AutoSize = true, Anchor = AnchorStyles.Left };
            var field = new TextBox { Text = target, Dock = DockStyle.Fill };
            var browse = new Button { Text = "Escolher", AutoSize = true };
            browse.Click += delegate {
                string leaf = name == "adobe" ? "com.framepilot.aeagent" : name == "hub" ? "Hub" : "soma_agente";
                using (var dialog = new FolderBrowserDialog { Description = "Escolha a pasta de extensões/plugins. O instalador acrescenta " + leaf + " ao caminho.", SelectedPath = Directory.Exists(field.Text) ? field.Text : "" }) {
                    if (dialog.ShowDialog() == DialogResult.OK) {
                        field.Text = new DirectoryInfo(dialog.SelectedPath).Name.Equals(leaf, StringComparison.OrdinalIgnoreCase)
                            ? dialog.SelectedPath : Path.Combine(dialog.SelectedPath, leaf);
                    }
                }
            };
            choices.Add(name, check); targets.Add(name, field);
            grid.Controls.Add(check); grid.Controls.Add(field); grid.Controls.Add(browse);
        }
        void Log(string text) {
            if (InvokeRequired) { Invoke(new Action<string>(Log), text); return; }
            output.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
        }
        void SaveLog() {
            try {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "install-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log"), output.Text);
            } catch (Exception e) { Log("Não foi possível salvar o relatório: " + e.Message); }
        }
        void Busy(bool busy) {
            install.Enabled = diagnose.Enabled = uninstall.Enabled = !busy;
            foreach (var field in targets.Values) field.Enabled = !busy;
            foreach (var choice in choices.Values) choice.Enabled = !busy;
            enableCep.Enabled = !busy; publicKnowledge.Enabled = privateKnowledge.Enabled = !busy; ControlBox = !busy;
            UseWaitCursor = busy;
        }
        static void Unpack(string name, string target) {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name + ".zip")) {
                if (stream == null) throw new IOException("Recurso ausente: " + name);
                string expected = PayloadHashes.Values[name];
                string actual;
                using (var sha = SHA256.Create()) actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != expected) throw new IOException("Integridade inválida: " + name);
                stream.Position = 0;
                Engine.Extract(stream, target);
            }
        }
        List<Package> Selected() {
            if (choices.Any(p => p.Value.Checked)) choices["hub"].Checked = true;
            var packages = new List<Package>();
            foreach (var pair in choices.Where(p => p.Value.Checked)) {
                string name = pair.Key, target = targets[name].Text.Trim();
                if (String.IsNullOrEmpty(target)) throw new IOException("Escolha o destino de " + pair.Value.Text + ".");
                string leaf = new DirectoryInfo(target).Name;
                string expected = name == "adobe" ? "com.framepilot.aeagent" : name == "hub" ? "Hub" : "soma_agente";
                if (!leaf.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("O destino de " + pair.Value.Text + " deve terminar em " + expected + ", para preservar outras extensões.");
                packages.Add(new Package { Name = name, Target = target, Stage = delegate(string stage) {
                    Unpack(name, stage);
                    if (name == "hub") {
                        Unpack("python", Path.Combine(stage, "python"));
                        McpProfiles.Write(stage, target, McpProfiles.Apps(packages.Select(p => p.Name)));
                        File.WriteAllText(Path.Combine(stage, "Iniciar-Soma-Hub.cmd"), "@echo off\r\ncd /d \"%~dp0\"\r\n\"%~dp0python\\python.exe\" \"%~dp0bridge\\soma_hub.py\"\r\nif errorlevel 1 pause\r\n");
                    }
                }});
            }
            return packages;
        }
        static void RequireClosed(IEnumerable<Package> packages) {
            var names = new List<string>();
            if (packages.Any(p => p.Name == "adobe")) names.AddRange(new [] { "AfterFX", "Photoshop", "Illustrator", "Adobe Premiere Pro" });
            if (packages.Any(p => p.Name == "blender")) names.Add("blender");
            if (packages.Any(p => p.Name == "cinema4d")) names.Add("Cinema 4D");
            foreach (string name in names) {
                var processes = Process.GetProcessesByName(name);
                try { if (processes.Length != 0) throw new IOException("Feche " + name + " e tente novamente. Nenhum aplicativo foi encerrado pelo instalador."); }
                finally { foreach (var process in processes) process.Dispose(); }
            }
        }
        async void InstallClick(object sender, EventArgs args) {
            bool filesInstalled = false;
            try {
                var packages = Selected();
                if (packages.Count == 0) throw new IOException("Selecione pelo menos um componente.");
                RequireClosed(packages);
                string summary = String.Join(Environment.NewLine, packages.Select(p => p.Name + ": " + p.Target));
                string publicFolder = publicKnowledge.Text.Trim(), privateFolder = privateKnowledge.Text.Trim();
                foreach (string folder in new [] { publicFolder, privateFolder })
                    if (!String.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder)) throw new IOException("Pasta de conhecimento não encontrada: " + folder);
                if (!String.IsNullOrWhiteSpace(publicFolder)) summary += "\nComplemento: " + publicFolder;
                if (!String.IsNullOrWhiteSpace(privateFolder)) summary += "\nBiblioteca privada: " + privateFolder;
                bool cep = enableCep.Checked && packages.Any(p => p.Name == "adobe");
                if (cep) summary += "\n\nTambém habilitará PlayerDebugMode no CEP 12 para este usuário.";
                if (MessageBox.Show(summary + "\n\nInstalar com backup?", "Confira a instalação", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
                Busy(true);
                await Task.Run(() => Engine.Install(packages, ReceiptPath, Log));
                filesInstalled = true;
                var hubPackage = packages.First(p => p.Name == "hub");
                KnowledgeSetup.Locate(hubPackage.Target);
                await Task.Run(() => {
                    KnowledgeSetup.Connect(hubPackage.Target, publicFolder, false, Log);
                    KnowledgeSetup.Connect(hubPackage.Target, privateFolder, true, Log);
                });
                if (cep) {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Adobe\CSXS.12")) {
                        string record = Path.Combine(root, "cep-debug-before-install.txt");
                        if (!File.Exists(record)) File.WriteAllText(record, Convert.ToString(key.GetValue("PlayerDebugMode", "ABSENT")));
                        key.SetValue("PlayerDebugMode", "1", RegistryValueKind.String);
                    }
                    Log("CEP 12 habilitado com consentimento. Valor anterior registrado.");
                }
                Log("Arquivos instalados. Isso não confirma funcionamento dentro dos aplicativos.");
                if (packages.Any(p => p.Name == "hub")) Log("Hub: abra Iniciar-Soma-Hub.cmd ou conecte um perfil MCP. Use Perfis MCP para Claude Desktop/Code e Codex. Figma: importe plugin/manifest.json.");
                if (packages.Any(p => p.Name == "adobe") && !cep) Log("Adobe: se o CEP 12 não estava habilitado, o painel ainda não aparecerá. A opção acima exige consentimento explícito.");
                Log("Blender/Cinema 4D: confira carregamento no aplicativo; ativação automática não foi adicionada.");
            } catch (Exception e) {
                Log((filesInstalled ? "Arquivos instalados, mas a configuração posterior falhou: " : "Instalação não concluída: ") + e.Message);
                MessageBox.Show(e.Message, "Soma Agente", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { Busy(false); SaveLog(); }
        }
        async void DiagnoseClick(object sender, EventArgs args) {
            try {
                Busy(true);
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Adobe\CSXS.12"))
                    Log("CEP 12: " + ((key != null && Convert.ToString(key.GetValue("PlayerDebugMode")) == "1") ? "painéis de desenvolvimento habilitados" : "painéis de desenvolvimento não habilitados"));
                Log("Contas e conexão MCP precisam ser verificadas no painel do Soma; o diagnóstico não solicita nem registra credenciais.");
                if (!File.Exists(ReceiptPath)) { Log("Nenhuma instalação gerenciada por esta versão foi encontrada. Uma instalação antiga pode existir."); return; }
                var receipt = Engine.Read(ReceiptPath);
                foreach (var entry in receipt.Entries) Log(entry.Component + ": " + entry.Target);
                var issues = await Task.Run(() => Engine.Diagnose(receipt));
                Log("Manifesto: " + receipt.Entries.Count + " componente(s).");
                foreach (string issue in issues) Log(issue);
                if (issues.Count == 0) Log("Integridade dos arquivos conferida. Login, conexão MCP e operações nos aplicativos ainda precisam de teste.");
            } catch (Exception e) { Log("Diagnóstico não concluído: " + e.Message); }
            finally { Busy(false); SaveLog(); }
        }
        async void UninstallClick(object sender, EventArgs args) {
            try {
                if (!File.Exists(ReceiptPath)) { Log("Não há manifesto desta versão. Nenhuma pasta foi removida."); return; }
                var receipt = Engine.Read(ReceiptPath);
                RequireClosed(receipt.Entries.Select(e => new Package { Name = e.Component }));
                if (MessageBox.Show("Remover os componentes registrados dos destinos?\n\nOs arquivos serão preservados em pastas .removed; conversas e referências permanecem.\nA configuração CEP e instalações antigas sem manifesto não serão alteradas.", "Desinstalar Soma", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                Busy(true);
                await Task.Run(() => Engine.Uninstall(ReceiptPath, Log));
                Log("Componentes desinstalados dos destinos. Backups e dados pessoais preservados.");
            } catch (Exception e) { Log("Desinstalação não concluída: " + e.Message); }
            finally { Busy(false); SaveLog(); }
        }
    }
}
