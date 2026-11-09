#!/usr/bin/env bash
# game-ci CLI v0.1.72 ENGINE_LAUNCH_WRAPPER; never wrap activation/return calls.
set -u

run_tests=false
project_path=''
prepare=false
previous=''
for argument in "$@"; do
  [[ "$argument" == '-runTests' ]] && run_tests=true
  [[ "$previous" == '-projectPath' ]] && project_path="$argument"
  [[ "$argument" == '-rpgPrepareDefaults' ]] && prepare=true
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

# A fresh editor process enters Play Mode through the normal scene entry point.
if [[ "$prepare" != true && -f "$project_path/Assets/Game/Scenes/SoundwellTrial.unity" ]]; then
  # Unity Search's startup index can race the initial domain reload in this editor.
  # Keep the preference local to the smoke process and leave the checkout untouched.
  search_settings="$project_path/UserSettings/Search.settings"
  mkdir -p "$(dirname "$search_settings")" || exit 1
  search_backup=''
  if [[ -f "$search_settings" ]]; then
    search_backup="$(mktemp)" || exit 1
    cp -p "$search_settings" "$search_backup" || exit 1
  fi
  printf 'indexOnEditorStartup = false\n' > "$search_settings" || exit 1
  timeout --kill-after=15s 180s "$1" -batchmode -projectPath "$project_path" \
    -executeMethod Rpg.Editor.SoundwellSmoke.RunFromCommandLine -logFile "$evidence/soundwell.log"
  soundwell_exit=$?
  if [[ -n "$search_backup" ]]; then
    mv -f "$search_backup" "$search_settings" || exit 1
  else
    rm -f "$search_settings" || exit 1
  fi
  printf '%s\n' "$soundwell_exit" > "$evidence/soundwell-editor-exit-code.txt"
  if [[ "$soundwell_exit" -ne 0 ]]; then
    grep -m 2 -A 15 -E '^[[:alpha:].]+Exception:' "$evidence/soundwell.log" >&2 || true
    tail -n 80 "$evidence/soundwell.log" >&2
    exit "$soundwell_exit"
  fi
fi
