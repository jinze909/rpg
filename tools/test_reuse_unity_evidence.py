"""Regression tests for exact-SHA reuse, provenance, expiry and fail-open-to-Unity."""
import datetime as dt
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import urllib.error
import zipfile

import reuse_unity_evidence as reuse


NOW = dt.datetime(2026, 11, 11, tzinfo=dt.timezone.utc)
TEST_SHA = "a" * 40
RUN_ID = 123


def context():
    return {
        "schema": 1, "repository": reuse.REPO, "repository_id": "42",
        "source_sha": TEST_SHA, "mode": "verify", "license_method": "personal",
        "workflows": {reuse.WORKFLOW: "workflow-digest"}, "unity": "6000.6.8f1",
        "expected_tests": ["Rpg.Tests.One", "Rpg.Tests.Two"],
    }


def run():
    return {
        "id": RUN_ID, "event": "push", "path": reuse.WORKFLOW,
        "head_sha": TEST_SHA, "head_branch": "codex/pending/example",
        "repository": {"full_name": reuse.REPO, "id": 42},
        "head_repository": {"full_name": reuse.REPO, "id": 42},
        "run_attempt": 1, "status": "completed", "conclusion": "success",
        "updated_at": "2026-11-10T23:00:00Z",
    }


def artifact(name, identity):
    return {
        "id": identity, "name": name, "expired": False, "size_in_bytes": 1000,
        "expires_at": "2026-11-24T00:00:00Z",
        "workflow_run": {"id": RUN_ID, "head_sha": TEST_SHA},
    }


class FakeGitHub:
    def __init__(self):
        self.runs = [run()]
        self.artifacts = [artifact("unity-reuse-context-1", 1),
            artifact(f"unity-ork-p0-verify-{TEST_SHA}-attempt-1", 2)]
        self.context = context()
        self.report = {
            "mode": "verify", "status": "passed-clean-candidate", "source_sha": TEST_SHA,
            "candidate_sha": TEST_SHA, "unity": "6000.6.8f1", "unity_action_outcome": "success",
            "errors": [], "changed_files_sha256": {},
            "run_url": f"https://github.com/{reuse.REPO}/actions/runs/{RUN_ID}",
        }
        self.xml = ('<test-run result="Passed"><test-case fullname="Rpg.Tests.One" result="Passed"/>'
                    '<test-case fullname="Rpg.Tests.Two" result="Passed"/></test-run>')
        self.calls = []

    def json(self, path):
        self.calls.append(path)
        if "/artifacts?" in path:
            return {"total_count": len(self.artifacts), "artifacts": self.artifacts}
        return {"total_count": len(self.runs), "workflow_runs": self.runs}

    def members(self, artifact, names):
        self.calls.append(names)
        if artifact["id"] == 1:
            return {"unity-reuse-context.json": json.dumps(self.context).encode()}
        return {"validation.json": json.dumps(self.report).encode(), "editmode-results.xml": self.xml.encode()}


