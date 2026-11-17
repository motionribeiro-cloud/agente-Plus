"""Manual update checks for the Soma preview; never launches downloaded installers.

The update button opens the project's release page. No background auto-install
or silent /update mode is supported by this preview installer.
"""
import hashlib
import json
import os
import subprocess
import threading
import time
import urllib.request

from soma_core import VERSION, system
from soma_core.extensions import _ssl_context
from soma_core.i18n import L

REPOSITORY = "motionribeiro-cloud/agente-Plus"
API = "https://api.github.com/repos/%s/releases/latest" % REPOSITORY
RELEASES_PAGE = "https://github.com/%s/releases" % REPOSITORY
INSTALLER = "Soma-Agente-Instalador.exe"
CHECK_EVERY = 6 * 3600
RETRY_EVERY = 10 * 60
ADOBE_PROCESSES = ("AfterFX.exe", "Photoshop.exe", "Illustrator.exe", "Adobe Premiere Pro.exe")


def _version_tuple(text):
    parts = []
    for piece in str(text or "").lstrip("vV").split("."):
        digits = "".join(ch for ch in piece if ch.isdigit())
        parts.append(int(digits) if digits else 0)
    return tuple(parts + [0] * (3 - len(parts)))


def installed_version():
    override = os.environ.get("SOMA_INSTALLED_VERSION")
    if override:
        return override
    try:
        with open(os.path.join(system.data_directory(), "installed-version.txt"), "r", encoding="utf-8") as handle:
            return handle.read().strip() or VERSION
    except OSError:
        return VERSION


def _get(url, accept="application/vnd.github+json"):
    request = urllib.request.Request(url, headers={"User-Agent": "SomaAgente-Hub/" + VERSION, "Accept": accept})
    with urllib.request.urlopen(request, timeout=60, context=_ssl_context()) as response:
        return response.read()


def _adobe_running():
    if not system.WINDOWS:
        return []
    try:
        out = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True, text=True, timeout=30, creationflags=system.hidden_window_flags()).stdout
    except (OSError, subprocess.SubprocessError):
        return []
    running = []
    for line in out.splitlines():
        name = line.split('","')[0].strip('"')
        if name in ADOBE_PROCESSES and name not in running:
            running.append(name)
    return running


class Updater:
    def __init__(self):
        self.state = {"current": installed_version(), "latest": None, "status": "idle", "detail": "", "notes": "",
                      "checked": None, "available": False, "requested": False}
        self.lock = threading.Lock()
        self.ready_path = None
        self.dry_run = os.environ.get("SOMA_UPDATE_DRY_RUN") == "1"
        self.wake = threading.Event()

    def disabled(self):
        return os.path.exists(os.path.join(system.data_directory(), "auto-update-off.txt"))

    def _set(self, **values):
        with self.lock:
            self.state.update(values)

    def snapshot(self):
        with self.lock:
            return dict(self.state)

    def start(self):
        self._set(status="manual", detail="Atualizações desta preview são manuais; use a página de releases do Soma.")

    def _loop(self):
        self.wake.wait(float(os.environ.get("SOMA_UPDATE_FIRST_DELAY", "60")))
        while True:
            self.wake.clear()
            wait = CHECK_EVERY
            try:
                if self.snapshot()["requested"] or not self.disabled():
                    wait = self.check_and_install()
                    # Asked for by the user: keep trying until the Adobe programs close.
                    if self.snapshot()["requested"] and wait == RETRY_EVERY:
                        wait = 30
            except Exception as error:  # noqa: BLE001 - try again next round
                self._set(status="error", detail="%s: %s" % (type(error).__name__, error))
            self.wake.wait(wait)

    def _fetch(self):
        release = json.loads(_get(API).decode("utf-8"))
        latest = release.get("tag_name", "")
        newer = _version_tuple(latest) > _version_tuple(installed_version())
        self._set(latest=latest.lstrip("vV"), notes=(release.get("body") or "")[:2000], checked=time.time(),
                  current=installed_version(), available=newer)
        return release, newer

    def check(self, force=False):
        """Reads the latest Release now (at most every few minutes unless forced).
        Used by the plugins when they open and by the soma_check_update tool."""
        checked = self.snapshot()["checked"]
        if force or not checked or time.time() - checked > 300:
            try:
                _release, newer = self._fetch()
                if self.snapshot()["status"] not in ("downloading", "waiting", "installing"):
                    self._set(status="available" if newer else "up-to-date", detail="")
            except Exception as error:  # noqa: BLE001 - reported to the caller
                self._set(status="error", detail="%s: %s" % (type(error).__name__, error))
        return self.snapshot()

    def request_install(self):
        system.open_url(RELEASES_PAGE)
        self._set(requested=False, status="manual", detail="Baixe a atualização na página de releases e execute o instalador com os aplicativos fechados.")
        return self.snapshot()

    def check_and_install(self):
        self.check(force=True)
        self._set(requested=False, status="manual", detail="Instalação automática desativada nesta preview. Use a página de releases.")
        return CHECK_EVERY

    def _download(self, release):
        assets = {a.get("name"): a.get("browser_download_url") for a in release.get("assets", [])}
        if INSTALLER not in assets or INSTALLER + ".sha256" not in assets:
            raise RuntimeError("The release %s has no %s with its .sha256." % (release.get("tag_name"), INSTALLER))
        expected = _get(assets[INSTALLER + ".sha256"], "application/octet-stream").decode("utf-8").split()[0].strip().lower()
        self._set(status="downloading", detail="")
        data = _get(assets[INSTALLER], "application/octet-stream")
        actual = hashlib.sha256(data).hexdigest()
        if actual != expected:
            raise RuntimeError("Downloaded installer does not match the published SHA-256; not installing.")
        folder = os.path.join(system.data_directory(), "updates")
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, "Soma-Agente-Instalador-%s.exe" % release.get("tag_name", "new").lstrip("vV"))
        with open(path, "wb") as handle:
            handle.write(data)
        return path
