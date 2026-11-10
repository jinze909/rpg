#!/usr/bin/env python3
"""Restore the verified prepare artifact into its exact source checkout."""

import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys
from zipfile import ZipFile


# The pinned Linux Editor adds its target SDK and toolchain packages on import.
LINUX_EDITOR_PACKAGES = {
    "com.unity.sdk.linux-arm64",
    "com.unity.sdk.linux-x86_64",
    "com.unity.sysroot.base",
    "com.unity.toolchain.linux-x86_64-linux",
}


def allowed_path(name):
    path = PurePosixPath(name)
    if path.is_absolute() or ".." in path.parts or path.as_posix() != name:
        return False
    if name in {
        # M4: only these Unity-authored game-owned clips/controller and their
        # folder/asset metadata may be published from an animation prepare run.
        "Assets/Game/Resources/Soundwell.meta",
        "Assets/Game/Resources/Soundwell/Clips.meta",
        "Assets/Game/Resources/Soundwell/PlaceholderPlayer.controller",
        "Assets/Game/Resources/Soundwell/PlaceholderPlayer.controller.meta",
        "Assets/Game/Resources/Soundwell/Clips/Idle.anim",
        "Assets/Game/Resources/Soundwell/Clips/Idle.anim.meta",
        "Assets/Game/Resources/Soundwell/Clips/Move.anim",
        "Assets/Game/Resources/Soundwell/Clips/Move.anim.meta",
        "Assets/Game/Resources/Soundwell/Clips/Action.anim",
        "Assets/Game/Resources/Soundwell/Clips/Action.anim.meta",
        "Assets/Game/Resources/Soundwell/Clips/Hit.anim",
        "Assets/Game/Resources/Soundwell/Clips/Hit.anim.meta",
        "Assets/Game/Scenes/Bootstrap.unity",
        "Assets/Game/Rendering.meta",
        "Assets/Game/Rendering/URP2D.meta",
        "Assets/UniversalRenderPipelineGlobalSettings.asset",
        "Assets/UniversalRenderPipelineGlobalSettings.asset.meta",
        "Assets/DefaultVolumeProfile.asset",
        "Assets/DefaultVolumeProfile.asset.meta",
        "Assets/Gaming Is Love/_Data.meta",
        "Packages/manifest.json",
        "Packages/packages-lock.json",
        "ProjectSettings/SceneTemplateSettings.json",
    }:
        return True
    if len(path.parts) == 5 and path.parts[:4] == ("Assets", "Game", "Rendering", "URP2D"):
        return path.suffix in {".asset", ".meta"}
    if len(path.parts) > 3 and path.parts[:3] == ("Assets", "Gaming Is Love", "_Data"):
        return path.suffix in {".asset", ".meta"}
    return len(path.parts) == 2 and path.parts[0] == "ProjectSettings" and path.suffix == ".asset"


def validate_manifest(previous_bytes, generated_bytes, lock_bytes):
    previous = json.loads(previous_bytes)
    generated = json.loads(generated_bytes)
    lock = json.loads(lock_bytes)["dependencies"]
    previous_dependencies = previous.pop("dependencies")
    generated_dependencies = generated.pop("dependencies")
    if previous != generated or previous_dependencies.keys() - generated_dependencies.keys():
        raise ValueError("Unity changed manifest configuration or removed a direct dependency.")

    for name, version in sorted(generated_dependencies.items()):
        if previous_dependencies.get(name) == version:
            continue
        resolved = lock.get(name, {})
        builtin = name.startswith("com.unity.") and resolved.get("source") == "builtin"
        linux_sdk = (
            name not in previous_dependencies
            and name in LINUX_EDITOR_PACKAGES
            and version == "1.1.1"
            and resolved.get("source") == "registry"
            and resolved.get("url") == "https://packages.unity.com"
        )
        if not ((builtin or linux_sdk) and resolved.get("depth") == 0
                and resolved.get("version") == version):
            raise ValueError(f"Manifest change is not an approved editor dependency: {name}")
        print(f"Unity normalized {name}: {previous_dependencies.get(name, '(new)')} -> {version}")


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
        manifest = "Packages/manifest.json"
        if manifest in hashes:
            validate_manifest(Path(manifest).read_bytes(), archive.read(manifest),
                              archive.read("Packages/packages-lock.json"))
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
