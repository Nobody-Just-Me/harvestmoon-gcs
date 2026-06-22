#!/usr/bin/env bash
# push.sh — commit semua perubahan dan push ke GitHub
# Usage: ./push.sh "pesan commit"
#        ./push.sh               (pakai pesan otomatis dengan timestamp)

set -e

BRANCH=$(git rev-parse --abbrev-ref HEAD)
REMOTE="origin"

# Pesan commit: gunakan argumen pertama, atau generate otomatis
if [ -n "$1" ]; then
    MSG="$1"
else
    DATE=$(date '+%Y-%m-%d %H:%M')
    MSG="chore: update $DATE [$BRANCH]"
fi

echo ""
echo "╔══════════════════════════════════════════╗"
echo "║         MoonHarvest GCS — Push           ║"
echo "╚══════════════════════════════════════════╝"
echo "  Branch : $BRANCH"
echo "  Remote : $REMOTE"
echo "  Pesan  : $MSG"
echo ""

# Tampilkan file yang berubah
echo "── Perubahan ───────────────────────────────"
git status --short
echo ""

# Konfirmasi
read -rp "Lanjutkan push? [y/N] " CONFIRM
if [[ "$CONFIRM" != "y" && "$CONFIRM" != "Y" ]]; then
    echo "Dibatalkan."
    exit 0
fi

echo ""
echo "── Staging semua perubahan ─────────────────"
git add -A
git status --short

echo ""
echo "── Commit ──────────────────────────────────"
git commit -m "$MSG" || { echo "Tidak ada perubahan untuk di-commit."; exit 0; }

echo ""
echo "── Push ke $REMOTE/$BRANCH ─────────────────"
git push "$REMOTE" "$BRANCH"

echo ""
echo "✓ Berhasil push ke $REMOTE/$BRANCH"
echo "  https://github.com/Nobody-Just-Me/harvestmoon-gcs/tree/$BRANCH"
