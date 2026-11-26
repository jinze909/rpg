import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import verify_ork


class VerifyOrkTests(unittest.TestCase):
    def _make_project(self, root: Path, files: dict, extra_metas: dict | None = None):
        entries = []
        for rel, data in files.items():
            target = root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            entries.append({
                "path": rel,
                "size_bytes": len(data),
                "sha256": hashlib.sha256(data).hexdigest(),
            })
        for rel, text in (extra_metas or {}).items():
            target = root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(text, encoding="utf-8")
        manifest_path = root / "docs/ork-package-manifest.json"
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps({"files": entries}), encoding="utf-8")

    def _zip_bytes(self, members: dict[str, bytes]) -> bytes:
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as zf:
            for name, content in members.items():
                zf.writestr(name, content)
        return buf.getvalue()

    def test_valid_manifest_unique_guids_and_zip_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "real_repo"
            root.mkdir()
            zip_payload = self._zip_bytes({"hello.txt": b"world"})
            self._make_project(
                root,
                {
                    "Assets/Gaming Is Love/Makinom 2/source.zip": zip_payload,
                    "Assets/Gizmos/icon.png": b"\x89PNG",
                },
                extra_metas={
                    "Assets/Gaming Is Love/Makinom 2/source.zip.meta": "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n",
                    "Assets/Gizmos/icon.png.meta": "fileFormatVersion: 2\nguid: fedcba9876543210fedcba9876543210\n",
                },
            )
            # Accessing the repo through a symlinked workspace parent must still pass.
            link_root = Path(tmp) / "symlink_workspace"
            link_root.symlink_to(root, target_is_directory=True)
            verify_ork.verify(link_root)

    def test_rejects_symlink_inside_repo_package_path(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            outside = root / "outside.bin"
            outside.write_bytes(b"secret")
            rel = "Assets/Gizmos/link.bin"
            target = root / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            target.symlink_to(outside)
            manifest_path = root / "docs/ork-package-manifest.json"
            manifest_path.parent.mkdir(parents=True, exist_ok=True)
            manifest_path.write_text(json.dumps({
                "files": [{
                    "path": rel,
                    "size_bytes": 6,
                    "sha256": hashlib.sha256(b"secret").hexdigest(),
                }]
            }), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Symlink in package path"):
                verify_ork.verify(root)

    def test_rejects_duplicate_guid_across_assets(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._make_project(
                root,
                {"Assets/Gizmos/a.png": b"a"},
                extra_metas={
                    "Assets/Gizmos/a.png.meta": "fileFormatVersion: 2\nguid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n",
                    "Assets/Game/b.cs.meta": "fileFormatVersion: 2\nguid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n",
                },
            )
            with self.assertRaisesRegex(ValueError, "Duplicate GUID"):
                verify_ork.verify(root)

    def test_rejects_missing_or_invalid_guid(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._make_project(
                root,
                {"Assets/Gizmos/a.png": b"a"},
                extra_metas={
                    "Assets/Gizmos/a.png.meta": "fileFormatVersion: 2\nguid: not-a-valid-guid\n",
                },
            )
            with self.assertRaisesRegex(ValueError, "Missing or invalid GUID"):
                verify_ork.verify(root)

    def test_rejects_unterminated_empty_script_asset_bundle_variant(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            relative = "Assets/Game/Tests/Editor/Example.cs.meta"
            content = (
                "fileFormatVersion: 2\nguid: c84a2f91e5374b609d18e3b7a4c2f6d9\n"
                "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
                "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant:"
            )
            self._make_project(root, {}, extra_metas={relative: content})
            with self.assertRaisesRegex(ValueError, "Unterminated assetBundleVariant.*Example.cs.meta"):
                verify_ork.verify(root)

            # Preserve the GUID and verify the Unity-compatible terminator.
            (root / relative).write_text(content + " \n", encoding="utf-8")
            verify_ork.verify(root)

    def test_accepts_nonempty_script_asset_bundle_variant(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._make_project(root, {}, extra_metas={
                "Assets/Game/Example.cs.meta": (
                    "fileFormatVersion: 2\nguid: c84a2f91e5374b609d18e3b7a4c2f6d9\n"
                    "MonoImporter:\n  assetBundleVariant: example"
                ),
            })
            verify_ork.verify(root)

    def test_rejects_invalid_vendor_manifest_path(self):
        self.assertFalse(verify_ork.valid_vendor_manifest_path("Assets/Game/Foo.cs"))
        self.assertFalse(verify_ork.valid_vendor_manifest_path("Assets/Gizmos/../Game/Foo.cs"))
        self.assertFalse(verify_ork.valid_vendor_manifest_path("Assets\\Gizmos\\a.png"))
        self.assertTrue(verify_ork.valid_vendor_manifest_path("Assets/Gaming Is Love.meta"))
        self.assertTrue(verify_ork.valid_vendor_manifest_path("Assets/Gizmos/icon.png"))

    def test_rejects_digest_mismatch(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._make_project(root, {"Assets/Gizmos/a.png": b"abc"})
            (root / "Assets/Gizmos/a.png").write_bytes(b"xyz")
            with self.assertRaisesRegex(ValueError, "Package file differs"):
                verify_ork.verify(root)

    def test_rejects_corrupt_zip(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._make_project(root, {"Assets/Gaming Is Love/bad.zip": b"not a zip"})
            with self.assertRaises(zipfile.BadZipFile):
                verify_ork.verify(root)


if __name__ == "__main__":
    unittest.main()
