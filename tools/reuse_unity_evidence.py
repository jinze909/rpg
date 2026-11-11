#!/usr/bin/env python3
"""Reuse exact-SHA pending verification on dev; any uncertainty runs Unity again.

Only push/verify runs whose head is already the final candidate are eligible.
Prepare/publish runs and old runs without an execution-context receipt are not.
No credentials or downloaded code are written into evidence or executed.
"""
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

from collect_unity_p0 import expected_tests_for

REPO = "jinze909/rpg"
WORKFLOW = ".github/workflows/unity-p0.yml"
CONTEXT_NAME = "unity-reuse-context"
MAX_ARCHIVE = 256 * 1024 * 1024
MAX_MEMBER = 4 * 1024 * 1024
SHA = re.compile(r"^[0-9a-f]{40}$")


def timestamp(value):
    value = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if value.tzinfo is None:
        raise ValueError("Timestamp lacks timezone")
    return value


def context_for(root, env):
    sha = env["GITHUB_SHA"]
    if not SHA.fullmatch(sha) or env["GITHUB_REPOSITORY"] != REPO:
        raise ValueError("Unexpected repository or revision")
    actual = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
    if actual != sha:
        raise ValueError("Checkout differs from requested revision")
    mode = env.get("REQUESTED_MODE") or (root / "production/ci/p0-mode.txt").read_text().strip()
    license_method = env.get("LICENSE_METHOD") or "personal"
    if mode not in ("verify", "prepare") or license_method not in ("personal", "serial", "file", "floating"):
        raise ValueError("Unknown execution mode")
    workflows = {path: hashlib.sha256((root / path).read_bytes()).hexdigest()
                 for path in (WORKFLOW, ".github/workflows/unity-p0-editor.yml")}
    unity = re.search(r"^m_EditorVersion: (\S+)$",
                      (root / "ProjectSettings/ProjectVersion.txt").read_text(), re.MULTILINE)
    if unity is None:
        raise ValueError("Missing pinned Unity version")
    return {
        "schema": 1, "repository": REPO, "repository_id": env["GITHUB_REPOSITORY_ID"],
        "source_sha": sha, "mode": mode, "license_method": license_method,
        "workflows": workflows, "unity": unity.group(1),
        # The same SHA also pins packages, test source, wrapper, collector and
        # Docker digest/runner/test-filter declarations in the hashed workflows.
        "expected_tests": sorted(expected_tests_for(mode,
            (root / "Assets/Game/Scenes/SoundwellTrial.unity").is_file(), root)),
    }


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class GitHub:
    def __init__(self, token):
        self.token = token
        self.opener = urllib.request.build_opener(NoRedirect())

    def request(self, path):
        if not path.startswith("/repos/" + REPO + "/"):
            raise ValueError("Unexpected API route")
        return urllib.request.Request("https://api.github.com" + path, headers={
            "Authorization": "Bearer " + self.token,
            "Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28",
        })

    def json(self, path):
        with self.opener.open(self.request(path), timeout=25) as response:
            data = response.read(MAX_MEMBER + 1)
            if len(data) > MAX_MEMBER:
                raise ValueError("API response too large")
            return json.loads(data)

    def members(self, artifact, names):
        # Never forward GITHUB_TOKEN to GitHub's signed artifact storage URL.
        try:
            response = self.opener.open(self.request(
                f"/repos/{REPO}/actions/artifacts/{int(artifact['id'])}/zip"), timeout=25)
        except urllib.error.HTTPError as error:
            if error.code != 302:
                raise
            location = error.headers["Location"]
            parsed = urllib.parse.urlsplit(location)
            if parsed.scheme != "https" or parsed.username or parsed.password or parsed.port not in (None, 443):
                raise ValueError("Unsafe artifact redirect")
            response = urllib.request.urlopen(urllib.request.Request(location), timeout=25)
        with response, tempfile.TemporaryFile() as archive_file:
            size = 0
            while chunk := response.read(64 * 1024):
                size += len(chunk)
                if size > MAX_ARCHIVE:
                    raise ValueError("Artifact exceeds reuse download budget")
                archive_file.write(chunk)
            archive_file.seek(0)
            with zipfile.ZipFile(archive_file) as archive:
                result = {}
                for name in names:
                    matches = [entry for entry in archive.infolist() if entry.filename == name]
                    if len(matches) != 1 or matches[0].file_size > MAX_MEMBER:
                        raise ValueError("Missing, duplicate or oversized evidence member")
                    result[name] = archive.read(matches[0])
                return result


def valid_run(run, context):
    return (run.get("event") == "push" and run.get("path") == WORKFLOW
            and run.get("head_sha") == context["source_sha"]
            and run.get("head_branch", "").startswith("codex/pending/")
            and run.get("repository", {}).get("full_name") == REPO
            and run.get("head_repository", {}).get("full_name") == REPO
            and str(run.get("head_repository", {}).get("id")) == context["repository_id"])


