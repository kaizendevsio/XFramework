#!/usr/bin/env bash
# Xeon Dev only. Source is read-only; all archives, writes and rollback targets are ephemeral.
set -euo pipefail
source=xframework-postgres
image=$(docker inspect -f '{{.Image}}' "$source")
target=$(docker run -d --rm --network none --tmpfs /var/lib/postgresql/data:rw,size=768m \
    --tmpfs /backup:rw,size=128m -e POSTGRES_HOST_AUTH_METHOD=trust "$image")
trap 'docker stop "$target" >/dev/null' EXIT
for _ in {1..60}; do
    if docker exec "$target" sh -c 'test "$(cat /proc/1/comm)" = postgres && pg_isready -q'; then break; fi
    sleep 1
done
SECONDS=0
docker exec "$source" pg_dump -U dbAdmin -d XFramework --lock-wait-timeout=5s --exclude-table-data=audit.event -Fc |
    docker exec -i "$target" sh -c 'cat > /backup/dev.dump'
echo "Dev dump seconds: $SECONDS (audit.event data excluded)"
restore() {
    docker exec "$target" createdb -U postgres "$1"
    docker exec "$target" pg_restore -U postgres -d "$1" --single-transaction --no-owner --no-acl /backup/dev.dump
}
# Include record IDs, tenant IDs, amounts, status, and all other stored fields, without displaying rows.
fingerprint() {
    local pguser=$2 database=$3
    docker exec -i "$1" psql -qXAt -v ON_ERROR_STOP=1 -U "$pguser" -d "$database" <<'SQL'
BEGIN READ ONLY ISOLATION LEVEL REPEATABLE READ;
SELECT format('SELECT %L, count(*), md5(coalesce(string_agg(md5(to_jsonb(t)::text), '''' ORDER BY md5(to_jsonb(t)::text)), '''')) FROM %I.%I t',
    schemaname || '.' || tablename, schemaname, tablename)
FROM pg_tables WHERE schemaname IN ('POS','Wallet','Inventario') ORDER BY schemaname, tablename
\gexec
COMMIT;
SQL
}
expected=$(fingerprint "$source" dbAdmin XFramework)
test -n "$expected"
SECONDS=0
restore candidate
test "$(fingerprint "$target" postgres candidate)" = "$expected" || { echo "Mismatch; check source for concurrent writes" >&2; exit 1; }
echo "First restore and verification seconds: $SECONDS"
docker exec "$target" psql -qX -v ON_ERROR_STOP=1 -U postgres -d candidate -c 'CREATE TABLE public.rollback_drill_marker (id int)'
SECONDS=0
restore rollback
test "$(docker exec "$target" psql -XAt -U postgres -d rollback -c "SELECT to_regclass('public.rollback_drill_marker') IS NULL")" = t
test "$(fingerprint "$target" postgres rollback)" = "$expected"
echo "Pre-change restore and verification seconds: $SECONDS"
echo "Matching ledger/module tables: $(wc -l <<< "$expected")"
