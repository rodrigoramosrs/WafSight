#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

entries=()
for f in *.md; do
  [ "$f" = "index.md" ] && continue
  name=$(basename "$f")
  entries+=("  { \"name\": \"$name\", \"path\": \"docs/$name\" }")
done

echo "["
for i in "${!entries[@]}"; do
  if [ $i -lt $((${#entries[@]} - 1)) ]; then
    echo "${entries[$i]},"
  else
    echo "${entries[$i]}"
  fi
done
echo "]"
