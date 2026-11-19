#!/usr/bin/env python3
"""M9: stage the exact OFL pixel CJK game font before Unity imports native assets.

Pins TakWolf/fusion-pixel-font (SIL OFL 1.1) 12px proportional Simplified
Chinese TrueType font by exact GitHub Release archive size and SHA-256.
No pip, external package or paid service.
"""
import hashlib
import http.client
import io
import os
from pathlib import Path, PurePosixPath
import tempfile
import time
import urllib.error
import urllib.request
import zipfile

SOURCE_RELEASE = "2026.11.10"
SOURCE_URL = (
    "https://github.com/TakWolf/fusion-pixel-font/releases/download/"
    + SOURCE_RELEASE
    + "/fusion-pixel-font-12px-proportional-ttf-v"
    + SOURCE_RELEASE
    + ".zip"
)
ARCHIVE_MEMBER = "fusion-pixel-12px-proportional-zh_hans.ttf"
EXPECTED_ARCHIVE_SIZE = 51250684
EXPECTED_ARCHIVE_SHA256 = "aa6b4da3f80d95af45f41d74b37db0fa3a832d7f428adeb7234b4758d50bbae9"
EXPECTED_SIZE = None
EXPECTED_GIT_BLOB_SHA1 = None
FONT_PATH = Path("Assets/Game/Resources/Soundwell/Fonts/SoundwellChinese.ttf")


def git_blob_sha1(data):
    return hashlib.sha1(
        b"blob " + str(len(data)).encode() + b"\x00" + data
    ).hexdigest()


def extract_font_from_archive(archive_bytes):
    if len(archive_bytes) != EXPECTED_ARCHIVE_SIZE:
        raise ValueError("Unexpected OFL CJK font archive size: " + str(len(archive_bytes)))
    actual_sha256 = hashlib.sha256(archive_bytes).hexdigest()
    if actual_sha256 != EXPECTED_ARCHIVE_SHA256:
        raise ValueError("Pinned OFL font archive SHA-256 mismatch: " + actual_sha256)
    with zipfile.ZipFile(io.BytesIO(archive_bytes)) as archive:
        matches = [
            name for name in archive.namelist()
            if PurePosixPath(name).name == ARCHIVE_MEMBER
        ]
        if len(matches) != 1:
            raise ValueError("Expected single member " + ARCHIVE_MEMBER + " in archive.")
        raw = archive.read(matches[0])
    if len(raw) < 4 or raw[:4] != b"\x00\x01\x00\x00":
        raise ValueError("Extracted font is not a valid TrueType binary.")
    return raw


def checked_download():
    last_error = None
    for attempt in range(3):
        try:
            req = urllib.request.Request(
                SOURCE_URL, headers={"User-Agent": "rpg-unity-m9-font-pinner/2"}
            )
            with urllib.request.urlopen(req, timeout=90) as response:
                archive_bytes = response.read(EXPECTED_ARCHIVE_SIZE + 1)
            raw = extract_font_from_archive(archive_bytes)
            if EXPECTED_SIZE is not None and len(raw) != EXPECTED_SIZE:
                raise ValueError("Unexpected OFL CJK font size: " + str(len(raw)))
            actual = git_blob_sha1(raw)
            if EXPECTED_GIT_BLOB_SHA1 is not None and actual != EXPECTED_GIT_BLOB_SHA1:
                raise ValueError("Pinned OFL font blob SHA mismatch: " + actual)
            return raw
        except (
            OSError,
            ValueError,
            zipfile.BadZipFile,
            urllib.error.URLError,
            http.client.HTTPException,
        ) as error:
            last_error = error
            if attempt != 2:
                time.sleep(attempt + 1)
    raise RuntimeError("Unable to verify pinned OFL Chinese font") from last_error


def _atomic_write_bytes(target, data):
    target.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_name = tempfile.mkstemp(prefix=".font-stage-", dir=target.parent)
    tmp_path = Path(tmp_name)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(data)
        tmp_path.replace(target)
    except BaseException:
        if tmp_path.exists():
            tmp_path.unlink()
        raise


def main(font_path=None):
    target = FONT_PATH if font_path is None else Path(font_path)
    if target.is_symlink() or target.parent.is_symlink():
        raise RuntimeError("Expected font path must not be a symlink.")
    if target.is_file():
        current = target.read_bytes()
        digest = git_blob_sha1(current)
        if EXPECTED_SIZE is not None and EXPECTED_GIT_BLOB_SHA1 is not None:
            if len(current) != EXPECTED_SIZE or digest != EXPECTED_GIT_BLOB_SHA1:
                raise ValueError("Existing Chinese font is not the pinned OFL blob.")
            print("Existing pinned OFL Chinese font verified:", digest)
            return
        expected = checked_download()
        if current != expected:
            raise ValueError("Existing Chinese font is not the pinned OFL blob.")
        print(
            "Existing pinned OFL Chinese font verified against archive:",
            "size", len(current),
            "Git blob SHA", digest,
            "SHA-256", hashlib.sha256(current).hexdigest(),
        )
        return
    if target.exists():
        raise RuntimeError("Expected font path is not a regular file.")
    if not target.parent.parent.is_dir():
        raise RuntimeError("Soundwell resource root missing.")
    raw = checked_download()
    _atomic_write_bytes(target, raw)
    print(
        "Pinned OFL CJK font staged; size", len(raw),
        "Git blob SHA", git_blob_sha1(raw),
        "SHA-256", hashlib.sha256(raw).hexdigest(),
    )


if __name__ == "__main__":
    main()
