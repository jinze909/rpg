"""Verify the committed ORK package files without needing the original archive."""

import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import zipfile

PROJECT = Path(__file__).resolve().parent.parent


def verify():
    manifest = json.loads((PROJECT / "docs/ork-package-manifest.json").read_text())
    entries = manifest["files"]
    paths = set()
    for entry in entries:
        relative = PurePosixPath(entry["path"])
        if relative.is_absolute() or ".." in relative.parts or str(relative) in paths:
            raise ValueError(f"Invalid or duplicate manifest path: {relative}")
        paths.add(str(relative))
        target = PROJECT.joinpath(*relative.parts)
        if any(p.is_symlink() for p in (target, *target.parents)):
            raise ValueError(f"Symlink in package path: {relative}")
        data = target.read_bytes()
        if len(data) != entry["size_bytes"] or hashlib.sha256(data).hexdigest() != entry["sha256"]:
            raise ValueError(f"Package file differs: {relative}")

    # Check GUID collisions across vendor and game assets, not just the package.
    guids = {}
    for meta in sorted((PROJECT / "Assets").rglob("*.meta")):
        match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(), re.MULTILINE)
        if match is None:
            raise ValueError(f"Missing or invalid GUID: {meta.relative_to(PROJECT)}")
        guid = match.group(1)
        if guid in guids:
            raise ValueError(f"Duplicate GUID: {meta} and {guids[guid]}")
        guids[guid] = meta

    archives = 0
    for entry in entries:
        target = PROJECT / entry["path"]
        if target.suffix.lower() == ".zip":
            with zipfile.ZipFile(target) as archive:
                bad = archive.testzip()
                if bad is not None:
                    raise ValueError(f"ZIP CRC failed: {target}: {bad}")
            archives += 1
    print(f"Verified {len(entries)} package files, {len(guids)} unique asset GUIDs, {archives} source ZIPs.")


if __name__ == "__main__":
    try:
        verify()
    except (OSError, ValueError, KeyError, zipfile.BadZipFile) as error:
        raise SystemExit(str(error))
