#!/usr/bin/env bash
# game-ci CLI v0.1.72 ENGINE_LAUNCH_WRAPPER; never wrap activation/return calls.
set -u

run_tests=false
project_path=''
platform=''
previous=''
for argument in "$@"; do
  [[ "$argument" == '-runTests' ]] && run_tests=true
  [[ "$previous" == '-projectPath' ]] && project_path="$argument"
  [[ "$previous" == '-testPlatform' ]] && platform="$argument"
  previous="$argument"
done
if [[ "$run_tests" != true ]]; then
  exec "$@"
fi
if [[ -z "$project_path" ]]; then
  echo 'P0 wrapper requires an explicit project path.' >&2
  exit 1
fi

evidence="$GITHUB_WORKSPACE/${ARTIFACTS_PATH:-artifacts/p0}"
mkdir -p "$evidence"
"$@"
tests_exit=$?
if [[ "${platform,,}" == playmode ]]; then
  printf '%s\n' "$tests_exit" > "$evidence/playmode-editor-exit-code.txt"
  exit "$tests_exit"
fi
printf '%s\n' "$tests_exit" > "$evidence/test-editor-exit-code.txt"

# A separate Editor invocation proves the documented batch entry point and exit.
"$1" -batchmode -nographics -quit -projectPath "$project_path" \
  -executeMethod Rpg.Editor.OrkBootstrapSetup.ValidateBatch \
  -logFile "$evidence/batch.log"
batch_exit=$?
printf '%s\n' "$batch_exit" > "$evidence/batch-editor-exit-code.txt"
if [[ "$tests_exit" -ne 0 || "$batch_exit" -ne 0 ]]; then
  exit 1
fi
if ! grep -q 'RPG_BOOTSTRAP_VALIDATED' "$evidence/batch.log"; then
  echo 'Batch invocation did not produce the required validation marker.' >&2
  exit 1
fi
