#!/usr/bin/env python3
"""Collect P0 evidence; preparation is never clean-commit acceptance."""
import argparse
import hashlib
import json
import os
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
    if os.environ.get("GITHUB_SHA", sha) != sha:
        errors.append("Checkout HEAD does not match the workflow SHA.")

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
        errors.append("Verification changed source files; this is not clean-SHA acceptance.")

    try:
        result = ET.parse(evidence / "editmode-results.xml").getroot()
        cases = list(result.iter("test-case"))
        names = [case.get("fullname") for case in cases]
        if len(names) != len(EXPECTED_TESTS) or set(names) != EXPECTED_TESTS:
            errors.append("Expected exactly the three P0 tests; empty/partial test runs are invalid.")
        if result.get("result") != "Passed" or any(case.get("result") != "Passed" for case in cases):
            errors.append("All P0 tests must pass; skipped/inconclusive tests are invalid.")
    except (OSError, ET.ParseError) as error:
        errors.append(f"Missing or invalid test XML: {error}")

    for filename in ("test-editor-exit-code.txt", "batch-editor-exit-code.txt"):
        try:
            if (evidence / filename).read_text().strip() != "0":
                errors.append(f"{filename} is not zero.")
        except OSError:
            errors.append(f"Missing {filename}.")
    for filename, markers in {
        "editmode.log": ("RPG_BOOTSTRAP_PLAY_VALIDATED:first", "RPG_BOOTSTRAP_PLAY_VALIDATED:second"),
        "batch.log": ("RPG_BOOTSTRAP_VALIDATED",),
    }.items():
        try:
            log = (evidence / filename).read_text(errors="replace")
            errors.extend(f"Missing {marker} in {filename}." for marker in markers if marker not in log)
        except OSError:
            errors.append(f"Missing {filename}.")

    lock = root / "Packages/packages-lock.json"
    if lock.is_file():
        (evidence / "packages-lock.json").write_bytes(lock.read_bytes())
    else:
        errors.append("Unity did not generate Packages/packages-lock.json.")
    vendor = subprocess.run([sys.executable, "tools/verify_ork.py"], capture_output=True, text=True)
    (evidence / "vendor-verification.txt").write_text(vendor.stdout + vendor.stderr)
    if vendor.returncode:
        errors.append("ORK vendor package verification failed.")

    status = "failed" if errors else ("prepared-candidate" if mode == "prepare" else "passed-clean-candidate")
    report = {
        "source_sha": sha,
        "mode": mode,
        "status": status,
        "unity": "6000.6.8f1",
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
