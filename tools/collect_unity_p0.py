#!/usr/bin/env python3
"""Collect P0 evidence; preparation is never clean-commit acceptance."""
import argparse
from collections import Counter
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
}
URP2D_MARKER = "RPG_URP2D_VALIDATED"
SOUNDWELL_TESTS = {
    "Rpg.Tests.Editor.SoundwellTrialTests.PlayerCanRingWrapRetreatAndRetry",
    "Rpg.Tests.Editor.SoundwellOrkBattleTests.ThreePlayerCombatantsEnterOneOrkTurnBasedBattle",
}
INPUT_SYSTEM_TESTS = {
    "Rpg.Tests.Editor.SoundwellInputSystemTests.InputActionsHaveSeparateContextsAndKeepBothBackendsEnabled",
    "Rpg.Tests.Editor.SoundwellInputSystemTests.EntryAndGameplayContextsRouteLiveKeyboardAndGamepadEvents",
}
ORK_UI_TESTS = {
    "Rpg.Tests.Editor.MakinomUiIntegrationSurfaceTests.UnityUiModuleExposesItsSystemSettingsAndCreationCommands",
}
ORK_UI_MARKER = "RPG_ORK_UI_SURFACE_VALIDATED"
# Additional end-to-end tests are introduced in separate Story 003 candidates.
# Require every test actually declared in the clean candidate; never accept
# partial/skipped results or silently disregard a new UnityTest.
ADDITIONAL_SOUNDWELL_TESTS = {
    "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs": {
        "LeaderActionAndHitFeedbackExpireDuringPausedOrkClock":
            "Rpg.Tests.Editor.SoundwellCompanionActionTests.LeaderActionAndHitFeedbackExpireDuringPausedOrkClock",
        "OrkAcceptedCompanionAttackChangesOnlyItsPixelPose":
            "Rpg.Tests.Editor.SoundwellCompanionActionTests.OrkAcceptedCompanionAttackChangesOnlyItsPixelPose",
    },
    "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs": {
        "BattleStepChangesGateAssignmentFromSameOpening":
            "Rpg.Tests.Editor.SoundwellOrkBattleTests.BattleStepChangesGateAssignmentFromSameOpening",
        "BattleBackCancelEventRetreatsAndReleasesOrkParticipants":
            "Rpg.Tests.Editor.SoundwellOrkBattleTests.BattleBackCancelEventRetreatsAndReleasesOrkParticipants",
    },
    "Assets/Game/Tests/Editor/SoundwellDirectVictoryEscortTests.cs": {
        "OrkVictoryDoesNotBypassSurvivorEscortOrSafeArrival":
            "Rpg.Tests.Editor.SoundwellDirectVictoryEscortTests.OrkVictoryDoesNotBypassSurvivorEscortOrSafeArrival",
    },
    "Assets/Game/Tests/Editor/SoundwellTrialTests.cs": {
        "BeastKnockbackKeepsCarriedBellWithRescuerOnContactFrame":
            "Rpg.Tests.Editor.SoundwellTrialTests.BeastKnockbackKeepsCarriedBellWithRescuerOnContactFrame",
        "BellOnlyRescueWorksWithoutOptionalCluesOrEscortRope":
            "Rpg.Tests.Editor.SoundwellTrialTests.BellOnlyRescueWorksWithoutOptionalCluesOrEscortRope",
        "PausedEscortRetryClearsWaitingVisualBeforeNextFrame":
            "Rpg.Tests.Editor.SoundwellTrialTests.PausedEscortRetryClearsWaitingVisualBeforeNextFrame",
        "ReturnScreenKeepsRescueOutcomeLocalizedAcrossLanguageChanges":
            "Rpg.Tests.Editor.SoundwellTrialTests.ReturnScreenKeepsRescueOutcomeLocalizedAcrossLanguageChanges",
        "PlayerLanguageChoiceSurvivesFreshPlayAndUpdatesEntryUi":
            "Rpg.Tests.Editor.SoundwellTrialTests.PlayerLanguageChoiceSurvivesFreshPlayAndUpdatesEntryUi",
        "AcceptedOrkLeaderAttackShowsActualPlayerActionClip":
            "Rpg.Tests.Editor.SoundwellTrialTests.AcceptedOrkLeaderAttackShowsActualPlayerActionClip",
    },
    "Assets/Game/Tests/Editor/P0BootstrapTests.cs": {
        "SoundwellAudioMuteSurvivesSceneRerouting":
            "Rpg.Tests.Editor.P0BootstrapTests.SoundwellAudioMuteSurvivesSceneRerouting",
    },
    "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs": {
        "GamepadSubmitOnFocusedEntrySfxButtonDoesNotStartTheTrial":
            "Rpg.Tests.Editor.SoundwellInputSystemTests.GamepadSubmitOnFocusedEntrySfxButtonDoesNotStartTheTrial",
    },
    "Assets/Game/Tests/Editor/SoundwellThreatPreviewTests.cs": {
        "SideBellLureDisplaysRealPursuitAndExpiresAfterWrapping":
            "Rpg.Tests.Editor.SoundwellThreatPreviewTests.SideBellLureDisplaysRealPursuitAndExpiresAfterWrapping",
    },
}
# Standalone editor [Test] methods must be included in the exact test
# manifest too. The UnityTest registry below only recognizes IEnumerator,
# so new NUnit void tests otherwise compile and pass but fail evidence.
ADDITIONAL_EDITOR_TESTS = {
    "Assets/Game/Tests/Editor/SoundwellTrialTests.cs": {
        "BattleChooserActorLookupIgnoresDestroyedOrMissingActors":
            "Rpg.Tests.Editor.SoundwellTrialTests.BattleChooserActorLookupIgnoresDestroyedOrMissingActors",
        "RetiredOrkGateActorCannotOpenPhysicalGate":
            "Rpg.Tests.Editor.SoundwellTrialTests.RetiredOrkGateActorCannotOpenPhysicalGate",
    },
    "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs": {
        "EveryPlayableSoundwellLabelAndRewardHasMatchingEnAndChineseCopy":
            "Rpg.Tests.Editor.SoundwellLocalizedRescueCopyTests.EveryPlayableSoundwellLabelAndRewardHasMatchingEnAndChineseCopy",
    },
    "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs": {
        "OrkStepMustKeepWholeCombatantInsideSavedBattleBounds":
            "Rpg.Tests.Editor.SoundwellCompanionActionTests.OrkStepMustKeepWholeCombatantInsideSavedBattleBounds",
        "OrkBattleReadoutMustNotMislabelAnActiveNonAttackChooserAsPending":
            "Rpg.Tests.Editor.SoundwellCompanionActionTests.OrkBattleReadoutMustNotMislabelAnActiveNonAttackChooserAsPending",
        "LowerPlankExplorationKnockbackMustNotOverrideAnOrkStep":
            "Rpg.Tests.Editor.SoundwellCompanionActionTests.LowerPlankExplorationKnockbackMustNotOverrideAnOrkStep",
    },
    "Assets/Game/Tests/Editor/P0BootstrapTests.cs": {
        "InvalidSavedBindingOverridesRecoverDefaultInputWithoutLosingValidRemaps":
            "Rpg.Tests.Editor.P0BootstrapTests.InvalidSavedBindingOverridesRecoverDefaultInputWithoutLosingValidRemaps",
        "FailedAudioMixerMuteMustPreserveTheSavedPlayerChoice":
            "Rpg.Tests.Editor.P0BootstrapTests.FailedAudioMixerMuteMustPreserveTheSavedPlayerChoice",
    },
}

