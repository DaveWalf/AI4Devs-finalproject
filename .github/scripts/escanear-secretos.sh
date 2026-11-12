#!/usr/bin/env bash
# Escanea con gitleaks la historia propia del fork y falla si encuentra un secreto o si no escaneó
# lo que debía: gitleaks sale 0 con «0 commits scanned» cuando su git log interno falla.
# Uso: escanear-secretos.sh <gitleaks> [base]   (desde la raíz del repo, con la historia completa)
set -euo pipefail

gitleaks="$1"
# bcde5c4: main del fork, revert de LIDR del PR #107. Lo anterior es la plantilla, no este proyecto.
base="${2:-bcde5c4a233375c0f5ea3c8475786f8f2d3a6f49}"
rango="$base..HEAD"

if [ -e .gitleaksignore ]; then
  echo "::error::Hay un .gitleaksignore: silencia hallazgos sin pasar por la revisión del workflow."
  exit 1
fi

# Commits con alguna línea agregada, con el mismo --diff-merges que usa gitleaks.
esperado=$(git log --diff-merges=first-parent --numstat --format='C %H' "$rango" |
  awk '/^C /{ if (c) n++; c = 0; next } NF == 3 && $1 != "-" && $1 > 0 { c = 1 } END { if (c) n++; print n + 0 }')

registro="$(mktemp)"
set +e
"$gitleaks" git --no-banner --no-color --redact --log-opts="--diff-merges=first-parent $rango" . 2>&1 | tee "$registro"
estado=${PIPESTATUS[0]}
set -e

escaneados=$(grep -oE '[0-9]+ commits scanned' "$registro" | grep -oE '^[0-9]+' || echo 0)
echo "commits escaneados: $escaneados · esperados: $esperado · exit de gitleaks: $estado"

if [ "$estado" -ne 0 ]; then
  echo "::error::gitleaks encontró secretos o falló (exit $estado)."
  exit 1
fi
if [ "$escaneados" -ne "$esperado" ]; then
  echo "::error::gitleaks escaneó $escaneados commits y se esperaban $esperado."
  exit 1
fi
