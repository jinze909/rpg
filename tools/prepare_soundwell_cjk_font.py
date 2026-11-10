#!/usr/bin/env python3
"""M9: stage the exact OFL game font before Unity imports native assets.

The pin is Git's *blob* hash for the original public font, not a claimed
checksum of an unknown system typeface. No pip, external package or paid
service. Only the existing Unity prepare CI job downloads this asset; the
published candidate verifies from the committed binary with no network.
"""
import hashlib
from pathlib import Path
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
            git_blob = b"blob " + str(len(raw)).encode() + b"\x00" + raw
            actual = hashlib.sha1(git_blob).hexdigest()
            if actual != EXPECTED_GIT_BLOB_SHA1:
                raise ValueError("Pinned OFL font blob SHA mismatch: " + actual)
            return raw
        except (OSError, ValueError, urllib.error.URLError) as error:
            last_error = error
            if attempt != 2:
                time.sleep(attempt + 1)
    raise RuntimeError("Unable to verify pinned OFL Chinese font") from last_error


def main():
    if FONT_PATH.exists():
        raise RuntimeError("Refusing to overwrite an existing CJK font asset.")
    if not FONT_PATH.parent.parent.is_dir():
        raise RuntimeError("Soundwell resource root missing.")
    raw = checked_download()
    FONT_PATH.parent.mkdir(parents=True, exist_ok=False)
    FONT_PATH.write_bytes(raw)
    print("Pinned OFL CJK font staged; size", len(raw),
          "Git blob SHA", EXPECTED_GIT_BLOB_SHA1)


if __name__ == "__main__":
    main()