SOUNDWELL_MARKERS = (
    "RPG_SOUNDWELL_SIGNAL_VALIDATED",
    "RPG_SOUNDWELL_LOCALIZATION_PLAY_VALIDATED",
    "RPG_SOUNDWELL_ORK_BATTLE_VALIDATED",
    "RPG_SOUNDWELL_DIRECT_RESCUE_VALIDATED",
    "RPG_SOUNDWELL_SPRITE_ATLAS_VALIDATED",
    "RPG_SOUNDWELL_PIXEL_PERFECT_VALIDATED",
)
SOURCE_PATHS = ("Assets", "Packages", "ProjectSettings")


def expected_tests_for(mode, has_soundwell_scene, source_root=Path(".")):
    """Match the actual candidate's declared UnityTest cases, not a stale fixed count."""
    expected = set(EXPECTED_TESTS)
    for relative_path, declared_tests in ADDITIONAL_EDITOR_TESTS.items():
        path = source_root / relative_path
        if not path.is_file():
            continue
        content = path.read_text(encoding="utf-8")
        for method_name, test_name in declared_tests.items():
            signature = (
                r"\[Test(?:,\s*[^\]]+)?\]\s*"
                r"(?:\[[^\]]+\]\s*)*"
                r"public\s+void\s+" + re.escape(method_name) + r"\s*\("
            )
            if re.search(signature, content):
                expected.add(test_name)
    if mode == "verify" and has_soundwell_scene:
        expected.update(SOUNDWELL_TESTS)
        expected.update(INPUT_SYSTEM_TESTS)
        expected.update(ORK_UI_TESTS)
        for relative_path, declared_tests in ADDITIONAL_SOUNDWELL_TESTS.items():
            path = source_root / relative_path
            if not path.is_file():
                continue
            content = path.read_text(encoding="utf-8")
            for method_name, test_name in declared_tests.items():
                signature = (
                    r"\[UnityTest(?:,\s*[^\]]+)?\]\s*"
                    r"(?:\[[^\]]+\]\s*)*"
                    r"public\s+IEnumerator\s+" + re.escape(method_name) + r"\s*\("
                )
                if re.search(signature, content):
                    expected.add(test_name)
    return expected


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


