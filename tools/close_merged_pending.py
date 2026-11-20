"""Retire delivered pending branches for an ordinary codex/dev push."""

import json
import os
import re
import subprocess
import time
import urllib.parse
import urllib.request

if __package__:
    from .pending_cleanup_history import ARCHIVED
else:
    from pending_cleanup_history import ARCHIVED

REPO = "jinze909/rpg"
REF = "refs/heads/codex/dev"
SHA_RE = re.compile(r"^[0-9a-f]{40}$")
PENDING = re.compile(r"^codex/pending/[A-Za-z0-9._/-]+$")


def check(ok, reason):
    if not ok:
        raise RuntimeError("SAFETY STOP: " + reason)


def git(*args):
    proc = subprocess.run(["git", *args], capture_output=True, text=True)
    check(proc.returncode == 0, "git " + args[0] + ": " + proc.stderr[-240:])
    return proc.stdout.strip()


def ancestor(a, b):
    p = subprocess.run(["git", "merge-base", "--is-ancestor", a, b], capture_output=True, text=True)
    check(p.returncode in (0, 1), "unknown ancestry: " + p.stderr[-240:])
    return p.returncode == 0


def have_commit(sha):
    # Actions retains metadata for SHAs on long-deleted branches.
    # A fetch of live refs correctly lacks those unreachable objects.
    # They cannot be ancestors of the fetched dev history.
    p = subprocess.run(["git", "cat-file", "-e", sha + "^{commit}"], capture_output=True)
    return p.returncode == 0


