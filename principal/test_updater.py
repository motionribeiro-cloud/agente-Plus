"""Verify that the preview cannot silently launch a downloaded installer."""
import importlib.util
import json
from pathlib import Path
import tempfile
import types
import unittest
from unittest import mock


class ManualUpdateTests(unittest.TestCase):
    def test_checks_and_requests_never_install_or_start_background_worker(self):
        with tempfile.TemporaryDirectory(prefix="soma-update-test-") as directory:
            system = types.SimpleNamespace(WINDOWS=True, data_directory=lambda: directory, open_url=mock.Mock())
            core = types.ModuleType("soma_core")
            core.VERSION, core.system = "1.9.0-preview", system
            extensions = types.ModuleType("soma_core.extensions")
            extensions._ssl_context = lambda: None
            i18n = types.ModuleType("soma_core.i18n")
            i18n.L = lambda en, pt: pt
            modules = {"soma_core": core, "soma_core.extensions": extensions, "soma_core.i18n": i18n}
            with mock.patch.dict("sys.modules", modules):
                path = Path(__file__).parent / "components/hub/bridge/soma_hub_updater.py"
                spec = importlib.util.spec_from_file_location("updater_test", path)
                updater = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(updater)
            with mock.patch.object(updater, "_get", return_value=json.dumps({"tag_name": "v1.9.1", "assets": []}).encode()), mock.patch.object(updater.subprocess, "Popen", side_effect=AssertionError("Installer must not run")), mock.patch.object(updater.threading, "Thread") as thread:
                instance = updater.Updater()
                instance.start()
                instance.check_and_install()
                instance.request_install()
                thread.assert_not_called()
                self.assertFalse(instance.snapshot()["requested"])
                self.assertEqual(instance.snapshot()["status"], "manual")
                system.open_url.assert_called_once_with("https://github.com/motionribeiro-cloud/agente-Plus/releases")


if __name__ == "__main__":
    unittest.main(verbosity=2)