MEMORY_PHASES = (
    "bootstrap_before_entry", "soundwell_entered", "rescue_completed",
    "soundwell_reloaded", "bootstrap_after_return",
)
MEMORY_COUNTER_KEYS = (
    "managed_gc_heap_bytes", "unity_managed_used_bytes",
    "unity_managed_reserved_bytes", "unity_total_allocated_bytes",
    "unity_total_reserved_bytes", "unity_unused_reserved_bytes",
    "loaded_game_objects", "loaded_textures", "loaded_audio_clips",
)


def validate_memory_phases(report):
    """Reject synthetic/empty/incomplete memory evidence, not natural growth."""
    errors = []
    if not isinstance(report, dict):
        return ["M10 memory report must be a JSON object."]
    if report.get("schema_version") != 1:
        errors.append("M10 memory schema version must be 1.")
    if report.get("unity_version") != "6000.6.8f1":
        errors.append("M10 memory evidence must come from pinned Unity.")
    if report.get("scenario") != "soundwell-enter-rescue-reload-return" or (
        report.get("sample_source") != "UnityEngine.Profiling.Profiler + System.GC"
    ):
        errors.append("M10 memory evidence must use the real tested scenario and Profiler counters.")
    if "not player hardware" not in str(report.get("environment", "")):
        errors.append("M10 must identify the Linux Editor container, not player hardware.")
    samples = report.get("snapshots")
    if not isinstance(samples, list) or len(samples) != len(MEMORY_PHASES):
        return errors + ["M10 requires five real story-phase memory measurements."]
    expected_scenes = ("Bootstrap", "SoundwellTrial", "SoundwellTrial",
                       "SoundwellTrial", "Bootstrap")
    previous_frame = -1
    for index, (sample, phase, scene) in enumerate(zip(samples, MEMORY_PHASES, expected_scenes)):
        if not isinstance(sample, dict) or sample.get("phase") != phase or sample.get("scene") != scene:
            errors.append(f"M10 sample {index} must be the real {phase} scene.")
            continue
        frame = sample.get("unity_frame")
        if type(frame) is not int or frame < 0:
            errors.append(f"M10 sample {phase} lacks a valid Unity frame.")
        elif frame <= previous_frame:
            errors.append(f"M10 sample {phase} must advance the Unity frame since the previous scene phase.")
        else:
            previous_frame = frame
        for key in MEMORY_COUNTER_KEYS:
            value = sample.get(key)
            if type(value) is not int or value < 0 or (
                key in ("managed_gc_heap_bytes", "loaded_game_objects") and value == 0
            ):
                errors.append(f"M10 sample {phase} has invalid {key}.")
    for key in ("bootstrap_managed_gc_heap_delta_bytes",
                "bootstrap_unity_total_allocated_delta_bytes"):
        if type(report.get(key)) is not int:
            errors.append(f"M10 report missing signed delta: {key}.")
    if not errors:
        before, after = samples[0], samples[-1]
        if report["bootstrap_managed_gc_heap_delta_bytes"] != (
            after["managed_gc_heap_bytes"] - before["managed_gc_heap_bytes"]
        ) or report["bootstrap_unity_total_allocated_delta_bytes"] != (
            after["unity_total_allocated_bytes"] - before["unity_total_allocated_bytes"]
        ):
            errors.append("M10 bootstrap delta does not reconcile with collected samples.")
    return errors


BUILD_SCENES = (
    "Assets/Game/Scenes/Bootstrap.unity", "Assets/Game/Scenes/SoundwellTrial.unity",
)


def validate_build_profile_player(report):
    """Only a real Linux Player build is evidence; an .asset alone is not."""
    if not isinstance(report, dict):
        return ["M11 Player build report must be a JSON object."]
    errors = []
    expected = {
        "schema_version": 1, "unity_version": "6000.6.8f1",
        "platform_guid": "cb423bfea44b4d658edb8bc5d91a3024",
        "target": "StandaloneLinux64",
        "test_profile": "Assets/Settings/Build Profiles/Soundwell Test Linux.asset",
        "result": "Succeeded", "development_build": True,
        "total_errors": 0, "data_directory_present": True,
    }
    for k, v in expected.items():
        if report.get(k) != v or type(report.get(k)) != type(v):
            errors.append(f"M11 invalid verified build field: {k}.")
    if report.get("scenes") != list(BUILD_SCENES):
        errors.append("M11 build used a scene list different from global ORK scenes.")
    if report.get("scripting_backend") not in ("Mono2x", "IL2CPP"):
        errors.append("M11 has no verified Standalone scripting backend.")
    for k in ("executable_bytes", "build_report_bytes"):
        if type(report.get(k)) is not int or report[k] <= 1024:
            errors.append("M11 requires a nonempty actual player artifact: " + k)
    if "not a user device" not in str(report.get("environment", "")):
        errors.append("M11 evidence must identify CI and not a user device.")
    return errors


