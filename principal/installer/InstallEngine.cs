using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SomaSetup {
    [DataContract] public class Entry {
        [DataMember] public string Target;
        [DataMember] public string Backup;
        [DataMember] public string Component;
        [DataMember] public Dictionary<string,string> Files = new Dictionary<string,string>();
    }
    [DataContract] public class Receipt {
        [DataMember] public string Version = "1.9.0-preview";
        [DataMember] public List<Entry> Entries = new List<Entry>();
    }
    public class Package {
        public string Name, Target;
        public Action<string> Stage;
    }
    public static class Engine {
        public static string Full(string path) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
        public static bool Inside(string path, string root) {
            return Full(path).StartsWith(Full(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        public static void CheckTarget(string path) {
            string full = Full(path);
            if (full == Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar) || !Path.IsPathRooted(path))
                throw new IOException("Destino inválido: " + path);
            for (string p = full; !String.IsNullOrEmpty(p); p = Path.GetDirectoryName(p)) {
                if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("O destino contém um link de diretório: " + p);
            }
        }
        public static void Extract(Stream source, string destination) {
            Directory.CreateDirectory(destination);
            using (var archive = new ZipArchive(source, ZipArchiveMode.Read, true)) {
                foreach (var item in archive.Entries) {
                    string relative = item.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string target = Path.GetFullPath(Path.Combine(destination, relative));
                    if (Path.IsPathRooted(relative) || !Inside(target, destination) || relative.IndexOf(':') >= 0)
                        throw new IOException("Caminho inválido dentro do pacote: " + item.FullName);
                    if (item.FullName.EndsWith("/")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var input = item.Open()) using (var output = new FileStream(target, FileMode.CreateNew)) input.CopyTo(output);
                }
            }
        }
        public static string Hash(string file) {
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        public static Dictionary<string,string> Inventory(string directory) {
            var result = new Dictionary<string,string>();
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)) {
                CheckTarget(path);
                result[path.Substring(Full(directory).Length + 1)] = Hash(path);
            }
            return result;
        }
        static Version ComponentVersion(string directory) {
            string manifest = Path.Combine(directory, "CSXS", "manifest.xml");
            string core = Path.Combine(directory, "soma_core", "agent.py");
            if (!File.Exists(core)) core = Path.Combine(directory, "bridge", "soma_core", "agent.py");
            string file = File.Exists(manifest) ? manifest : core;
            if (!File.Exists(file)) return null;
            string pattern = File.Exists(manifest) ? "ExtensionBundleVersion=\"([0-9.]+)" : "VERSION\\s*=\\s*\"([0-9.]+)";
            var match = Regex.Match(File.ReadAllText(file), pattern);
            Version version;
            return match.Success && Version.TryParse(match.Groups[1].Value.TrimEnd('.'), out version) ? version : null;
        }
        public static Receipt Read(string file) {
            using (var stream = File.OpenRead(file)) return (Receipt)new DataContractJsonSerializer(typeof(Receipt)).ReadObject(stream);
        }
        public static void Write(string file, Receipt receipt) {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string stage = file + ".tmp-" + Guid.NewGuid().ToString("N");
            try {
                using (var stream = File.Create(stage)) new DataContractJsonSerializer(typeof(Receipt)).WriteObject(stream, receipt);
                if (File.Exists(file)) File.Replace(stage, file, null); else File.Move(stage, file);
            } finally { if (File.Exists(stage)) File.Delete(stage); }
        }
        // File operations remain in the selected target's parent, allowing rename-based rollback.
        public static Receipt Install(List<Package> packages, string receiptPath, Action<string> log) {
            if (packages.Count == 0) throw new IOException("Selecione pelo menos um componente.");
            var receipt = File.Exists(receiptPath) ? Read(receiptPath) : new Receipt();
            var staged = new Dictionary<Package,string>();
            var changed = new List<Entry>();
            try {
                foreach (var package in packages) {
                    CheckTarget(package.Target);
                    foreach (var other in packages.Where(p => p != package)) {
                        if (Full(other.Target).Equals(Full(package.Target), StringComparison.OrdinalIgnoreCase) || Inside(other.Target, package.Target) || Inside(package.Target, other.Target))
                            throw new IOException("Destinos de componentes não podem se sobrepor.");
                    }
                    string stage = Full(package.Target) + ".stage-" + Guid.NewGuid().ToString("N");
                    staged.Add(package, stage);
                    Directory.CreateDirectory(stage);
                    package.Stage(stage);
                    if (!Directory.EnumerateFileSystemEntries(stage).Any()) throw new IOException("Componente vazio: " + package.Name);
                    var installedVersion = ComponentVersion(package.Target);
                    var incomingVersion = ComponentVersion(stage);
                    if (installedVersion != null && incomingVersion != null && installedVersion > incomingVersion)
                        throw new IOException("Atualização recusada: " + package.Name + " instalado é " + installedVersion + ", mas o pacote é " + incomingVersion + ". A versão atual foi preservada.");
                    log("Preparado: " + package.Name);
                }
                foreach (var package in packages) {
                    string target = Full(package.Target);
                    var entry = new Entry { Target = target, Component = package.Name, Files = Inventory(staged[package]) };
                    if (Directory.Exists(target)) {
                        entry.Backup = target + ".backup-" + Guid.NewGuid().ToString("N");
                        Directory.Move(target, entry.Backup);
                    }
                    changed.Add(entry);
                    Directory.Move(staged[package], target);
                    log("Copiado: " + package.Name);
                    if (entry.Backup != null) log("Backup preservado: " + entry.Backup);
                }
                foreach (var entry in changed) {
                    // The previous version's backup stays on disk; record the latest replacement.
                    var prior = receipt.Entries.FirstOrDefault(e => e.Target.Equals(entry.Target, StringComparison.OrdinalIgnoreCase));
                    if (prior != null) receipt.Entries.Remove(prior);
                    receipt.Entries.Add(entry);
                }
                Write(receiptPath, receipt);
                return receipt;
            } catch {
                foreach (var entry in changed.AsEnumerable().Reverse()) {
                    try {
                        if (Directory.Exists(entry.Target)) Directory.Move(entry.Target, entry.Target + ".failed-" + Guid.NewGuid().ToString("N"));
                        if (entry.Backup != null && Directory.Exists(entry.Backup)) Directory.Move(entry.Backup, entry.Target);
                    } catch (Exception error) { log("Reversão requer atenção: " + entry.Target + " | " + error.Message); }
                }
                throw;
            } finally {
                foreach (var stage in staged.Values) {
                    // Only delete generated staging directories; never a supplied installation target.
                    try { if (stage.Contains(".stage-") && Directory.Exists(stage)) Directory.Delete(stage, true); }
                    catch (IOException) { log("Pasta temporária preservada: " + stage); }
                }
            }
        }
        public static List<string> Diagnose(Receipt receipt) {
            var issues = new List<string>();
            foreach (var entry in receipt.Entries) foreach (var file in entry.Files) {
                string path = Path.GetFullPath(Path.Combine(entry.Target, file.Key));
                if (!Inside(path, entry.Target)) { issues.Add("Manifesto inválido: " + entry.Component); continue; }
                if (!File.Exists(path)) issues.Add("Ausente: " + entry.Component + "/" + file.Key);
                else if (Hash(path) != file.Value) issues.Add("Alterado: " + entry.Component + "/" + file.Key);
            }
            return issues;
        }
        public static void Uninstall(string receiptPath, Action<string> log) {
            var receipt = Read(receiptPath);
            var moved = new List<KeyValuePair<string,string>>();
            try {
                foreach (var entry in receipt.Entries) {
                    CheckTarget(entry.Target);
                    if (!Directory.Exists(entry.Target)) continue;
                    // Never recursively delete an installation. Keep all files, including user additions.
                    string retained = entry.Target + ".removed-" + Guid.NewGuid().ToString("N");
                    Directory.Move(entry.Target, retained);
                    moved.Add(new KeyValuePair<string,string>(entry.Target, retained));
                    log("Componente removido do destino e preservado em: " + retained);
                }
                File.Move(receiptPath, receiptPath + ".removed-" + Guid.NewGuid().ToString("N"));
            } catch {
                foreach (var pair in moved.AsEnumerable().Reverse()) Directory.Move(pair.Value, pair.Key);
                throw;
            }
        }
    }
}
