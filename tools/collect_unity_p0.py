#!/usr/bin/env python3
"""Collect P0 evidence; preparation is never clean-commit acceptance."""
import argparse
import hashlib
import json
import math
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
    "Rpg.Tests.Editor.P0BootstrapTests.Urp2DRendererIsTheOnlyActivePipeline",
    "Rpg.Tests.Editor.SoundwellInputSystemTests.InputActionsHaveSeparateContextsAndKeepBothBackendsEnabled",
}
URP2D_MARKER = "RPG_URP2D_VALIDATED"
SOUNDWELL_TESTS = {
    "Rpg.Tests.Editor.SoundwellTrialTests.PlayerCanRingWrapRetreatAndRetry",
    "Rpg.Tests.Editor.SoundwellOrkBattleTests.ThreePlayerCombatantsEnterOneOrkTurnBasedBattle",
}
SOUNDWELL_MARKERS = (
    "RPG_SOUNDWELL_SIGNAL_VALIDATED",
    "RPG_SOUNDWELL_ORK_BATTLE_VALIDATED",
)
SOURCE_PATHS = ("Assets", "Packages", "ProjectSettings")

# Import/updater notices can include "Unable to update following assemblies"
# even when the pinned DLL is intentionally excluded from API updating,
# both Unity processes exit 0, and all expected real Play Mode tests pass.
# Require an actual compiler/assembly load diagnostic here; action outcomes,
# exit codes, exact tests, markers, artifacts, and clean SHA remain mandatory.
UNITY_COMPILER_ERROR = re.compile(
    r"error CS\d+|Scripts have compiler errors|Failed to resolve assembly|"
    r"Assembly .+ will not be loaded", re.IGNORECASE
)


def git(*args):
    return subprocess.check_output(["git", *args])



