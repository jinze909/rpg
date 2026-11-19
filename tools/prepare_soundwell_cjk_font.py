#!/usr/bin/env python3
"""M9: stage the exact OFL game font before Unity imports native assets.

The pin is Git's *blob* hash for the original public font, not a claimed
checksum of an unknown system typeface. No pip, external package or paid
service. Only the existing Unity prepare CI job downloads this asset; the
published candidate verifies from the committed binary with no network.
"""
import hashlib
import http.client
import os
from pathlib import Path
import tempfile
import time
import urllib.error
import urllib.request

SOURCE_COMMIT = "56328c01842464f3af41632473d3e9a4731b7627"
SOURCE_URL = (
    "https://raw.githubusercontent.com/google/fonts/" + SOURCE_COMMIT +
    "/ofl/zcoolqingkehuangyou/ZCOOLQingKeHuangYou-Regular.ttf"
)
EXPECTED_GIT_BLOB_SHA1 = "982a3b80143268592f573feb196791fbc2330e17"
EXPECTED_SIZE = 8328684
FONT_PATH = Path("Assets/Game/Resources/Soundwell/Fonts/SoundwellChinese.ttf")


def git_blob_sha1(data):
    return hashlib.sha1(
        b"blob " + str(len(data)).encode() + b"\x00" + data
    ).hexdigest()


def checked_download():
    last_error = None
    for attempt in range(3):
        try:
            req = urllib.request.Request(
                SOURCE_URL, headers={"User-Agent": "rpg-unity-m9-font-pinner/1"}
            )
            with urllib.request.urlopen(req, timeout=45) as response:
                raw = response.read(EXPECTED_SIZE + 1)
            if len(raw) != EXPECTED_SIZE:
                raise ValueError("Unexpected OFL CJK font size: " + str(len(raw)))
            actual = git_blob_sha1(raw)
            if actual != EXPECTED_GIT_BLOB_SHA1:
                raise ValueError("Pinned OFL font blob SHA mismatch: " + actual)
            return raw
        except (OSError, ValueError, urllib.error.URLError, http.client.HTTPException) as error:
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
        # All later migrations inherit the already-published OFL binary.
        # Hash its actual bytes before reusing it, without network access or
        # reauthoring a new asset/metadata file on each Unity preparation.
        current = target.read_bytes()
        digest = git_blob_sha1(current)
        if len(current) != EXPECTED_SIZE or digest != EXPECTED_GIT_BLOB_SHA1:
            raise ValueError("Existing Chinese font is not the pinned OFL blob.")
        print("Existing pinned OFL Chinese font verified:", digest)
        return
    if target.exists():
        raise RuntimeError("Expected font path is not a regular file.")
    if not target.parent.parent.is_dir():
        raise RuntimeError("Soundwell resource root missing.")
    raw = checked_download()
    _atomic_write_bytes(target, raw)
    print("Pinned OFL CJK font staged; size", len(raw),
          "Git blob SHA", EXPECTED_GIT_BLOB_SHA1)


if __name__ == "__main__":
    main()
