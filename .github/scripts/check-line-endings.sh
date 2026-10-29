#!/usr/bin/env bash
# Fails if a tracked text file is stored with CRLF in the repository (index) or in the checkout,
# except the file types that .gitattributes keeps as CRLF (*.cmd, *.bat, *.sln).
set -euo pipefail

offenders=$(git ls-files --eol \
  | awk '$1 ~ /crlf|mixed/ || $2 ~ /crlf|mixed/ { print $NF }' \
  | grep -Ev '\.(cmd|bat|sln)$' || true)

if [ -n "$offenders" ]; then
  echo "Diese Dateien haben CRLF- oder gemischte Zeilenenden (erwartet: LF, siehe .gitattributes):"
  echo "$offenders"
  exit 1
fi

echo "Alle Textdateien verwenden LF."
