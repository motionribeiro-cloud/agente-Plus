"""Build payload ZIPs from editable component sources, retaining the original runtime."""
import hashlib
import json
import shutil
from pathlib import Path
import zipfile

ROOT = Path(__file__).parent


def main():
    shared = ROOT / "components/hub/bridge/soma_knowledge.py"
    for host in ["hub/bridge/soma_core", "blender/soma_core", "cinema4d/soma_core"]:
        shutil.copy2(shared, ROOT / "components" / host / "knowledge.py")
    output = ROOT / "payloads"
    output.mkdir(exist_ok=True)
    entries = []
    for name in ["adobe", "blender", "cinema4d", "hub"]:
        source = ROOT / "components" / name
        files = [p for p in source.rglob("*") if p.is_file() and "__pycache__" not in p.parts and p.suffix not in {".pyc", ".log"}]
        with zipfile.ZipFile(output / (name + ".zip"), "w", zipfile.ZIP_DEFLATED) as archive:
            for file in sorted(files):
                archive.write(file, file.relative_to(source))
        entries.append({"name": name, "files": len(files), "sha256": hashlib.sha256((output / (name + ".zip")).read_bytes()).hexdigest()})
    runtime = output / "python.zip"
    expected_runtime = ""
    previous = ROOT.parent / "soma-installer-1.4.2-preview/payload-manifest.json"
    if previous.exists():
        expected_runtime = next(e["sha256"] for e in json.loads(previous.read_text())["payloads"] if e["name"] == "python")
    reference = ROOT / "runtime-sha256.txt"
    if reference.exists():
        expected_runtime = reference.read_text().strip()
    actual = hashlib.sha256(runtime.read_bytes()).hexdigest()
    if not expected_runtime or expected_runtime != actual:
        raise ValueError("Python runtime integrity mismatch")
    reference.write_text(actual + "\n", encoding="utf-8")
    with zipfile.ZipFile(runtime) as archive:
        entries.append({"name": "python", "files": len(archive.infolist()), "sha256": actual})
    manifest = {
        "installerVersion": "1.9.0-preview", "payloadVersion": "mixed-see-readme",
        "sources": {"adobe": "installed-1.7.11-with-local-health-fix", "hub": "installed-1.7.11-with-scoped-mcp-and-health-fix", "blender": "original-installer-1.4.1-with-health-fix", "cinema4d": "original-installer-1.4.1-with-health-fix", "python": "unchanged-original-installer-runtime"},
        "payloads": entries,
    }
    (ROOT / "payload-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print("Payloads reconstruídos a partir dos fontes; integridade do runtime Python conferida.")


if __name__ == "__main__":
    main()
