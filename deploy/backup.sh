#!/usr/bin/env bash
set -euo pipefail
cd /opt/keycapstore
umask 077

BACKUP_DIR=/var/backups/keycapstore
RECIPIENT=/opt/keycapstore/backup-recipient.txt
KEEP_LOCAL=2

source /opt/keycapstore/backup.env

STAMP=$(date -u +%Y%m%dT%H%M%SZ)
NAME="keycapstore-$STAMP.dump.age"
FILE="$BACKUP_DIR/$NAME"

on_exit() {
  local status=$?
  rm -f "$FILE.partial"
  if [[ $status -ne 0 ]]; then
    curl -fsS -m 10 --retry 3 -o /dev/null "$HEALTHCHECK_URL/fail" || true
  fi
}
trap on_exit EXIT

mkdir -p "$BACKUP_DIR"

docker compose exec -T db pg_dump -U keycapstore -d keycapstore --format=custom \
  | age -R "$RECIPIENT" > "$FILE.partial"
mv "$FILE.partial" "$FILE"

ls -1t "$BACKUP_DIR"/keycapstore-*.dump.age | tail -n +$((KEEP_LOCAL + 1)) | xargs -r rm --

export RCLONE_CONFIG_R2_TYPE=s3
export RCLONE_CONFIG_R2_PROVIDER=Cloudflare
export RCLONE_CONFIG_R2_ENDPOINT="https://$R2_ACCOUNT_ID.r2.cloudflarestorage.com"
export RCLONE_CONFIG_R2_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID"
export RCLONE_CONFIG_R2_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY"
export RCLONE_CONFIG_R2_ACL=private
export RCLONE_CONFIG_R2_NO_CHECK_BUCKET=true

rclone copyto "$FILE" "r2:$R2_BUCKET/daily/$NAME"
if [[ $(date -u +%u) -eq 7 ]]; then
  rclone copyto "$FILE" "r2:$R2_BUCKET/weekly/$NAME"
fi

curl -fsS -m 10 --retry 3 -o /dev/null "$HEALTHCHECK_URL"

echo "==> Backup criado e enviado: $NAME ($(du -h "$FILE" | cut -f1))"
