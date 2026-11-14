#!/usr/bin/env bash
# Run on the Xeon Dev Docker host. No rows, passwords, or archives leave the host.
set -euo pipefail
source_container=xframework-postgres
image=$(docker inspect --format '{{.Image}}' "$source_container")
target=$(docker run -d --rm --network none --tmpfs /var/lib/postgresql/data:rw,size=512m \
    -e POSTGRES_HOST_AUTH_METHOD=trust "$image")
trap 'docker stop "$target" >/dev/null' EXIT
for i in {1..45}; do
    # The init process also starts a temporary socket-only server; wait for final PID 1.
    if docker exec "$target" sh -c 'test "$(cat /proc/1/comm)" = postgres && pg_isready -q'; then break; fi
    sleep 1
done
docker exec "$target" pg_isready -q
SECONDS=0
docker exec "$source_container" pg_dump -U dbAdmin -d XFramework --schema-only --lock-wait-timeout=5s -Fc |
    docker exec -i "$target" pg_restore -U postgres -d postgres --single-transaction --no-owner --no-acl
echo "Schema stream and restore: ${SECONDS}s"
catalog="SELECT n.nspname, c.relkind, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
         WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname <> 'information_schema'
         UNION ALL SELECT n.nspname, 'c', con.conname || ':' || pg_get_constraintdef(con.oid)
         FROM pg_constraint con JOIN pg_namespace n ON n.oid=con.connamespace
         WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname <> 'information_schema' ORDER BY 1,2,3"
before=$(docker exec "$source_container" psql -XAt -U dbAdmin -d XFramework -c "$catalog")
after=$(docker exec "$target" psql -XAt -U postgres -d postgres -c "$catalog")
test -n "$before" && test "$before" = "$after" || { echo 'Restored catalog mismatch' >&2; exit 1; }
echo "Matching catalog entries: $(wc -l <<< "$after")"
docker exec "$target" psql -XAt -U postgres -d postgres -c "SELECT count(DISTINCT schemaname) AS schemas, count(*) AS tables FROM pg_tables WHERE schemaname NOT IN ('information_schema','pg_catalog')"
