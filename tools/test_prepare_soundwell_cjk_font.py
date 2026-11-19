import hashlib
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import prepare_soundwell_cjk_font as font_mod


def _make_font_zip(member_name, payload):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr(member_name, payload)
    return buf.getvalue()


class PrepareSoundwellCjkFontTests(unittest.TestCase):
    def test_git_blob_sha1_matches_git_object_hash(self):
        # Known Git blob SHA-1 for b"hello\n"
        self.assertEqual(
            font_mod.git_blob_sha1(b"hello\n"),
            "ce013625030ba8dba906f756967f9e9ca394464a",
        )

    def test_extract_font_from_pinned_archive_verifies_sha256_and_ttf_header(self):
        payload = b"\x00\x01\x00\x00fusion-pixel-ttf"
        archive_bytes = _make_font_zip(font_mod.ARCHIVE_MEMBER, payload)
        digest = hashlib.sha256(archive_bytes).hexdigest()
        with mock.patch.object(font_mod, "EXPECTED_ARCHIVE_SIZE", len(archive_bytes)), \
             mock.patch.object(font_mod, "EXPECTED_ARCHIVE_SHA256", digest):
            self.assertEqual(font_mod.extract_font_from_archive(archive_bytes), payload)

        with mock.patch.object(font_mod, "EXPECTED_ARCHIVE_SIZE", len(archive_bytes)), \
             mock.patch.object(font_mod, "EXPECTED_ARCHIVE_SHA256", "0" * 64):
            with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
                font_mod.extract_font_from_archive(archive_bytes)

    def test_existing_pinned_font_reuses_without_network(self):
        payload = b"\x00\x01\x00\x00pinned-font-bytes"
        digest = font_mod.git_blob_sha1(payload)
        with tempfile.TemporaryDirectory() as tmp:
            font_path = Path(tmp) / "Soundwell/Fonts/SoundwellChinese.ttf"
            font_path.parent.mkdir(parents=True, exist_ok=True)
            font_path.write_bytes(payload)
            with mock.patch.object(font_mod, "EXPECTED_SIZE", len(payload)), \
                 mock.patch.object(font_mod, "EXPECTED_GIT_BLOB_SHA1", digest), \
                 mock.patch.object(font_mod, "checked_download") as download_mock:
                font_mod.main(font_path)
                download_mock.assert_not_called()

    def test_existing_mismatched_font_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            font_path = Path(tmp) / "Soundwell/Fonts/SoundwellChinese.ttf"
            font_path.parent.mkdir(parents=True, exist_ok=True)
            font_path.write_bytes(b"corrupt")
            with mock.patch.object(font_mod, "checked_download", return_value=b"\x00\x01\x00\x00valid"):
                with self.assertRaisesRegex(ValueError, "not the pinned OFL blob"):
                    font_mod.main(font_path)

    def test_symlink_font_path_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            outside = root / "outside.ttf"
            outside.write_bytes(b"fake")
            font_path = root / "Soundwell/Fonts/SoundwellChinese.ttf"
            font_path.parent.mkdir(parents=True, exist_ok=True)
            font_path.symlink_to(outside)
            with self.assertRaisesRegex(RuntimeError, "must not be a symlink"):
                font_mod.main(font_path)

    def test_stages_downloaded_font_atomically_when_missing(self):
        payload = b"\x00\x01\x00\x00fresh-pinned-font"
        with tempfile.TemporaryDirectory() as tmp:
            resource_root = Path(tmp) / "Soundwell"
            resource_root.mkdir(parents=True, exist_ok=True)
            font_path = resource_root / "Fonts/SoundwellChinese.ttf"
            with mock.patch.object(font_mod, "checked_download", return_value=payload):
                font_mod.main(font_path)
            self.assertEqual(font_path.read_bytes(), payload)
            # No leftover staging temp files remain in Fonts/
            self.assertEqual([p.name for p in font_path.parent.iterdir()], ["SoundwellChinese.ttf"])


if __name__ == "__main__":
    unittest.main()
