"""Verify the committed ORK package files without needing the original archive."""

import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import zipfile

PROJECT = Path(__file__).resolve().parent.parent


def valid_vendor_manifest_path(name):
    """Limit integrity entries to canonical paths inside the saved ORK vendor roots."""
    if not isinstance(name, str) or not name or name != name.strip() or "\\" in name:
        return False
    path = PurePosixPath(name)
    if path.is_absolute() or ".." in path.parts or path.as_posix() != name:
        return False
    roots = (PurePosixPath("Assets/Gaming Is Love"), PurePosixPath("Assets/Gizmos"))
    return any(path == root or path == PurePosixPath(str(root) + ".meta")
               or root in path.parents for root in roots)


def _has_repo_symlink(project, relative):
    current = project
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            return True
    return False


def verify(project_root=None):
    project = PROJECT if project_root is None else Path(project_root)
    manifest = json.loads((project / "docs/ork-package-manifest.json").read_text(encoding="utf-8"))
    entries = manifest["files"]
    paths = set()
    for entry in entries:
        relative = PurePosixPath(entry["path"])
        if not valid_vendor_manifest_path(entry["path"]) or str(relative) in paths:
            raise ValueError(f"Invalid or duplicate manifest path: {relative}")
        paths.add(str(relative))
        if _has_repo_symlink(project, relative):
            raise ValueError(f"Symlink in package path: {relative}")
        target = project.joinpath(*relative.parts)
        data = target.read_bytes()
        if len(data) != entry["size_bytes"] or hashlib.sha256(data).hexdigest() != entry["sha256"]:
            raise ValueError(f"Package file differs: {relative}")

    # Check GUID collisions across vendor and game assets, not just the package.
    guids = {}
    for meta in sorted((project / "Assets").rglob("*.meta")):
        relative_meta = meta.relative_to(project)
        content = meta.read_text(encoding="utf-8", errors="replace")
        # Unity's MonoImporter rejects an unterminated final empty mapping
        # entry even when the GUID itself is correct. Catch the observed
        # script metadata failure before launching the expensive editor run.
        if (relative_meta.parts[:2] == ("Assets", "Game")
                and meta.name.endswith(".cs.meta")
                and re.search(r"(?m)^[ \t]*assetBundleVariant:[ \t]*\Z", content)):
            raise ValueError(f"Unterminated assetBundleVariant in script metadata: {relative_meta}; add a final newline")
        match = re.search(
            r"^guid: ([0-9a-f]{32})$",
            content,
            re.MULTILINE,
        )
        if match is None:
            raise ValueError(f"Missing or invalid GUID: {meta.relative_to(project)}")
        guid = match.group(1)
        if guid in guids:
            raise ValueError(f"Duplicate GUID: {meta} and {guids[guid]}")
        guids[guid] = meta

    archives = 0
    for entry in entries:
        target = project / entry["path"]
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