def api(path):
    check(path.startswith("/repos/" + REPO + "/"), "invalid API route")
    headers = {
        "Authorization": "Bearer " + os.environ["GH_TOKEN"],
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    q = urllib.request.Request("https://api.github.com" + path, headers=headers)
    with urllib.request.urlopen(q, timeout=30) as response:
        return json.load(response)


def pages(path, key=None, limit=500, allow_prefix=False):
    items = []
    for n in range(1, limit // 100 + 2):
        sep = "&" if "?" in path else "?"
        response = api(path + sep + "per_page=100&page=" + str(n))
        batch = response[key] if key else response
        check(isinstance(batch, list), "invalid API page")
        items += batch
        if allow_prefix and len(items) >= limit:
            return items[:limit]
        check(len(items) <= limit, "API page limit reached")
        if len(batch) < 100:
            return items
    raise RuntimeError("API page limit exceeded")


def runs_for(name):
    runs = pages(
        "/repos/" + REPO + "/actions/runs?branch=" + urllib.parse.quote(name, safe=""),
        "workflow_runs",
    )
    check(all(r.get("status") == "completed" for r in runs), "active or queued CI")
    return runs


# The same-SHA artifact remains the acceptance criterion. For branch
# retirement ONLY, a verified descendant may certify a failed/old
# ancestor, and an exactly matching verified file snapshot may prove
# that a divergent candidate was incorporated by a different SHA.
class Proofs:
    """Resolve exact-SHA evidence against one fetched development history."""

    def __init__(self, dev):
        self.dev = dev
        self.snapshot_deadline = time.monotonic() + 540
        self.artifacts = {}
        self.ancestors = None

    def verified_artifact(self, run, sha, runs):
        if not (
            SHA_RE.fullmatch(sha)
            and run.get("head_sha") == sha
            and run.get("name") == "Unity ORK P0"
            and run.get("status") == "completed"
            and run.get("conclusion") == "success"
        ):
            return None
        if any(
            x.get("head_sha") == sha
            and x.get("name") == "Unity ORK P0"
            and x.get("created_at", "") > run.get("created_at", "")
            and x.get("conclusion") != "success"
            for x in runs
        ):
            return None
        key = (run["id"], run.get("run_attempt"), sha)
        if key not in self.artifacts:
            artifacts = pages(
                "/repos/" + REPO + "/actions/runs/" + str(run["id"]) + "/artifacts",
                "artifacts",
                limit=200,
            )
            want = "unity-ork-p0-verify-" + sha + "-attempt-" + str(run.get("run_attempt"))
            self.artifacts[key] = (
                run.get("html_url")
                if any(
                    a.get("name") == want
                    and a.get("expired") is False
                    and a.get("size_in_bytes", 0) > 0
                    for a in artifacts
                )
                else None
            )
        return self.artifacts[key]

    def verify_run(self, name, sha, runs):
        for run in runs:
            proof = self.verified_artifact(run, sha, runs)
            if proof:
                return proof
        return None

    def verified_ancestors(self):
        if self.ancestors is None:
            # A bounded scan of the newest runs can prove retirement
            # when it finds an exact match. A missing proof keeps the ref.
            # Branch-specific activity checks still require every page.
            all_runs = pages(
                "/repos/" + REPO + "/actions/runs", "workflow_runs", limit=2000, allow_prefix=True
            )
            valid = [
                r
                for r in all_runs
                if r.get("name") == "Unity ORK P0"
                and r.get("status") == "completed"
                and r.get("conclusion") == "success"
                and SHA_RE.fullmatch(r.get("head_sha") or "")
                and have_commit(r["head_sha"])
                and ancestor(r["head_sha"], self.dev)
            ]
            self.ancestors = (all_runs, valid)
        return self.ancestors

    def descendant_proof(self, sha):
        all_runs, valid = self.verified_ancestors()
        for r in valid:
            if not ancestor(sha, r["head_sha"]):
                continue
            proof = self.verified_artifact(r, r["head_sha"], all_runs)
            if proof:
                return proof
        return None

    def changed_paths_since_base(self, sha):
        base = git("merge-base", sha, self.dev)
        result = subprocess.run(
            ["git", "diff", "--no-renames", "--name-only", "-z", base, sha], capture_output=True
        )
        check(result.returncode == 0, "candidate diff failed")
        paths = [os.fsdecode(p) for p in result.stdout.split(b"\0") if p]
        # Large, ambiguous branch snapshots need a separate audit.
        check(len(paths) <= 150, "candidate diff too broad for snapshot proof")
        return base, paths

    def same_changed_files(self, sha, verified_sha, paths):
        proc = subprocess.run(
            ["git", "diff", "--quiet", "--no-ext-diff", sha, verified_sha, "--", *paths],
            capture_output=True,
            text=True,
        )
        check(proc.returncode in (0, 1), "snapshot diff failed: " + proc.stderr[-240:])
        return proc.returncode == 0

    def snapshot_proof(self, sha):
        check(time.monotonic() < self.snapshot_deadline, "snapshot audit deferred: runtime budget")
        base, paths = self.changed_paths_since_base(sha)
        all_runs, valid = self.verified_ancestors()
        for r in valid:
            check(
                time.monotonic() < self.snapshot_deadline, "snapshot audit deferred: runtime budget"
            )
            verified_sha = r["head_sha"]
            if not ancestor(base, verified_sha):
                continue
            # ALL net changed files must be byte/mode identical in ONE
            # actual verified dev-history commit; names alone are never proof.
            if not self.same_changed_files(sha, verified_sha, paths):
                continue
            proof = self.verified_artifact(r, verified_sha, all_runs)
            if proof:
                return proof
        return None

    def workflow_only_proof(self, sha):
        # A housekeeping-only candidate is accepted by this exact
        # cleanup workflow, never by pretending Unity has run.
        if not ancestor(sha, self.dev):
            return None
        parents = git("rev-list", "--parents", "-n", "1", sha).split()
        if len(parents) != 2 or parents[0] != sha:
            return None
        files = git("diff-tree", "--no-commit-id", "--name-only", "-r", sha).splitlines()
        if files != [".github/workflows/close-merged-pending.yml"]:
            return None
        runs = pages("/repos/" + REPO + "/actions/runs?head_sha=" + sha, "workflow_runs", limit=300)
        for run in runs:
            if (
                run.get("head_sha") == sha
                and run.get("name") == "Close fully delivered pending branches"
                and run.get("head_branch") == "codex/dev"
                and run.get("status") == "completed"
                and run.get("conclusion") == "success"
            ):
                return run.get("html_url")
        return None

    def verified_archived_proof(self, name, sha):
        entry = ARCHIVED[name]
        check(sha == entry["head"], "historical branch moved since audit")
        # For this reviewed retirement set, independently prove the original
        # branch was itself green, not only that the replacement is green.
        if entry.get("original_run"):
            original = api("/repos/" + REPO + "/actions/runs/" + str(entry["original_run"]))
            check(
                original.get("head_sha") == sha
                and original.get("name") == "Unity ORK P0"
                and original.get("status") == "completed"
                and original.get("conclusion") == "success",
                "original SHA lacks successful Unity verification",
            )
            original_artifacts = pages(
                "/repos/" + REPO + "/actions/runs/" + str(entry["original_run"]) + "/artifacts",
                "artifacts",
                limit=200,
            )
            original_want = (
                "unity-ork-p0-verify-" + sha + "-attempt-" + str(original.get("run_attempt"))
            )
            check(
                any(
                    a.get("name") == original_want
                    and a.get("expired") is False
                    and a.get("size_in_bytes", 0) > 0
                    for a in original_artifacts
                ),
                "original candidate exact-SHA artifact missing",
            )
        if entry.get("workflow_only"):
            # Workflow-only history is verified by its own successful
            # cleanup run; it must never impersonate Unity acceptance.
            check(ancestor(sha, self.dev), "workflow-only branch not integrated")
            changed = git("diff-tree", "--no-commit-id", "--name-only", "-r", sha).splitlines()
            check(
                changed == [".github/workflows/close-merged-pending.yml"],
                "non-workflow files changed",
            )
            proof = api("/repos/" + REPO + "/actions/runs/" + str(entry["run"]))
            check(
                proof.get("head_sha") == sha
                and proof.get("status") == "completed"
                and proof.get("conclusion") == "success"
                and proof.get("name") == "Close fully delivered pending branches",
                "workflow-only SHA not proven by exact cleanup run",
            )
            return proof["html_url"]
        check(
            ancestor("ffc39f9fe1bea122774fd8a54e889083ab78e0e7", self.dev),
            "audited dev baseline is missing",
        )
        if entry["ancestor"]:
            check(ancestor(entry["verified"], self.dev), "replacement commit not merged")
        else:
            check(
                ancestor("8e9f7643ff147ef6f26419733c27ecaab38de207", self.dev),
                "evidence workflow integration is missing",
            )
        snapshot = entry["verified"] if entry.get("pin_verified_snapshot") else self.dev
        for file_path, want in entry["blobs"].items():
            check(
                git("rev-parse", snapshot + ":" + file_path) == want,
                "audited replacement changed: " + file_path,
            )
        proof = api("/repos/" + REPO + "/actions/runs/" + str(entry["run"]))
        check(
            proof.get("head_sha") == entry["verified"]
            and proof.get("status") == "completed"
            and proof.get("conclusion") == "success"
            and proof.get("name") == "Unity ORK P0",
            "replacement SHA lacks verified Unity CI",
        )
        artifacts = pages(
            "/repos/" + REPO + "/actions/runs/" + str(entry["run"]) + "/artifacts",
            "artifacts",
            limit=200,
        )
        want = (
            "unity-ork-p0-verify-" + entry["verified"] + "-attempt-" + str(proof.get("run_attempt"))
        )
        check(
            any(
                a.get("name") == want
                and a.get("expired") is False
                and a.get("size_in_bytes", 0) > 0
                for a in artifacts
            ),
            "replacement verify evidence missing or expired",
        )
        return proof["html_url"]


def main():
    check("GH_TOKEN" in os.environ, "missing GitHub token")
    check(os.environ.get("GITHUB_REPOSITORY") == REPO, "wrong repository")
    check(os.environ.get("GITHUB_REF") == REF, "wrong branch")
    with open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8") as f:
        event = json.load(f)
    check(
        event.get("ref") == REF
        and event.get("forced") is False
        and event.get("created") is False
        and event.get("deleted") is False,
        "not an ordinary dev update",
    )
    before, after = event.get("before"), event.get("after")
    check(
        isinstance(before, str)
        and isinstance(after, str)
        and SHA_RE.fullmatch(before)
        and SHA_RE.fullmatch(after)
        and before != "0" * 40
        and before != after,
        "invalid push history",
    )
    git("fetch", "--no-tags", "origin", "+refs/heads/*:refs/remotes/origin/*")
    dev = api("/repos/" + REPO + "/git/ref/heads/codex/dev")["object"]["sha"]
    check(git("rev-parse", "refs/remotes/origin/codex/dev") == dev, "dev changed while fetching")
    check(
        ancestor(before, after) and ancestor(after, dev),
        "force update, missing baseline, or stale event",
    )
    pr_heads = {p["head"]["ref"] for p in pages("/repos/" + REPO + "/pulls?state=open")}
    branches = pages("/repos/" + REPO + "/branches", limit=1000)
    # Sort by immutable ancestry first. Exhaustive divergent snapshot
    # comparison must never starve deletion of already merged heads.
    candidates = []
    for branch in branches:
        name = branch.get("name", "")
        sha = branch.get("commit", {}).get("sha", "")
        if not PENDING.fullmatch(name):
            continue
        check(bool(SHA_RE.fullmatch(sha)), "bad candidate SHA")
        candidates.append((not ancestor(sha, dev), name, branch))
    candidates.sort(key=lambda item: (item[0], item[1]))
    proofs = Proofs(dev)
    merged_seen = 0
    newly_merged = 0
    historical_seen = 0
    diverged_seen = 0
    descendant_proven = 0
    snapshot_proven = 0
    deleted = []
    kept = []
    for diverged, name, b in candidates:
        sha = b["commit"]["sha"]
        merged = not diverged
        historical = name in ARCHIVED
        if merged:
            merged_seen += 1
            if not ancestor(sha, before):
                newly_merged += 1
        else:
            diverged_seen += 1
        if historical:
            historical_seen += 1
        try:
            check(not b.get("protected"), "protected branch")
            check(name not in pr_heads, "open PR")
            check(git("rev-parse", "refs/remotes/origin/" + name) == sha, "local HEAD changed")
            # Never delete a branch with any CI still queued or running,
            # even when other branches contain an identical snapshot.
            branch_runs = runs_for(name)
            if historical:
                proof = proofs.verified_archived_proof(name, sha)
                method = "SHA-pinned audited replacement"
            elif merged:
                proof = proofs.verify_run(name, sha, branch_runs)
                method = "exact candidate SHA"
                if not proof:
                    proof = proofs.descendant_proof(sha)
                    method = "verified descendant in dev history"
                    if proof:
                        descendant_proven += 1
                if not proof:
                    proof = proofs.workflow_only_proof(sha)
                    if proof:
                        method = "same-SHA successful cleanup-only workflow"
            else:
                proof = proofs.snapshot_proof(sha)
                method = "exact changed-file snapshot in verified dev history"
                if proof:
                    snapshot_proven += 1
            check(proof, "no validated ancestor or exact snapshot replacement")
            archive_tag = ARCHIVED[name].get("archive_tag") if historical else None
            if archive_tag:
                check(
                    re.fullmatch(r"refs/tags/archive/guilai-v1/[a-z0-9-]+", archive_tag),
                    "invalid legacy narrative tag",
                )
                archived = git("ls-remote", "--tags", "--refs", "origin", archive_tag).splitlines()
                if not archived:
                    git("push", "origin", sha + ":" + archive_tag)
                    archived = git(
                        "ls-remote", "--tags", "--refs", "origin", archive_tag
                    ).splitlines()
                check(
                    archived == [sha + "\t" + archive_tag],
                    "legacy draft tag does not match original head",
                )
                print("ARCHIVED " + name + " at " + sha + " as " + archive_tag, flush=True)
            check(
                api("/repos/" + REPO + "/git/ref/heads/codex/dev")["object"]["sha"] == dev,
                "dev moved",
            )
            check(
                name
                not in {
                    p[side]["ref"]
                    for p in pages("/repos/" + REPO + "/pulls?state=open")
                    for side in ("head", "base")
                },
                "new open PR",
            )
            fresh = api("/repos/" + REPO + "/branches/" + urllib.parse.quote(name, safe=""))
            check(
                fresh["commit"]["sha"] == sha and not fresh.get("protected"),
                "remote HEAD changed or protected",
            )
            runs_for(name)
            ref = "refs/heads/" + name
            lines = git("ls-remote", "--heads", "origin", ref).splitlines()
            check(
                len(lines) == 1 and lines[0].split()[0] == sha,
                "compare-and-swap precondition failed",
            )
            git("push", "--force-with-lease=" + ref + ":" + sha, "origin", "--delete", ref)
            check(not git("ls-remote", "--heads", "origin", ref), "deletion not confirmed")
            deleted.append((name, sha, proof, method))
            print(
                "DELETED " + name + " at " + sha + " via " + method + " verified " + proof,
                flush=True,
            )
        except (RuntimeError, ValueError, KeyError, OSError) as err:
            kept.append((name, str(err)))
            print("RETAIN " + name + " : " + str(err), flush=True)
    print(
        f"SUMMARY merged_seen={merged_seen} newly_merged={newly_merged}"
        f" historical_audited={historical_seen} diverged_seen={diverged_seen}"
        f" descendant_proven={descendant_proven} snapshot_proven={snapshot_proven}"
        f" deleted={len(deleted)} kept={len(kept)}",
        flush=True,
    )
    with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as out:
        out.write("## Audited pending branch cleanup\n\n")
        out.write(
            f"Dev {dev}; event {before[:12]}..{after[:12]}; merged {merged_seen} (new {newly_merged})"
            f"; divergent examined {diverged_seen}; descendant verified {descendant_proven}"
            f"; snapshot verified {snapshot_proven}; deleted {len(deleted)}; retained {len(kept)}.\n\n"
        )
        for name, sha, proof, method in deleted:
            out.write(f"- Deleted {name} at {sha} via {method}; verify {proof}\n")
        for name, why in kept:
            out.write(f"- Retained {name}: {why}\n")


if __name__ == "__main__":
    main()
