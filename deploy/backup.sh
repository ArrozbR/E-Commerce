#!/usr/bin/env bash
set -euo pipefail
cd /opt/keycapstore
umask 077

BACKUP_DIR=/var/backups/keycapstore
RECIPIENT=/opt/keycapstore/backup-recipient.txt
KEEP_LOCAL=2

mkdir -p "$BACKUP_DIR"

STAMP=$(date -u +%Y%m%dT%H%M%SZ)
FILE="$BACKUP_DIR/keycapstore-$STAMP.dump.age"
trap 'rm -f "$FILE.partial"' EXIT

docker compose exec -T db pg_dump -U keycapstore -d keycapstore --format=custom \
  | age -R "$RECIPIENT" > "$FILE.partial"
mv "$FILE.partial" "$FILE"

ls -1t "$BACKUP_DIR"/keycapstore-*.dump.age | tail -n +$((KEEP_LOCAL + 1)) | xargs -r rm --

echo "==> Backup criado: $FILE ($(du -h "$FILE" | cut -f1))"