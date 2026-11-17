import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).parent
ENGINE = ROOT / "components/hub/bridge/soma_knowledge.py"
spec = importlib.util.spec_from_file_location("knowledge_test", ENGINE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class KnowledgeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="soma-library-test-")
        self.root = Path(self.temporary.name)
        self.library = module.Library(self.root / "data/knowledge.json")
        self.pack = self.root / "pack"
        self.pack.mkdir()
        (self.pack / "saas.md").write_text("# SaaS\nInterface e dashboard com estados consistentes.", encoding="utf-8")
        (self.pack / "explicativo.md").write_text("# Explicativo\nEnsinar com diagrama e exemplo.", encoding="utf-8")
        self.source = self.library.configure(self.pack)["source"]["id"]

    def tearDown(self):
        self.temporary.cleanup()

    def test_search_and_read_complementary_code_without_executing(self):
        (self.pack / "bounce.jsx").write_text("throw new Error('must not run'); // bounce", encoding="utf-8")
        result = self.library.search("bounce")
        self.assertEqual(result["results"][0]["path"], "bounce.jsx")
        value = self.library.read(self.source, "bounce.jsx")
        self.assertIn("must not run", value["content"])
        self.assertEqual(len(value["sha256"]), 64)

    def test_new_files_and_updated_content_are_used_next_request(self):
        (self.pack / "institucional.md").write_text("institucional evidência", encoding="utf-8")
        self.assertEqual(self.library.search("institucional")["results"][0]["path"], "institucional.md")
        (self.pack / "saas.md").write_text("# SaaS\nRevisão nova.", encoding="utf-8")
        self.assertIn("Revisão nova", self.library.read(self.source, "saas.md")["content"])

    def test_manifest_limits_scope_and_reads_nested_resources(self):
        (self.pack / "refs").mkdir()
        (self.pack / "refs/detail.md").write_text("SaaS detalhe", encoding="utf-8")
        (self.pack / "soma-knowledge.json").write_text(json.dumps({"documents": [{"path": "refs/detail.md", "tags": ["saas"]}]}), encoding="utf-8")
        self.assertEqual(len(self.library.search("saas")["results"]), 1)
        self.assertIn("detalhe", self.library.read(self.source, "refs/detail.md")["content"])
        with self.assertRaises(ValueError):
            self.library.read(self.source, "saas.md")

    def test_selected_brain_does_not_index_clients_or_personal_files(self):
        brain = self.root / "brain"
        (brain / "memoria/governantes").mkdir(parents=True)
        (brain / "conhecimento").mkdir()
        (brain / "memoria/governantes/alma.md").write_text("# Alma\nDireção.", encoding="utf-8")
        (brain / "memoria/pessoal.md").write_text("private_unselected_keyword", encoding="utf-8")
        (brain / "conhecimento/unselected.md").write_text("private_unselected_keyword", encoding="utf-8")
        data = self.library.configure(brain, private=False)
        self.assertTrue(data["source"]["private"])
        self.assertEqual(data["documents"], 1)
        self.assertEqual(self.library.search("private_unselected_keyword")["results"], [])

    def test_read_cannot_escape_root_or_selection(self):
        (self.root / "outside.md").write_text("outside", encoding="utf-8")
        for path in ["../outside.md", str(self.root / "outside.md"), "C:/outside.md"]:
            with self.assertRaises(ValueError):
                self.library.read(self.source, path)

    def test_invalid_manifest_rejected_without_changing_configuration(self):
        prior = self.library.config.read_bytes()
        (self.pack / "soma-knowledge.json").write_text(json.dumps({"documents": [{"path": "../outside.md"}]}), encoding="utf-8")
        with self.assertRaises(ValueError):
            self.library.configure(self.pack)
        self.assertEqual(self.library.config.read_bytes(), prior)

    def test_secret_like_text_never_returned(self):
        (self.pack / "secret.md").write_text("SaaS key ghp_" + "a" * 32, encoding="utf-8")
        self.assertTrue(all(item["path"] != "secret.md" for item in self.library.search("saas")["results"]))
        with self.assertRaises(ValueError):
            self.library.read(self.source, "secret.md")

    def test_plain_local_hub_token_and_connector_config_are_not_documents(self):
        (self.pack / "hub-token.txt").write_text("a" * 48, encoding="utf-8")
        (self.pack / "connectors.json").write_text('{"headers":{"Authorization":"example"}}', encoding="utf-8")
        self.assertEqual(len(self.library.documents(self.library.settings()["roots"][0])), 2)
        with self.assertRaises(ValueError):
            self.library.read(self.source, "hub-token.txt")

    def test_disconnect_preserves_files_and_other_sources(self):
        other = self.root / "other"
        other.mkdir()
        (other / "notes.md").write_text("other", encoding="utf-8")
        self.library.configure(other)
        self.library.remove(self.source)
        self.assertEqual(len(self.library.status()["sources"]), 1)
        self.assertTrue((self.pack / "saas.md").exists())

    def test_root_missing_reported_without_blocking_other_source(self):
        shutil.rmtree(self.pack)
        status = self.library.status()["sources"][0]
        self.assertFalse(status["available"])
        self.assertEqual(self.library.search("saas")["results"], [])

    def test_context_is_bounded_and_returns_provenance(self):
        for index in range(5):
            (self.pack / ("large%d.md" % index)).write_text("saas " * 12000, encoding="utf-8")
        result = self.library.context("saas")
        self.assertLessEqual(sum(len(item["content"]) for item in result["documents"]), 16000)
        self.assertLessEqual(len(result["documents"]), 3)
        self.assertTrue(all(item["source_id"] == self.source and item["sha256"] for item in result["documents"]))

    def test_core_guidance_is_loaded_alongside_topic_reference(self):
        (self.pack / "core.md").write_text("Briefing e revisão antes da entrega.", encoding="utf-8")
        (self.pack / "soma-knowledge.json").write_text(json.dumps({"documents": [{"path": "core.md", "role": "core"}, {"path": "saas.md"}]}), encoding="utf-8")
        documents = self.library.context("SaaS")["documents"]
        self.assertEqual(documents[0]["path"], "core.md")
        self.assertTrue(any(item["path"] == "saas.md" for item in documents))

    def test_oversized_and_symlink_resources_are_rejected(self):
        oversized = self.pack / "large.md"
        oversized.write_text("a" * (module.MAX_BYTES + 1), encoding="utf-8")
        with self.assertRaises(ValueError):
            module.safe_file(self.pack, "large.md")
        outside = self.root / "outside.md"
        outside.write_text("outside", encoding="utf-8")
        try:
            (self.pack / "link.md").symlink_to(outside)
        except OSError:
            self.skipTest("Criação de symlink não disponível neste ambiente Windows")
        with self.assertRaises(ValueError):
            module.safe_file(self.pack, "link.md")

    def test_malformed_config_is_preserved(self):
        self.library.config.write_text("broken-json", encoding="utf-8")
        with self.assertRaises(ValueError):
            self.library.configure(self.pack)
        self.assertEqual(self.library.config.read_text(encoding="utf-8"), "broken-json")

    def test_cli_returns_json_and_never_changes_source_documents(self):
        before = (self.pack / "saas.md").read_bytes()
        process = subprocess.run([sys.executable, str(ENGINE), "--config", str(self.library.config), "--request", json.dumps({"action": "context", "query": "saas"})], capture_output=True, encoding="utf-8", timeout=10)
        self.assertEqual(process.returncode, 0)
        self.assertTrue(json.loads(process.stdout)["ok"])
        self.assertEqual((self.pack / "saas.md").read_bytes(), before)


if __name__ == "__main__":
    unittest.main(verbosity=2)