class EvidenceReuseTests(unittest.TestCase):
    def setUp(self):
        self.api = FakeGitHub()

    def result(self):
        return reuse.find_evidence(self.api, context(), NOW)

    def test_reuses_matching_final_sha_receipt_report_and_actual_tests(self):
        self.assertEqual(self.result(), f"https://github.com/{reuse.REPO}/actions/runs/{RUN_ID}")
        self.assertIn(("validation.json", "editmode-results.xml"), self.api.calls)

    def test_rejects_wrong_candidate_sha_prepare_dirty_or_failed_report(self):
        for key, value in (
            ("candidate_sha", "b" * 40), ("source_sha", "b" * 40),
            ("mode", "prepare"), ("status", "prepared-candidate"),
            ("errors", ["Compilation failed"]), ("changed_files_sha256", {"scene": "digest"}),
            ("unity_action_outcome", "failure"), ("unity", "6000.5.0f1"),
            ("run_url", "https://github.com/another/project/actions/runs/123"),
        ):
            with self.subTest(key=key):
                self.api = FakeGitHub()
                self.api.report[key] = value
                self.assertIsNone(self.result())

    def test_rejects_untrusted_or_different_source_run(self):
        for key, value in (
            ("head_sha", "b" * 40), ("event", "pull_request"),
            ("head_branch", "codex/dev"), ("path", ".github/workflows/other.yml"),
            ("repository", {"full_name": "other/repo"}),
            ("head_repository", {"full_name": reuse.REPO, "id": 99}),
        ):
            with self.subTest(key=key):
                self.api = FakeGitHub()
                self.api.runs[0][key] = value
                self.assertIsNone(self.result())

    def test_rejects_expired_missing_or_wrong_attempt_artifacts(self):
        for key, value in (("expired", True), ("expires_at", "2026-11-10T23:59:59Z"),
                           ("name", "unity-reuse-context-2"), ("size_in_bytes", 0),
                           ("workflow_run", {"id": 999, "head_sha": TEST_SHA})):
            with self.subTest(key=key):
                self.api = FakeGitHub()
                self.api.artifacts[0][key] = value
                self.assertIsNone(self.result())
        self.api = FakeGitHub()
        self.api.artifacts.pop(0)  # Old passing runs without receipts do not qualify.
        self.assertIsNone(self.result())

    def test_rejects_changed_workflow_environment_or_test_scope(self):
        for key, value in (("license_method", "file"), ("workflows", {reuse.WORKFLOW: "other"}),
                           ("expected_tests", ["Rpg.Tests.One"]), ("mode", "prepare")):
            with self.subTest(key=key):
                self.api = FakeGitHub()
                self.api.context[key] = value
                self.assertIsNone(self.result())

    def test_rejects_missing_skipped_duplicate_or_unexpected_tests(self):
        for xml in (
            '<test-run result="Passed"/>',
            self.api.xml.replace('result="Passed"/>', 'result="Skipped"/>', 1),
            self.api.xml.replace("Rpg.Tests.Two", "Rpg.Tests.One"),
            self.api.xml.replace("Rpg.Tests.Two", "Rpg.Tests.New"),
        ):
            with self.subTest(xml=xml):
                self.api.xml = xml
                self.assertIsNone(self.result())

    def test_newer_failed_or_active_run_prevents_reusing_old_green(self):
        for status, conclusion in (("completed", "failure"), ("in_progress", None)):
            with self.subTest(status=status):
                newer = run()
                newer.update(id=124, updated_at="2026-11-10T23:59:00Z", status=status, conclusion=conclusion)
                self.api.runs = [run(), newer]
                self.assertIsNone(self.result())

    def test_newer_manual_or_dev_failure_and_active_runs_block_old_pending_green(self):
        for event, branch in (("workflow_dispatch", "codex/pending/example"),
                              ("workflow_dispatch", "codex/dev"), ("push", "codex/dev")):
            for status, conclusion in (("completed", "failure"), ("in_progress", None),
                                       ("queued", None)):
                with self.subTest(event=event, branch=branch, status=status):
                    newer = run()
                    newer.update(id=124, event=event, head_branch=branch, status=status,
                                 conclusion=conclusion, updated_at="2026-11-10T23:59:00Z")
                    self.api.runs = [run(), newer]
                    self.assertIsNone(self.result())

    def test_current_dev_run_is_excluded_by_id_even_on_rerun(self):
        current = run()
        current.update(id=124, head_branch="codex/dev", status="in_progress",
                       conclusion=None, run_attempt=2, updated_at="2026-11-10T23:59:00Z")
        self.api.runs = [current, run()]
        self.assertEqual(reuse.find_evidence(self.api, context(), NOW, current_run_id=124),
                         f"https://github.com/{reuse.REPO}/actions/runs/{RUN_ID}")

    def test_failed_source_rerun_cannot_reuse_its_prior_success_artifacts(self):
        self.api.runs[0].update(run_attempt=2, conclusion="failure")
        self.assertIsNone(self.result())

    def test_successful_source_rerun_requires_latest_attempt_artifacts(self):
        self.api.runs[0].update(run_attempt=2)
        self.assertIsNone(self.result())  # Attempt 1 artifacts are still present.
        for item in self.api.artifacts:
            item["name"] = item["name"].rsplit("-", 1)[0] + "-2"
        self.assertEqual(self.result(), f"https://github.com/{reuse.REPO}/actions/runs/{RUN_ID}")

    def test_failure_at_same_timestamp_is_conservatively_checked_before_success(self):
        failed = run()
        failed.update(id=124, event="workflow_dispatch", conclusion="failure")
        self.api.runs = [run(), failed]
        self.assertIsNone(self.result())

    def test_old_failure_does_not_invalidate_newer_pending_success(self):
        old = run()
        old.update(id=124, event="workflow_dispatch", conclusion="failure",
                   updated_at="2026-11-10T22:00:00Z")
        self.api.runs = [old, run()]
        self.assertEqual(self.result(), f"https://github.com/{reuse.REPO}/actions/runs/{RUN_ID}")

    def test_no_matching_runs_falls_back(self):
        self.api.runs = []
        self.assertIsNone(self.result())


