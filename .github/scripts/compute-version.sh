#!/usr/bin/env bash
#
# compute-version.sh — calcula el próximo número de versión y las tags de imagen
# Docker a partir del contexto de GitHub Actions y de las labels de la PR mergeada.
#
# Contrato de entrada (env):
#   GITHUB_REF        refs/heads/main | refs/heads/dev | refs/tags/v* (default: vacío)
#   GITHUB_SHA        commit sha (default: git rev-parse HEAD). short = primeros 7
#   GITHUB_REPOSITORY owner/repo (para consultar la PR vía gh)
#   PR_LABELS         (override, testeable) labels separadas por espacio. Si está
#                     definida —aunque sea vacía— se usa en vez de consultar gh.
#   MOCK_TAGS         (override, testeable) lista de tags a usar en vez de git tag -l.
#   DRY_RUN=1         no ejecuta git tag/push; sólo loguea a stderr.
#
# Salidas: 'version=<x>' y un bloque de tags (una imagen por línea) a stdout y,
# si existe, a $GITHUB_OUTPUT (formato tags<<EOF ... EOF).

set -euo pipefail

IMAGE="ghcr.io/escorial-saic/pallets-api"
REF="${GITHUB_REF:-}"
SHA="${GITHUB_SHA:-$(git rev-parse HEAD 2>/dev/null || echo unknown)}"
SHORT="${SHA:0:7}"
DRY_RUN="${DRY_RUN:-0}"

if [[ -n "${MOCK_TAGS:-}" ]]; then
  ALL_TAGS="$MOCK_TAGS"
else
  ALL_TAGS="$(git tag -l)"
fi

# Lista de tags, una por línea (ALL_TAGS puede venir separada por espacios o newlines).
tag_lines() {
  # shellcheck disable=SC2086  # word-splitting intencional para normalizar separadores
  printf '%s\n' $ALL_TAGS
}

finals() {
  tag_lines | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' || true
}

latest_final() {
  finals | sort -V | tail -n1
}

# bump_core <base vX.Y.Z|vacío> <major|minor|patch> -> X.Y.Z
bump_core() {
  local base="${1:-v0.0.0}" bump="$2" M m p
  base="${base:-v0.0.0}"
  IFS=. read -r M m p <<<"${base#v}"
  case "$bump" in
    major) M=$((M + 1)); m=0; p=0 ;;
    minor) m=$((m + 1)); p=0 ;;
    patch) p=$((p + 1)) ;;
  esac
  echo "${M}.${m}.${p}"
}

# has_label <label> — matcheo por palabra tolerante a ':' en el nombre.
has_label() {
  grep -qE "(^|[[:space:]])$1([[:space:]]|\$)" <<<"${LABELS:-}"
}

get_labels() {
  if [[ -n "${PR_LABELS+x}" ]]; then
    echo "$PR_LABELS"
  else
    gh api "repos/${GITHUB_REPOSITORY:-}/commits/${SHA}/pulls" \
      --jq '.[].labels[].name' 2>/dev/null | paste -sd' ' - || true
  fi
}

pick_bump() {
  if has_label 'release:major'; then
    echo major
  elif has_label 'release:minor'; then
    echo minor
  else
    echo patch
  fi
}

create_tag() {
  local t="$1"
  if [[ "$DRY_RUN" == "1" ]]; then
    echo "[dry-run] git tag $t && git push origin $t" >&2
    return
  fi
  git tag "$t"
  git push origin "$t"
}

# emit <version> <tag...>
emit() {
  local version="$1"
  shift
  local tags=("$@")
  echo "version=$version"
  printf 'tags:\n'
  printf '  %s\n' "${tags[@]}"
  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    {
      echo "version=$version"
      echo "tags<<EOF"
      printf '%s\n' "${tags[@]}"
      echo "EOF"
    } >>"$GITHUB_OUTPUT"
  fi
}

LABELS="$(get_labels)"

case "$REF" in
  refs/tags/v*)
    ver="${REF#refs/tags/v}"
    IFS=. read -r M m _ <<<"$ver"
    emit "$ver" "$IMAGE:$ver" "$IMAGE:${M}.${m}" "$IMAGE:latest" "$IMAGE:sha-$SHORT"
    ;;
  refs/heads/main)
    if has_label 'release:skip'; then
      b="$(latest_final)"
      emit "${b#v}" "$IMAGE:latest" "$IMAGE:sha-$SHORT"
      exit 0
    fi
    core="$(bump_core "$(latest_final)" "$(pick_bump)")"
    create_tag "v$core"
    IFS=. read -r M m _ <<<"$core"
    emit "$core" "$IMAGE:$core" "$IMAGE:${M}.${m}" "$IMAGE:latest" "$IMAGE:sha-$SHORT"
    ;;
  refs/heads/dev)
    if has_label 'release:skip'; then
      emit "0.0.0-dev.$SHORT" "$IMAGE:dev" "$IMAGE:sha-$SHORT"
      exit 0
    fi
    target="$(bump_core "$(latest_final)" "$(pick_bump)")"
    n="$(tag_lines | grep -E "^v${target}-rc\.[0-9]+$" | sed -E 's/.*-rc\.//' | sort -n | tail -n1 || true)"
    ver="${target}-rc.$((${n:-0} + 1))"
    create_tag "v$ver"
    emit "$ver" "$IMAGE:$ver" "$IMAGE:dev" "$IMAGE:sha-$SHORT"
    ;;
  *)
    emit "0.0.0-dev.$SHORT" "$IMAGE:sha-$SHORT"
    ;;
esac