def artifact_for(artifacts, name, run, now):
    found = [a for a in artifacts if a.get("name") == name
             and a.get("expired") is False
             and 0 < a.get("size_in_bytes", 0) <= MAX_ARCHIVE
             and timestamp(a["expires_at"]) > now
             and a.get("workflow_run", {}).get("id") == run["id"]
             and a.get("workflow_run", {}).get("head_sha") == run["head_sha"]]
    return found[0] if len(found) == 1 else None


def accepted_report(report, xml, context, run):
    if not (report.get("mode") == "verify"
            and report.get("status") == "passed-clean-candidate"
            and report.get("source_sha") == context["source_sha"]
            and report.get("candidate_sha") == context["source_sha"]
            and report.get("unity") == context["unity"]
            and report.get("unity_action_outcome") == "success"
            and report.get("errors") == []
            and report.get("changed_files_sha256") == {}
            and report.get("run_url") == f"https://github.com/{REPO}/actions/runs/{run['id']}"):
        return False
    results = ET.fromstring(xml)
    cases = list(results.iter("test-case"))
    names = [case.get("fullname") for case in cases]
    return (results.get("result") == "Passed"
            and sorted(names) == context["expected_tests"]
            and all(case.get("result") == "Passed" for case in cases))


def find_evidence(api, context, now):
    data = api.json(f"/repos/{REPO}/actions/workflows/unity-p0.yml/runs?"
                    f"head_sha={context['source_sha']}&per_page=100")
    # Do not infer the absence of a newer failure from an incomplete page.
    if data["total_count"] > 100:
        return None
    runs = [r for r in data["workflow_runs"] if valid_run(r, context)]
    runs.sort(key=lambda r: timestamp(r["updated_at"]), reverse=True)
    for run in runs:
        if run.get("status") != "completed" or run.get("conclusion") != "success":
            # A newer failure/active run invalidates older green evidence.
            return None
        data = api.json(f"/repos/{REPO}/actions/runs/{int(run['id'])}/artifacts?per_page=100")
        if data["total_count"] > 100:
            return None
        attempt = int(run["run_attempt"])
        receipt = artifact_for(data["artifacts"], f"{CONTEXT_NAME}-{attempt}", run, now)
        evidence = artifact_for(data["artifacts"],
            f"unity-ork-p0-verify-{context['source_sha']}-attempt-{attempt}", run, now)
        if receipt is None or evidence is None:
            continue
        stored = api.members(receipt, ("unity-reuse-context.json",))
        if json.loads(stored["unity-reuse-context.json"]) != context:
            continue
        files = api.members(evidence, ("validation.json", "editmode-results.xml"))
        if accepted_report(json.loads(files["validation.json"]), files["editmode-results.xml"], context, run):
            return f"https://github.com/{REPO}/actions/runs/{run['id']}"
    return None


def probe(root, env, api, now):
    context = context_for(root, env)
    # Record only nonsecret execution conditions. This receipt cannot certify
    # success: reuse additionally requires a successful run and real validation.
    (Path(env["RUNNER_TEMP"]) / "unity-reuse-context.json").write_text(
        json.dumps(context, sort_keys=True) + "\n", encoding="utf-8")
    if (env.get("GITHUB_EVENT_NAME") != "push"
            or env.get("GITHUB_REF") != "refs/heads/codex/dev" or context["mode"] != "verify"):
        return None
    return find_evidence(api, context, now)


def main(env=None, api=None, root=None):
    env = os.environ if env is None else env
    root = Path(__file__).resolve().parents[1] if root is None else root
    reused = None
    try:
        api = GitHub(env["GH_TOKEN"]) if api is None else api
        reused = probe(root, env, api, dt.datetime.now(dt.timezone.utc))
    except Exception as error:
        # Network errors, expired artifacts, malformed receipts and programming
        # errors cannot turn the optimization into permission to skip Unity.
        print(f"Unity evidence reuse unavailable ({type(error).__name__}); running Unity.")
    with open(env["GITHUB_OUTPUT"], "a", encoding="utf-8") as out:
        out.write(f"reused={'true' if reused else 'false'}\n")
    if reused:
        print("Reusing exact-SHA Unity verification: " + reused)
        if env.get("GITHUB_STEP_SUMMARY"):
            with open(env["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as out:
                out.write(f"## Unity verification reused\n\nCandidate `{env['GITHUB_SHA']}`: {reused}\n"
                          "Matched verify mode, workflow/environment receipt and complete test results.\n")
    else:
        print("No equivalent exact-SHA evidence; running normal Unity validation.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
