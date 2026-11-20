"""Exercise the cleanup entry point with in-memory Git refs and GitHub responses."""
import importlib
import io
import json
import os
from contextlib import redirect_stdout
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import Mock, call, patch
from urllib.parse import parse_qs, unquote, urlsplit

TOOLS = str(Path(__file__).resolve().parent)
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)

DEV = "a" * 40
BEFORE = "b" * 40
SHA = "c" * 40
OTHER = "d" * 40
NAME = "codex/pending/fixture"
REF = "refs/heads/" + NAME
DEV_REF = "refs/heads/codex/dev"
LOOKUP = ("ls-remote", "--heads", "origin", REF)
DELETE = ("push", "--force-with-lease=" + REF + ":" + SHA, "origin", "--delete", REF)


class PendingCleanupTests(unittest.TestCase):
    def setUp(self):
        # Block external transports even if a new code path forgets a test double.
        self.enterContext(patch("subprocess.run", side_effect=AssertionError("unexpected real Git command")))
        self.enterContext(patch("urllib.request.urlopen", side_effect=AssertionError("unexpected network request")))
        with patch.dict(os.environ, {}, clear=True):
            self.cleanup = importlib.import_module("close_merged_pending")

        root = Path(self.enterContext(tempfile.TemporaryDirectory()))
        self.summary = root / "summary"
        self.event = root / "event.json"
        self.event.write_text(json.dumps({
            "ref": DEV_REF, "before": BEFORE, "after": DEV,
            "forced": False, "created": False, "deleted": False,
        }))
        self.enterContext(patch.dict(os.environ, {
            "GITHUB_REPOSITORY": "jinze909/rpg", "GITHUB_REF": DEV_REF, "GH_TOKEN": "fixture",
            "GITHUB_EVENT_PATH": str(self.event), "GITHUB_STEP_SUMMARY": str(self.summary),
        }, clear=True))

        self.remote_sha = SHA
        self.branch = {"name": NAME, "protected": False, "commit": {"sha": SHA}}
        self.run_info = {
            "id": 37, "run_attempt": 2, "head_sha": SHA, "name": "Unity ORK P0",
            "status": "completed", "conclusion": "success", "html_url": "https://example.invalid/runs/37",
        }
        self.artifact = {
            "name": f"unity-ork-p0-verify-{SHA}-attempt-2", "size_in_bytes": 320, "expired": False,
        }
        self.dev_head = Mock(return_value=DEV)
        self.open_prs = Mock(return_value=[])
        self.current_branch = Mock(return_value=self.branch)
        self.branch_runs = Mock(return_value=[self.run_info])

        self.git = self.enterContext(patch.object(self.cleanup, "git", side_effect=self.git_command))
        self.enterContext(patch.object(self.cleanup, "api", side_effect=self.github_response))
        self.enterContext(patch.object(self.cleanup, "ancestor", side_effect=lambda a, b: a == b or b == DEV))

    def git_command(self, *args):
        if args == ("fetch", "--no-tags", "origin", "+refs/heads/*:refs/remotes/origin/*"):
            return ""
        if args == ("rev-parse", "refs/remotes/origin/codex/dev"):
            return DEV
        if args == ("rev-parse", "refs/remotes/origin/" + NAME):
            return SHA
        if args == LOOKUP:
            return f"{self.remote_sha}\t{REF}" if self.remote_sha else ""
        if args == DELETE:
            self.assertEqual(self.remote_sha, SHA, "leased deletion must match the live ref")
            self.remote_sha = None
            return ""
        if args == ("rev-list", "--parents", "-n", "1", SHA):
            return f"{SHA} {BEFORE}"
        if args == ("diff-tree", "--no-commit-id", "--name-only", "-r", SHA):
            return "Assets/Game/Fixture.cs"
        self.fail(f"unexpected git operation: {args}")

    def github_response(self, url):
        route = urlsplit(url)
        prefix = "/repos/jinze909/rpg/"
        self.assertTrue(route.path.startswith(prefix))
        path = unquote(route.path[len(prefix):])
        params = parse_qs(route.query)
        if path == "git/ref/heads/codex/dev":
            return {"object": {"sha": self.dev_head()}}
        if path == "pulls":
            self.assertEqual(params["state"], ["open"])
            return self.open_prs()
        if path == "branches":
            return [self.branch]
        if path == "branches/" + NAME:
            return self.current_branch()
        if path == "actions/runs":
            if "branch" in params:
                self.assertEqual(params["branch"], [NAME])
                return {"workflow_runs": self.branch_runs()}
            return {"workflow_runs": []}
        if path == "actions/runs/37/artifacts":
            return {"artifacts": [self.artifact]}
        self.fail("unexpected API route: " + url)

    def audit(self):
        self.summary.write_text("")
        with redirect_stdout(io.StringIO()):
            self.cleanup.main()
        return self.summary.read_text()

    def assert_retained(self, reason):
        summary = self.audit()
        self.assertIn("Retained " + NAME, summary)
        self.assertIn(reason, summary)
        self.assertFalse(any(args[0] == "push" for args, _ in self.git.call_args_list))

    def test_verified_ancestor_requires_exact_lease_and_confirms_disappearance(self):
        summary = self.audit()
        self.assertIn("Deleted " + NAME, summary)
        self.assertIn(self.run_info["html_url"], summary)
        self.assertEqual(self.git.call_args_list[-3:], [call(*LOOKUP), call(*DELETE), call(*LOOKUP)])
        self.assertIsNone(self.remote_sha)

    def test_open_pr_retains_branch(self):
        self.open_prs.return_value = [{"head": {"ref": NAME}, "base": {"ref": "codex/dev"}}]
        self.assert_retained("open PR")

    def test_new_pr_using_candidate_as_base_retains_branch(self):
        self.open_prs.side_effect = [[], [{"head": {"ref": "topic"}, "base": {"ref": NAME}}]]
        self.assert_retained("new open PR")

    def test_queued_ci_at_final_check_retains_branch(self):
        self.branch_runs.side_effect = [[self.run_info], [self.run_info, {"status": "queued"}]]
        self.assert_retained("active or queued CI")

    def test_dev_advanced_during_verification_retains_branch(self):
        self.dev_head.side_effect = [DEV, OTHER]
        self.assert_retained("dev moved")

    def test_remote_branch_changed_after_api_check_retains_branch(self):
        self.remote_sha = OTHER
        self.assert_retained("compare-and-swap precondition failed")

    def test_new_branch_protection_retains_branch(self):
        self.current_branch.return_value = {**self.branch, "protected": True}
        self.assert_retained("remote HEAD changed or protected")

    def test_same_run_must_still_have_current_verify_artifact_on_next_audit(self):
        self.assertIn("Deleted " + NAME, self.audit())
        # Restore the virtual branch at the same SHA for a separate event.
        self.remote_sha = SHA
        self.git.reset_mock()
        self.artifact["expired"] = True
        self.assert_retained("no validated ancestor or exact snapshot replacement")

    def test_evidence_for_another_sha_or_attempt_retains_branch(self):
        for sha, attempt in ((OTHER, 2), (SHA, 1)):
            with self.subTest(sha=sha, attempt=attempt):
                self.artifact["name"] = f"unity-ork-p0-verify-{sha}-attempt-{attempt}"
                self.assert_retained("no validated ancestor or exact snapshot replacement")

    def test_forced_event_stops_before_fetch(self):
        event = json.loads(self.event.read_text())
        self.event.write_text(json.dumps({**event, "forced": True}))
        with self.assertRaisesRegex(RuntimeError, "not an ordinary dev update"):
            self.audit()
        self.git.assert_not_called()


if __name__ == "__main__":
    unittest.main()