class ProbeTests(unittest.TestCase):
    def test_execution_receipt_tracks_real_workflow_bytes_and_license_mode(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name, content in {
                "production/ci/p0-mode.txt": "verify\n",
                reuse.WORKFLOW: "caller workflow",
                ".github/workflows/unity-p0-editor.yml": "pinned environment",
                "ProjectSettings/ProjectVersion.txt": "m_EditorVersion: 6000.6.8f1\n",
            }.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content)
            env = {"GITHUB_SHA": TEST_SHA, "GITHUB_REPOSITORY": reuse.REPO,
                   "GITHUB_REPOSITORY_ID": "42", "LICENSE_METHOD": "personal"}
            with patch.object(reuse.subprocess, "check_output", return_value=TEST_SHA):
                original = reuse.context_for(root, env)
                (root / ".github/workflows/unity-p0-editor.yml").write_text("different image")
                changed = reuse.context_for(root, env)
                self.assertNotEqual(original["workflows"], changed["workflows"])
                env["LICENSE_METHOD"] = "file"
                self.assertNotEqual(original["license_method"], reuse.context_for(root, env)["license_method"])
            self.assertEqual(original["mode"], "verify")
            self.assertTrue(original["expected_tests"])

    def test_only_dev_push_verify_is_eligible_and_other_events_still_record_context(self):
        with tempfile.TemporaryDirectory() as directory:
            for event, ref, mode, eligible in (
                ("push", "refs/heads/codex/dev", "verify", True),
                ("push", "refs/heads/codex/dev", "prepare", False),
                ("workflow_dispatch", "refs/heads/codex/dev", "verify", False),
                ("push", "refs/heads/codex/pending/example", "verify", False),
            ):
                with self.subTest(event=event, ref=ref, mode=mode):
                    env = {"RUNNER_TEMP": directory, "GITHUB_EVENT_NAME": event, "GITHUB_REF": ref,
                           "GITHUB_RUN_ID": "456"}
                    value = context()
                    value["mode"] = mode
                    with patch.object(reuse, "context_for", return_value=value), \
                         patch.object(reuse, "find_evidence", return_value="proof") as lookup:
                        result = reuse.probe(Path(directory), env, FakeGitHub(), NOW)
                    self.assertEqual(result, "proof" if eligible else None)
                    self.assertEqual(lookup.called, eligible)
                    if eligible:
                        self.assertEqual(lookup.call_args.kwargs, {"current_run_id": 456})
                    self.assertEqual(json.loads((Path(directory) / "unity-reuse-context.json").read_text()), value)

    def test_network_corruption_and_context_errors_emit_false_without_failing_unity(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "outputs"
            env = {"GITHUB_OUTPUT": str(output)}
            for error in (OSError("network"), ValueError("bad JSON"), KeyError("missing")):
                output.write_text("")
                with patch.object(reuse, "probe", side_effect=error), patch("sys.stdout", new=io.StringIO()):
                    self.assertEqual(reuse.main(env=env, api=FakeGitHub(), root=Path(directory)), 0)
                self.assertEqual(output.read_text(), "reused=false\n")

    def test_checkout_sha_must_match_before_recording_context(self):
        env = {"GITHUB_SHA": TEST_SHA, "GITHUB_REPOSITORY": reuse.REPO}
        with patch.object(reuse.subprocess, "check_output", return_value="b" * 40):
            with self.assertRaisesRegex(ValueError, "Checkout differs"):
                reuse.context_for(Path("."), env)


class ArtifactTransportTests(unittest.TestCase):
    def test_signed_storage_download_does_not_receive_github_token(self):
        data = io.BytesIO()
        with zipfile.ZipFile(data, "w") as archive:
            archive.writestr("validation.json", '{"status":"passed-clean-candidate"}')
        api = reuse.GitHub("test-only-token")
        redirect = urllib.error.HTTPError("https://api.github.com/artifact", 302, "redirect",
            {"Location": "https://storage.example/limited-download?signature=example"}, None)
        with patch.object(api.opener, "open", side_effect=redirect), \
             patch.object(reuse.urllib.request, "urlopen", return_value=io.BytesIO(data.getvalue())) as download:
            result = api.members({"id": 2}, ("validation.json",))
        self.assertNotIn("Authorization", dict(download.call_args.args[0].header_items()))
        self.assertEqual(json.loads(result["validation.json"])["status"], "passed-clean-candidate")

    def test_duplicate_json_members_are_rejected(self):
        data = io.BytesIO()
        with zipfile.ZipFile(data, "w") as archive:
            archive.writestr("validation.json", "{}")
            with self.assertWarns(UserWarning):
                archive.writestr("validation.json", "{}")
        api = reuse.GitHub("test-only-token")
        with patch.object(api.opener, "open", return_value=io.BytesIO(data.getvalue())):
            with self.assertRaises(ValueError):
                api.members({"id": 2}, ("validation.json",))


if __name__ == "__main__":
    unittest.main()
