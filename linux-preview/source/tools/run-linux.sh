#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
if [[ "${EUID}" -eq 0 ]]; then
  echo 'Run FooMeter as your normal desktop user. Grant capture permissions to dumpcap instead.' >&2
  exit 1
fi
if [[ -z "${DISPLAY:-}" && "${1:-}" != --command && "${1:-}" != --help && "${1:-}" != --devices && "${1:-}" != --replay && "${1:-}" != --self-test-linux ]]; then
  echo 'No X11 display found. Enable XWayland in Hyprland and launch from your desktop terminal.' >&2
  exit 1
fi
exec ./FooMeter.Linux "$@"
