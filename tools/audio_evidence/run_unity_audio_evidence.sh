#!/usr/bin/env bash
# Records the AudioEvidence PlayMode scenarios of a Unity project and analyzes them.
# Usage: run_unity_audio_evidence.sh <unity_project_dir> <output_dir> [test filter] [capture: renderer|filter]
# Respects the machine-wide Unity semaphore (max 3 editors, never two on one project).
set -u
PROJECT="${1:?unity project dir}"
OUT="${2:?output dir}"
FILTER="${3:-}"
CAPTURE="${4:-filter}"
UNITY="${UNITY_EXE:-N:/Unity/Editors/6000.3.24f1/Editor/Unity.exe}"
LOCKS="${LMS_UNITY_LOCKS:-N:/LetMeSleep/Validation/V030/locks}"
HERE="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$OUT/captures"
SLOT=""
while true; do
  for s in 1 2 3; do
    if mkdir "$LOCKS/unity-slot-$s" 2>/dev/null; then
      echo "audio-evidence $(date -Iseconds) $PROJECT" > "$LOCKS/unity-slot-$s/owner"; SLOT=$s; break 2
    fi
  done
  sleep 20
done
trap 'rm -rf "$LOCKS/unity-slot-$SLOT"' EXIT
export LMS_GIT_COMMIT="$(git -C "$PROJECT" rev-parse --short HEAD 2>/dev/null || echo unknown)"
ARGS=(-batchmode -force-d3d11 -projectPath "$PROJECT" -runTests -testPlatform PlayMode
      -testCategory AudioEvidence -testResults "$OUT/results.xml" -logFile "$OUT/unity.log"
      --lms-audio-evidence "$OUT/captures" --lms-audio-capture "$CAPTURE")
[ -n "$FILTER" ] && ARGS+=(-testFilter "$FILTER")
echo "slot $SLOT: $UNITY ${ARGS[*]}"
"$UNITY" "${ARGS[@]}"
CODE=$?
echo "unity exit $CODE"
python "$HERE/lms_audio_evidence.py" captures "$OUT/captures" --out "$OUT/analysis" --spectrograms
python "$HERE/lms_audio_evidence.py" report --captures "$OUT/analysis" --out "$OUT/analysis"
exit $CODE
