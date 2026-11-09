#!/usr/bin/env python3
"""Collect P0 evidence; preparation is never clean-commit acceptance."""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

EXPECTED_TESTS = {
    "Rpg.Tests.Editor.P0BootstrapTests.SavedConfigurationAndWiringAreValid",
    "Rpg.Tests.Editor.P0BootstrapTests.FirstPlayInitializesFramework",
    "Rpg.Tests.Editor.P0BootstrapTests.SecondPlayInitializesFramework",
}
SOURCE_PATHS = ("Assets", "Packages", "ProjectSettings")


def git(*args):
    return subprocess.check_output(["git", *args])


def collect(mode):
    root = Path(__file__).resolve().parents[1]
    os.chdir(root)
    evidence = root / "artifacts/p0"
    evidence.mkdir(parents=True, exist_ok=True)
    errors = []
    action_outcome = os.environ.get("P0_UNITY_ACTION_OUTCOME")
    if action_outcome and action_outcome != "success":
        errors.append(f"Unity action did not succeed: {action_outcome}.")
    sha = git("rev-parse", "HEAD").decode().strip()
    if os.environ.get("P0_EXPECTED_SHA", os.environ.get("GITHUB_SHA", sha)) != sha:
        errors.append("Checkout HEAD does not match the requested candidate SHA.")

    changed = git("diff", "--name-only", "-z", "HEAD", "--", *SOURCE_PATHS)
    untracked = git("ls-files", "--others", "--exclude-standard", "-z", "--", *SOURCE_PATHS)
    paths = sorted(set(p.decode() for p in (changed + untracked).split(b"\0") if p))
    (evidence / "source-status.txt").write_bytes(git("status", "--short", "--", *SOURCE_PATHS))
    (evidence / "candidate.patch").write_bytes(git("diff", "--binary", "HEAD", "--", *SOURCE_PATHS))
    hashes = {}
    with zipfile.ZipFile(evidence / "candidate-files.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for name in paths:
            path = root / name
            if path.is_file():
                archive.write(path, name)
                hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
            else:
                hashes[name] = None  # A reviewed deletion is represented in candidate.patch.
    if mode == "verify" and paths:
        errors.append("Verification changed source files: " + ", ".join(paths))

    expected_tests = set(EXPECTED_TESTS)
    soundwell = mode == "verify" and (root / "Assets/Game/Scenes/SoundwellTrial.unity").is_file()
    if soundwell:
        expected_tests.add("Rpg.Tests.Editor.SoundwellTrialTests.PlayerCanRingWrapRetreatAndRetry")
        for filename in ("01-ringing.png", "02-wrapped-searching.png", "03-retreat-reset.png"):
            frame = evidence / "soundwell" / filename
            if not frame.is_file() or frame.stat().st_size < 1024:
                errors.append(f"Missing Soundwell frame: {filename}")

    try:
        result = ET.parse(evidence / "editmode-results.xml").getroot()
        cases = list(result.iter("test-case"))
        names = [case.get("fullname") for case in cases]
        if len(names) != len(expected_tests) or set(names) != expected_tests:
            errors.append("Expected exactly the configured Unity tests; empty/partial test runs are invalid.")
        if result.get("result") != "Passed" or any(case.get("result") != "Passed" for case in cases):
            errors.append("All configured Unity tests must pass; skipped/inconclusive tests are invalid.")
    except (OSError, ET.ParseError) as error:
        errors.append(f"Missing or invalid test XML: {error}")

    for filename in ("test-editor-exit-code.txt", "batch-editor-exit-code.txt"):
        try:
            if (evidence / filename).read_text().strip() != "0":
                errors.append(f"{filename} is not zero.")
        except OSError:
            errors.append(f"Missing {filename}.")
    editmode_markers = ("RPG_BOOTSTRAP_PLAY_VALIDATED:first", "RPG_BOOTSTRAP_PLAY_VALIDATED:second")
    if soundwell:
        editmode_markers += ("RPG_SOUNDWELL_SIGNAL_VALIDATED",)
    for filename, markers in {
        "editmode.log": editmode_markers,
        "batch.log": ("RPG_BOOTSTRAP_VALIDATED",),
    }.items():
        try:
            log = (evidence / filename).read_text(errors="replace")
            errors.extend(f"Missing {marker} in {filename}." for marker in markers if marker not in log)
            import_error = re.search(
                r"error CS\d+|Scripts have compiler errors|Unable to update following assemblies|"
                r"Failed to resolve assembly|Assembly .+ will not be loaded", log, re.IGNORECASE,
            )
            if import_error:
                errors.append(f"Unity import or compile error in {filename}: {import_error.group(0)}")
        except OSError:
            errors.append(f"Missing {filename}.")

    lock = root / "Packages/packages-lock.json"
    ugui_version = None
    if lock.is_file():
        (evidence / "packages-lock.json").write_bytes(lock.read_bytes())
        try:
            ugui = json.loads(lock.read_text())["dependencies"]["com.unity.ugui"]
            ugui_version = ugui["version"]
            if not ugui_version.startswith("2."):
                errors.append(f"Expected the editor's UGUI 2.x, got {ugui_version}.")
            print(f"Resolved UGUI: {ugui_version}")
        except (KeyError, ValueError) as error:
            errors.append(f"Invalid UGUI package lock entry: {error}")
    else:
        errors.append("Unity did not generate Packages/packages-lock.json.")
    vendor = subprocess.run([sys.executable, "tools/verify_ork.py"], capture_output=True, text=True)
    (evidence / "vendor-verification.txt").write_text(vendor.stdout + vendor.stderr)
    if vendor.returncode:
        errors.append("ORK vendor package verification failed.")

    status = "failed" if errors else ("prepared-candidate" if mode == "prepare" else "passed-clean-candidate")
    report = {
        "source_sha": os.environ.get("GITHUB_SHA", sha),
        "candidate_sha": sha if mode == "verify" else None,
        "mode": mode,
        "status": status,
        "unity": "6000.6.8f1",
        "ugui": ugui_version,
        "unity_action_outcome": action_outcome,
        "run_url": f"{os.environ.get('GITHUB_SERVER_URL', 'https://github.com')}/{os.environ.get('GITHUB_REPOSITORY', '')}/actions/runs/{os.environ.get('GITHUB_RUN_ID', '')}",
        "changed_files_sha256": hashes,
        "errors": errors,
        "note": "Preparation verifies SHA plus generated files; commit reviewed files and rerun verify for clean-SHA evidence.",
    }
    (evidence / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    summary = f"P0 evidence: {status} ({sha})\n" + "".join(f"- {error}\n" for error in errors)
    print(summary, end="")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as stream:
            stream.write(summary)
    return bool(errors)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("prepare", "verify"))
    sys.exit(collect(parser.parse_args().mode))
