import gzip
import hashlib
import io
from pathlib import Path
import sys
import tarfile
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import install_ork


class InstallOrkTests(unittest.TestCase):
    def _build_unitypackage(self, path: Path, entries: dict[str, dict[str, bytes]]) -> str:
        raw_tar = io.BytesIO()
        with tarfile.open(fileobj=raw_tar, mode="w") as archive:
            for guid, parts in entries.items():
                for kind, data in parts.items():
                    info = tarfile.TarInfo(name=f"{guid}/{kind}")
                    info.size = len(data)
                    archive.addfile(info, io.BytesIO(data))
        compressed = gzip.compress(raw_tar.getvalue())
        path.write_bytes(compressed)
        return hashlib.sha256(compressed).hexdigest()

    def _close_spools(self, files: dict):
        for data in files.values():
            if hasattr(data, "close"):
                data.close()

    def test_installs_idempotently_including_through_symlinked_workspace_parent(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / "real_repo"
            root.mkdir()
            pkg = Path(tmp) / "ork.unitypackage"
            digest = self._build_unitypackage(pkg, {
                "guid1": {
                    "pathname": b"Assets/Gizmos",
                    "asset.meta": b"fileFormatVersion: 2\nguid: 11111111111111111111111111111111\n",
                },
                "guid2": {
                    "pathname": b"Assets/Gizmos/icon.png",
                    "asset.meta": b"fileFormatVersion: 2\nguid: 22222222222222222222222222222222\n",
                    "asset": b"\x89PNG-data",
                },
            })
            link_root = Path(tmp) / "symlink_workspace"
            link_root.symlink_to(root, target_is_directory=True)

            files = install_ork.read_package(pkg, expected_sha256=digest)
            try:
                added_first = install_ork.install(files, project_root=link_root)
                added_second = install_ork.install(files, project_root=link_root)
            finally:
                self._close_spools(files)

            self.assertEqual(added_first, 4)
            self.assertEqual(added_second, 0)
            self.assertEqual((root / "Assets/Gizmos/icon.png").read_bytes(), b"\x89PNG-data")

    def test_rejects_duplicate_pathname_in_package(self):
        with tempfile.TemporaryDirectory() as tmp:
            pkg = Path(tmp) / "dup.unitypackage"
            digest = self._build_unitypackage(pkg, {
                "guid1": {
                    "pathname": b"Assets/Gizmos/icon.png",
                    "asset.meta": b"meta1",
                    "asset": b"a",
                },
                "guid2": {
                    "pathname": b"Assets/Gizmos/icon.png",
                    "asset.meta": b"meta2",
                    "asset": b"b",
                },
            })
            with self.assertRaisesRegex(ValueError, "Duplicate asset path"):
                install_ork.read_package(pkg, expected_sha256=digest)

    def test_rejects_non_canonical_or_outside_vendor_path(self):
        with tempfile.TemporaryDirectory() as tmp:
            pkg = Path(tmp) / "badpath.unitypackage"
            digest = self._build_unitypackage(pkg, {
                "guid1": {
                    "pathname": b"Assets/Gizmos//icon.png",
                    "asset.meta": b"meta",
                    "asset": b"a",
                },
            })
            with self.assertRaisesRegex(ValueError, "Unexpected asset path"):
                install_ork.read_package(pkg, expected_sha256=digest)

    def test_rejects_missing_meta_in_package(self):
        with tempfile.TemporaryDirectory() as tmp:
            pkg = Path(tmp) / "nometa.unitypackage"
            digest = self._build_unitypackage(pkg, {
                "guid1": {
                    "pathname": b"Assets/Gizmos/icon.png",
                    "asset": b"a",
                },
            })
            with self.assertRaisesRegex(ValueError, "lacks metadata"):
                install_ork.read_package(pkg, expected_sha256=digest)

    def test_rejects_symlink_inside_repo(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            outside = root / "outside"
            outside.mkdir()
            (root / "Assets").mkdir()
            (root / "Assets/Gizmos").symlink_to(outside, target_is_directory=True)
            pkg = root / "ork.unitypackage"
            digest = self._build_unitypackage(pkg, {
                "guid1": {
                    "pathname": b"Assets/Gizmos/icon.png",
                    "asset.meta": b"meta",
                    "asset": b"a",
                },
            })
            files = install_ork.read_package(pkg, expected_sha256=digest)
            try:
                with self.assertRaisesRegex(ValueError, "Refusing to write through a symlink"):
                    install_ork.install(files, project_root=root)
            finally:
                self._close_spools(files)


if __name__ == "__main__":
    unittest.main()