def format_performance_summary(performance):
    """Summarize only complete, plausible Editor Play Mode measurements."""
    if not isinstance(performance, dict):
        return ""

    samples = performance.get("frame_samples")
    if type(samples) is not int or samples < 30:
        return ""

    timings = {}
    for key in ("frame_time_ms_p50", "frame_time_ms_p95", "frame_time_ms_max"):
        value = performance.get(key)
        if type(value) not in (int, float) or not math.isfinite(value) or value <= 0:
            return ""
        timings[key] = value
    if not (timings["frame_time_ms_p50"] <= timings["frame_time_ms_p95"] <= timings["frame_time_ms_max"]):
        return ""

    collections = {}
    for key in ("gc_gen0_collections_delta", "gc_gen1_collections_delta", "gc_gen2_collections_delta"):
        value = performance.get(key)
        if type(value) is not int or value < 0:
            return ""
        collections[key] = value

    heap = {}
    for key in ("managed_heap_start_bytes", "managed_heap_peak_bytes", "managed_heap_end_bytes"):
        value = performance.get(key)
        if type(value) is not int or value < 0:
            return ""
        heap[key] = value
    if heap["managed_heap_peak_bytes"] < max(heap["managed_heap_start_bytes"], heap["managed_heap_end_bytes"]):
        return ""

    environment = performance.get("environment", "Unity Editor Play Mode")
    if not isinstance(environment, str):
        return ""
    environment = " ".join(environment.split())[:160] or "Unity Editor Play Mode"

    def mib(key):
        return heap[key] / (1024.0 * 1024.0)

    return (
        "\n### Soundwell Play Mode performance sample (trend only)\n"
        f"- Environment: {environment}\n"
        f"- Frames sampled: {samples}\n"
        f"- Frame time: P50 {timings['frame_time_ms_p50']:.2f} ms; "
        f"P95 {timings['frame_time_ms_p95']:.2f} ms; "
        f"max {timings['frame_time_ms_max']:.2f} ms\n"
        f"- GC collection deltas: Gen0 +{collections['gc_gen0_collections_delta']}; "
        f"Gen1 +{collections['gc_gen1_collections_delta']}; "
        f"Gen2 +{collections['gc_gen2_collections_delta']}\n"
        f"- Managed heap: start {mib('managed_heap_start_bytes'):.2f} MiB; "
        f"peak {mib('managed_heap_peak_bytes'):.2f} MiB; "
        f"end {mib('managed_heap_end_bytes'):.2f} MiB\n"
        "- Compare only like-for-like Unity/editor runner samples; this is not a "
        "player-hardware benchmark or a pass/fail performance target.\n"
    )


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
    performance = None
    if soundwell:
        expected_tests.update(SOUNDWELL_TESTS)
        for filename in ("01-ringing.png", "02-wrapped-searching.png", "03-retreat-reset.png"):
            frame = evidence / "soundwell" / filename
            if not frame.is_file() or frame.stat().st_size < 1024:
                errors.append(f"Missing Soundwell frame: {filename}")

        # Independently validate the end-to-end Play Mode performance artifact.
        # Reject absent, zero, non-finite, or internally inconsistent measurements.
        path = evidence / "performance" / "soundwell-playmode.json"
        try:
            performance = json.loads(path.read_text())
            if performance.get("schema_version") != 1:
                errors.append("Soundwell performance schema is not version 1.")
            if performance.get("unity_version") != "6000.6.8f1":
                errors.append("Soundwell performance Unity version does not match the pinned editor.")
            if performance.get("sample_source") != "UnityEngine.Time.unscaledDeltaTime":
                errors.append("Soundwell performance source is not a Play Mode frame interval.")
            if performance.get("scenario") != "soundwell-ring-wrap-retreat-reload-reenter":
                errors.append("Soundwell performance scenario does not match the tested path.")

            samples = performance.get("frame_samples")
            if type(samples) is not int or samples < 30:
                errors.append("Soundwell performance must contain at least 30 real frame samples.")

            timings = []
            for key in ("frame_time_ms_p50", "frame_time_ms_p95", "frame_time_ms_max"):
                value = performance.get(key)
                if type(value) not in (int, float) or not math.isfinite(value) or value <= 0:
                    errors.append(f"Soundwell {key} must be positive and finite.")
                else:
                    timings.append(value)
            if len(timings) == 3 and not (timings[0] <= timings[1] <= timings[2]):
                errors.append("Soundwell frame time percentiles are inconsistent.")

            for key in ("gc_gen0_collections_delta", "gc_gen1_collections_delta", "gc_gen2_collections_delta",
                        "managed_heap_start_bytes", "managed_heap_end_bytes", "managed_heap_peak_bytes"):
                value = performance.get(key)
                if type(value) is not int or value < 0:
                    errors.append(f"Soundwell {key} is missing or negative.")
            heap = [performance.get(k) for k in
                    ("managed_heap_start_bytes", "managed_heap_end_bytes", "managed_heap_peak_bytes")]
            if all(type(v) is int and v >= 0 for v in heap) and heap[2] < max(heap[0], heap[1]):
                errors.append("Soundwell peak heap is below its starting or ending sample.")
        except (OSError, ValueError, TypeError, AttributeError) as error:
            errors.append(f"Missing or invalid Soundwell performance artifact: {error}")

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
    editmode_markers = ("RPG_BOOTSTRAP_PLAY_VALIDATED:first", "RPG_BOOTSTRAP_PLAY_VALIDATED:second", URP2D_MARKER)
    if soundwell:
        editmode_markers += SOUNDWELL_MARKERS
    for filename, markers in {
        "editmode.log": editmode_markers,
        "batch.log": ("RPG_BOOTSTRAP_VALIDATED",),
    }.items():
        try:
            log = (evidence / filename).read_text(errors="replace")
            errors.extend(f"Missing {marker} in {filename}." for marker in markers if marker not in log)
            import_error = UNITY_COMPILER_ERROR.search(log)
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
        "performance": performance if soundwell else None,
        "errors": errors,
        "note": "Preparation verifies SHA plus generated files; commit reviewed files and rerun verify for clean-SHA evidence.",
    }
    (evidence / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    summary = f"P0 evidence: {status} ({sha})\n" + "".join(f"- {error}\n" for error in errors)
    if performance is not None:
        summary += format_performance_summary(performance)
    print(summary, end="")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a") as stream:
            stream.write(summary)
    return bool(errors)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("prepare", "verify"))
    sys.exit(collect(parser.parse_args().mode))
