#!/bin/sh
# Entry point of the container gamehub-migrate (US-030):
#   1. Live: backup of the database before migrating (pg_dump, kind PreMigration).
#   2. Applies all pending EF Core migrations with the migration bundle.
# Any failure ends the container with an error, so the new app version does not start.
set -eu

if [ "${App__Environment__Name:-}" = "Live" ]; then
  backup_dir="${App__Backup__Directory:-/backups}"
  mkdir -p "$backup_dir"
  file="$backup_dir/gamehub-PreMigration-$(date -u +%Y%m%dT%H%M%SZ).dump"
  echo "Backup vor der Migration: $file"
  pg_dump --format=custom --no-owner --file="$file"
  echo "Backup erstellt ($(du -h "$file" | cut -f1))."
fi

echo "Migrationen werden ausgeführt …"
exec /app/efbundle --connection "$ConnectionStrings__gamehub"
