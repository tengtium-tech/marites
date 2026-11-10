#!/usr/bin/env bash
# Captures every screen and dialog the prototype has, from one build.
# Usage: bash screens/shoot.sh [base-url]
set -u
BASE="${1:-http://127.0.0.1:4331/index.html}"
if ! curl -sf -o /dev/null "$BASE"; then
  echo "No server at $BASE — start one first:" >&2
  echo "  npx vite build && python -m http.server 4331 -d dist" >&2
  exit 1
fi
CH="/c/Program Files/Google/Chrome/Application/chrome.exe"
OUT="$(cd "$(dirname "$0")" && pwd)"

shoot() {
  local name="$1" url="$2" h="${3:-1000}"
  "$CH" --headless=new --disable-gpu --hide-scrollbars \
    --force-device-scale-factor=2 --window-size=1440,"$h" \
    --virtual-time-budget=8000 \
    --screenshot="$OUT/$name.png" "$BASE$url" >/dev/null 2>&1
  echo "  $name"
}

echo "Screens"
shoot 00-login            ""
shoot 00-login-dark       "?dark=1"
shoot 01-queues           "#queues"
shoot 02-queue-detail     "#queue" 1120
shoot 05-reporting        "#report"
shoot 09-publish          "#publish"
shoot 12-ai-settings      "#ai"
shoot 14-storage          "#storage"
shoot 14b-live-only       "?live=1#queue" 1120
shoot 14c-live-only-queues "?live=1#queues"
shoot 14d-live-only-report "?live=1#report"
shoot 15b-storage-add     "?live=1#storage"
shoot 06-dark             "?dark=1#queue" 1120

echo "Setup wizard"
shoot 04-setup-profile    "?step=1#setup"
shoot 04a-setup-storage   "?step=2#setup"
shoot 04b-setup-preflight "?step=3#setup"
shoot 04c-setup-queues    "?step=4#setup"
shoot 04d-setup-capture   "?step=5#setup"

echo "Dialogs"
shoot 07-peek             "#peek"
shoot 03-replay-dialog    "#replay"
shoot 08-discard          "#discard"
shoot 15-purge            "#purge"
shoot 10-agent-executed   "#queue" 1120
shoot 13-chat-drawer      "#chat"
shoot 13b-chat-answer     "#chat-answer"
shoot 13c-chat-not-configured       "?noai=1#chat"
shoot 13d-chat-not-configured-admin "?noai=1#chat-admin"

echo "Audit and payload evidence"
shoot 11-audit            "#audit"
shoot 11b-audit-payload   "#audit-payload"
shoot 11c-audit-bulk      "#audit-bulk" 1180
shoot 11h-audit-expired   "#audit-expired"
shoot 11d-payload-diff    "#payload-diff"
shoot 11e-payload-discard "#payload-discard"
shoot 11f-payload-purge   "#payload-purge"
shoot 15c-payload-publish "#payload-publish"
shoot 11g-payload-dark    "?dark=1#payload-diff"
shoot 08b-discard-payload "#discard-payload"

echo "done"
