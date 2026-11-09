#!/usr/bin/env python3
"""Restore the verified prepare artifact into its exact source checkout."""

import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys
from zipfile import ZipFile


def allowed_path(name):
    path = PurePosixPath(name)
    if path.is_absolute() or ".." in path.parts or path.as_posix() != name:
        return False
    if name in {
        "Assets/Game/Scenes/Bootstrap.unity",
        "Assets/Gaming Is Love/_Data.meta",
        "Packages/packages-lock.json",
        "ProjectSettings/SceneTemplateSettings.json",
    }:
        return True
    if len(path.parts) > 3 and path.parts[:3] == ("Assets", "Gaming Is Love", "_Data"):
        return path.suffix in {".asset", ".meta"}
    return len(path.parts) == 2 and path.parts[0] == "ProjectSettings" and path.suffix == ".asset"


def restore(evidence):
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    report = json.loads((evidence / "validation.json").read_text())
    if (report["source_sha"], report["mode"], report["status"]) != (head, "prepare", "prepared-candidate"):
        raise ValueError("Preparation evidence does not match this checkout.")
    hashes = report["changed_files_sha256"]
    if not hashes:
        raise ValueError("Preparation produced no generated Unity files.")
    rejected = [name for name, digest in hashes.items() if not digest or not allowed_path(name)]
    if rejected:
        raise ValueError(f"Preparation changed files outside the P0 output allowlist or deleted files: {rejected}")
    with ZipFile(evidence / "candidate-files.zip") as archive:
        if sorted(archive.namelist()) != sorted(hashes):
            raise ValueError("Candidate archive and preparation evidence differ.")
        for name, digest in hashes.items():
            data = archive.read(name)
            if hashlib.sha256(data).hexdigest() != digest:
                raise ValueError(f"Candidate file digest differs: {name}")
            path = Path(name)
            if any(p.is_symlink() for p in (path, *path.parents)):
                raise ValueError(f"Candidate file traverses a symlink: {name}")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        for name in hashes:
            if name.startswith("Assets/") and not name.endswith(".meta") and not Path(name + ".meta").is_file():
                raise ValueError(f"Candidate asset lacks metadata: {name}")

    for name in (
        "Assets/Game/Scenes/Bootstrap.unity",
        "Assets/Gaming Is Love/_Data/Project.asset",
        "ProjectSettings/EditorSettings.asset",
        "Packages/packages-lock.json",
    ):
        if not Path(name).is_file():
            raise ValueError(f"Candidate lacks required Unity output: {name}")
    Path("production/ci/p0-mode.txt").write_text("verify\n")


if __name__ == "__main__":
    restore(Path(sys.argv[1]))