def validate_soundwell_route_starts(log):
    snapshots = {}
    for route in ("BELL", "COMBAT"):
        matches = re.findall(rf"^RPG_SOUNDWELL_ROUTE_START:{route}:(.+)$", log, re.MULTILINE)
        if len(matches) != 1:
            return ["Expected one fresh Soundwell route snapshot for " + route]
        snapshots[route] = matches[0].strip()
    if snapshots["BELL"] != snapshots["COMBAT"]:
        return ["Soundwell routes must start from the same positions, HP, and rescue flags."]
    return []


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

    has_soundwell_scene = (root / "Assets/Game/Scenes/SoundwellTrial.unity").is_file()
    soundwell = mode == "verify" and has_soundwell_scene
    expected_tests = expected_tests_for(mode, has_soundwell_scene)
    performance = None
    memory_phases = None
    test_player = None
    if soundwell:
        try:
            report_path = evidence / "builds" / "soundwell-test-linux.json"
            test_player = json.loads(report_path.read_text())
            errors.extend(validate_build_profile_player(test_player))
        except (OSError, ValueError, TypeError, AttributeError) as error:
            errors.append(f"Missing or invalid M11 Linux Player build evidence: {error}")
        for filename in ("01-ringing.png", "02-wrapped-searching.png",
                         "03-retreat-reset.png", "14-localized-zh-Hans.png"):
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

        # M10 always derives actual before/after samples from the same CI
        # Soundwell story, with Unity native/managed metrics and asset counts.
        try:
            memory_path = evidence / "performance" / "soundwell-memory-phases.json"
            memory_phases = json.loads(memory_path.read_text())
            errors.extend(validate_memory_phases(memory_phases))
        except (OSError, ValueError, TypeError, AttributeError) as error:
            errors.append(f"Missing or invalid M10 memory evidence: {error}")

    try:
        result = ET.parse(evidence / "editmode-results.xml").getroot()
        cases = list(result.iter("test-case"))
        names = Counter(case.get("fullname") or "<unnamed test>" for case in cases)
        missing = sorted(expected_tests.difference(names))
        unexpected = sorted(names.keys() - expected_tests)
        duplicates = sorted(name for name, count in names.items() if count > 1)
        for label, differences in (
            ("Missing", missing), ("Unexpected", unexpected), ("Duplicate", duplicates)
        ):
            if differences:
                errors.append(f"{label} Unity tests: " + ", ".join(differences))
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
    editmode_markers = ("RPG_BOOTSTRAP_PLAY_VALIDATED:first", "RPG_BOOTSTRAP_PLAY_VALIDATED:second",
                        URP2D_MARKER, "RPG_SOUNDWELL_TILEMAP_VALIDATED",
                        "RPG_SOUNDWELL_AUDIO_MIXER_VALIDATED",
                        "RPG_SOUNDWELL_LOCALIZATION_VALIDATED")
    if soundwell:
        editmode_markers += SOUNDWELL_MARKERS + (ORK_UI_MARKER,)
    if "Rpg.Tests.Editor.SoundwellTrialTests.BellOnlyRescueWorksWithoutOptionalCluesOrEscortRope" in expected_tests:
        editmode_markers += ("RPG_SOUNDWELL_BELL_ONLY_RESCUE_VALIDATED",)
    for filename, markers in {
        "editmode.log": editmode_markers,
        "batch.log": ("RPG_BOOTSTRAP_VALIDATED",),
    }.items():
        try:
            log = (evidence / filename).read_text(errors="replace")
            errors.extend(f"Missing {marker} in {filename}." for marker in markers if marker not in log)
            if filename == "editmode.log" and soundwell:
                errors.extend(validate_soundwell_route_starts(log))
                for cost in ("BELL:world=4:turn=0:escort=3", "COMBAT:world=1:turn=3:escort=3"):
                    if "RPG_SOUNDWELL_ROUTE_COST:" + cost not in log:
                        errors.append("Missing verified Soundwell route cost: " + cost)
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
        "memory_phases": memory_phases if soundwell else None,
        "test_player": test_player if soundwell else None,
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
