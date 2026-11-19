import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from zipfile import ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import restore_unity_p0 as restore_mod


class RestoreUnityP0Tests(unittest.TestCase):
    def _write_checkout_required_outputs(self, root: Path):
        for rel in restore_mod.REQUIRED_OUTPUTS:
            target = root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            if rel == "Packages/packages-lock.json":
                target.write_text(json.dumps({
                    "dependencies": {
                        "com.unity.sdk.linux-x86_64": {
                            "version": "1.1.1",
                            "depth": 0,
                            "source": "registry",
                            "url": "https://packages.unity.com",
                        }
                    }
                }))
            else:
                target.write_text("existing\n")
        manifest = root / "Packages/manifest.json"
        manifest.parent.mkdir(parents=True, exist_ok=True)
        manifest.write_text(json.dumps({"dependencies": {"com.unity.ugui": "2.0.0"}}))
        mode_file = root / "production/ci/p0-mode.txt"
        mode_file.parent.mkdir(parents=True, exist_ok=True)
        mode_file.write_text("prepare\n")

    def _build_evidence(self, evidence_dir: Path, head: str, files: dict):
        evidence_dir.mkdir(parents=True, exist_ok=True)
        hashes = {}
        with ZipFile(evidence_dir / "candidate-files.zip", "w") as archive:
            for name, data in files.items():
                archive.writestr(name, data)
                hashes[name] = hashlib.sha256(data).hexdigest()
        (evidence_dir / "validation.json").write_text(json.dumps({
            "source_sha": head,
            "mode": "prepare",
            "status": "prepared-candidate",
            "changed_files_sha256": hashes,
        }))

    def test_manifest_change_uses_checkout_lockfile_when_lockfile_not_in_archive(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._write_checkout_required_outputs(root)
            updated_manifest = json.dumps({
                "dependencies": {
                    "com.unity.ugui": "2.0.0",
                    "com.unity.sdk.linux-x86_64": "1.1.1",
                }
            }).encode("utf-8")
            evidence = root / "evidence"
            self._build_evidence(evidence, "deadbeef", {"Packages/manifest.json": updated_manifest})

            old_cwd = os.getcwd()
            os.chdir(root)
            try:
                with mock.patch.object(subprocess, "check_output", return_value="deadbeef\n"):
                    restore_mod.restore(evidence)
                self.assertEqual((root / "Packages/manifest.json").read_bytes(), updated_manifest)
                self.assertEqual((root / "production/ci/p0-mode.txt").read_text(), "verify\n")
            finally:
                os.chdir(old_cwd)

    def test_manifest_change_rejects_missing_lockfile_in_archive_and_checkout(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._write_checkout_required_outputs(root)
            (root / "Packages/packages-lock.json").unlink()
            updated_manifest = json.dumps({
                "dependencies": {
                    "com.unity.ugui": "2.0.0",
                    "com.unity.sdk.linux-x86_64": "1.1.1",
                }
            }).encode("utf-8")
            evidence = root / "evidence"
            self._build_evidence(evidence, "deadbeef", {"Packages/manifest.json": updated_manifest})

            old_cwd = os.getcwd()
            os.chdir(root)
            try:
                with mock.patch.object(subprocess, "check_output", return_value="deadbeef\n"):
                    with self.assertRaisesRegex(ValueError, "required lockfile"):
                        restore_mod.restore(evidence)
            finally:
                os.chdir(old_cwd)

    def test_cli_missing_argument_exits_with_usage_message(self):
        proc = subprocess.run(
            [sys.executable, str(Path(__file__).resolve().parent / "restore_unity_p0.py")],
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(proc.returncode, 0)
        self.assertIn("Usage: restore_unity_p0.py", proc.stderr)


if __name__ == "__main__":
    unittest.main()
