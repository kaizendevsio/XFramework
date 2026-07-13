#!/usr/bin/env bash
set -euo pipefail

test "$(id -u)" -eq 0
source_root="${1:-}"
bootstrap_source="${BASH_SOURCE[0]}"
case "$source_root:$bootstrap_source" in
  /*:/*) ;;
  *) echo "bootstrap and staging root must use absolute paths" >&2; exit 1 ;;
esac

/usr/bin/python3 - "$source_root" "$bootstrap_source" <<'PY'
import os
import stat
import sys

source_root, bootstrap_source = sys.argv[1:]
components = (
    "deploy/bootstrap-xframework-bolt-phase0-root.sh",
    "scripts/manage-bolt-phase0-root.py",
    "scripts/run-bolt-phase0-watchdog.sh",
    "scripts/manage-bolt-phase0-deployment-lease.py",
    "scripts/verify-bolt-phase0-qualification.py",
    "deploy/systemd/xframework-bolt-phase0-watchdog.service",
    "deploy/systemd/xframework-bolt-phase0-watchdog.timer",
)

def fail(message: str) -> None:
    raise SystemExit(message)

def validate_directory(metadata: os.stat_result) -> None:
    if not stat.S_ISDIR(metadata.st_mode) or metadata.st_uid != 0 or metadata.st_mode & 0o022:
        fail("bootstrap staging parent must be root-owned and not group/world writable")

def open_directory(path: str) -> int:
    if not os.path.isabs(path) or os.path.normpath(path) != path:
        fail("bootstrap staging path must be canonical and absolute")
    descriptor = os.open("/", os.O_RDONLY | os.O_DIRECTORY)
    try:
        validate_directory(os.fstat(descriptor))
        for component in path.split("/")[1:]:
            if not component or component in {".", ".."}:
                fail("bootstrap staging path is invalid")
            child = os.open(
                component,
                os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=descriptor,
            )
            os.close(descriptor)
            descriptor = child
            validate_directory(os.fstat(descriptor))
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise

def validate_component(root_fd: int, relative: str) -> None:
    parts = relative.split("/")
    parent_fd = os.dup(root_fd)
    try:
        for component in parts[:-1]:
            child = os.open(
                component,
                os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=parent_fd,
            )
            os.close(parent_fd)
            parent_fd = child
            validate_directory(os.fstat(parent_fd))
        descriptor = os.open(
            parts[-1],
            os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0),
            dir_fd=parent_fd,
        )
        try:
            metadata = os.fstat(descriptor)
            current = os.stat(parts[-1], dir_fd=parent_fd, follow_symlinks=False)
            if (
                not stat.S_ISREG(metadata.st_mode)
                or metadata.st_uid != 0
                or metadata.st_nlink != 1
                or metadata.st_mode & 0o222
                or (metadata.st_dev, metadata.st_ino) != (current.st_dev, current.st_ino)
            ):
                fail("bootstrap component must be root-owned, single-link, and nonwritable")
        finally:
            os.close(descriptor)
    finally:
        os.close(parent_fd)

root_fd = open_directory(source_root)
try:
    expected_bootstrap = source_root + "/deploy/bootstrap-xframework-bolt-phase0-root.sh"
    if bootstrap_source != expected_bootstrap:
        fail("bootstrap must execute from the reviewed staging root")
    for component in components:
        validate_component(root_fd, component)
finally:
    os.close(root_fd)
PY

deployment_user=github-runner
deployment_group=github-runner
deploy_root=/home/github-runner/xframework-deploy
protected_root=/opt/xframework
protected_env="$protected_root/xeon-dev.env"
libexec_root=/usr/local/libexec/xframework-bolt-phase0
root_helper=/usr/local/sbin/xframework-bolt-phase0-root
watchdog=/usr/local/sbin/xframework-bolt-phase0-watchdog
lease_manager="$libexec_root/manage-bolt-phase0-deployment-lease.py"
lease_lock="$libexec_root/deployment-lease.lock"
service=/etc/systemd/system/xframework-bolt-phase0-watchdog.service
timer=/etc/systemd/system/xframework-bolt-phase0-watchdog.timer
sudoers=/etc/sudoers.d/xframework-bolt-phase0-root

id "$deployment_user" >/dev/null
inspect_status=0
hub_state="$(/usr/bin/timeout --signal=TERM --kill-after=5s 30s /usr/bin/docker inspect --format '{{.State.Running}}' xframework-bolt-hub 2>/dev/null)" || inspect_status=$?
if [ "$inspect_status" -ne 0 ]; then
  container_status=0
  container_names="$(/usr/bin/timeout --signal=TERM --kill-after=5s 30s /usr/bin/docker container ls -a --no-trunc --filter 'name=^/xframework-bolt-hub$' --format '{{.Names}}' 2>/dev/null)" || container_status=$?
  if [ "$container_status" -ne 0 ] || [ -n "$container_names" ]; then
    echo "unable to prove xframework-bolt-hub is absent" >&2
    exit 1
  fi
  daemon_status=0
  daemon_version="$(/usr/bin/timeout --signal=TERM --kill-after=5s 30s /usr/bin/docker info --format '{{.ServerVersion}}' 2>/dev/null)" || daemon_status=$?
  if [ "$daemon_status" -ne 0 ] || [ -z "$daemon_version" ]; then
    echo "unable to verify Docker daemon health" >&2
    exit 1
  fi
  hub_state=absent
fi
case "$hub_state" in
  false|absent) ;;
  true) echo "xframework-bolt-hub must be stopped before bootstrap" >&2; exit 1 ;;
  *) echo "unable to verify xframework-bolt-hub state" >&2; exit 1 ;;
esac

install -d -o root -g root -m 0755 /usr/local/sbin "$libexec_root"
/usr/bin/python3 - "$lease_lock" "$deployment_group" <<'PY'
import grp
import os
import stat
import sys

lock_path, deployment_group = sys.argv[1:]
lock_gid = grp.getgrnam(deployment_group).gr_gid

def fail(message: str) -> None:
    raise SystemExit(message)

def parent_signature(metadata: os.stat_result) -> tuple[int, ...]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_uid,
        metadata.st_gid,
    )

def open_trusted_parent(path: str) -> tuple[int, tuple[int, ...]]:
    parent = os.path.dirname(path)
    descriptor = os.open("/", os.O_RDONLY | os.O_DIRECTORY)
    try:
        root_metadata = os.fstat(descriptor)
        if root_metadata.st_uid != 0 or stat.S_IMODE(root_metadata.st_mode) & 0o022:
            fail("lease lock root parent is insecure")
        for component in parent.split("/")[1:]:
            child = os.open(
                component,
                os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=descriptor,
            )
            opened = os.fstat(child)
            current = os.stat(component, dir_fd=descriptor, follow_symlinks=False)
            os.close(descriptor)
            descriptor = child
            if (
                parent_signature(opened) != parent_signature(current)
                or not stat.S_ISDIR(opened.st_mode)
                or opened.st_uid != 0
                or stat.S_IMODE(opened.st_mode) & 0o022
            ):
                fail("lease lock parent chain is insecure")
        return descriptor, parent_signature(os.fstat(descriptor))
    except BaseException:
        os.close(descriptor)
        raise

parent_fd, parent_identity = open_trusted_parent(lock_path)
descriptor = -1
try:
    name = os.path.basename(lock_path)
    read_flags = (
        os.O_RDONLY
        | getattr(os, "O_CLOEXEC", 0)
        | getattr(os, "O_NOFOLLOW", 0)
    )
    try:
        descriptor = os.open(name, read_flags, dir_fd=parent_fd)
    except FileNotFoundError:
        created = os.open(
            name,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_CLOEXEC", 0)
            | getattr(os, "O_NOFOLLOW", 0),
            0o440,
            dir_fd=parent_fd,
        )
        try:
            os.write(created, b"0")
            os.fchown(created, 0, lock_gid)
            os.fchmod(created, 0o440)
            os.fsync(created)
        finally:
            os.close(created)
        os.fsync(parent_fd)
        descriptor = os.open(name, read_flags, dir_fd=parent_fd)

    opened = os.fstat(descriptor)
    current = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
    reopened_parent, reopened_identity = open_trusted_parent(lock_path)
    os.close(reopened_parent)
    if (
        not stat.S_ISREG(opened.st_mode)
        or opened.st_nlink != 1
        or opened.st_uid != 0
        or opened.st_gid != lock_gid
        or stat.S_IMODE(opened.st_mode) != 0o440
        or opened.st_size != 1
        or (opened.st_dev, opened.st_ino) != (current.st_dev, current.st_ino)
        or reopened_identity != parent_identity
    ):
        fail("installed lease lock validation failed")
finally:
    if descriptor >= 0:
        os.close(descriptor)
    os.close(parent_fd)
PY
/usr/bin/python3 - "$source_root" <<'PY'
import hashlib
import os
import secrets
import stat
import sys

source_root = sys.argv[1]
components = (
    ("scripts/manage-bolt-phase0-root.py", "/usr/local/sbin/xframework-bolt-phase0-root", 0o555),
    ("scripts/run-bolt-phase0-watchdog.sh", "/usr/local/sbin/xframework-bolt-phase0-watchdog", 0o555),
    ("scripts/manage-bolt-phase0-deployment-lease.py", "/usr/local/libexec/xframework-bolt-phase0/manage-bolt-phase0-deployment-lease.py", 0o555),
    ("scripts/verify-bolt-phase0-qualification.py", "/usr/local/libexec/xframework-bolt-phase0/verify-bolt-phase0-qualification.py", 0o444),
    ("deploy/systemd/xframework-bolt-phase0-watchdog.service", "/etc/systemd/system/xframework-bolt-phase0-watchdog.service", 0o644),
    ("deploy/systemd/xframework-bolt-phase0-watchdog.timer", "/etc/systemd/system/xframework-bolt-phase0-watchdog.timer", 0o644),
)
maximum_bytes = 4 * 1024 * 1024

def fail(message: str) -> None:
    raise SystemExit(message)

def validate_directory(metadata: os.stat_result) -> None:
    if not stat.S_ISDIR(metadata.st_mode) or metadata.st_uid != 0 or metadata.st_mode & 0o022:
        fail("bootstrap staging or destination parent is insecure")

def open_directory(path: str) -> int:
    descriptor = os.open("/", os.O_RDONLY | os.O_DIRECTORY)
    try:
        validate_directory(os.fstat(descriptor))
        for component in path.split("/")[1:]:
            child = os.open(
                component,
                os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=descriptor,
            )
            os.close(descriptor)
            descriptor = child
            validate_directory(os.fstat(descriptor))
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise

def signature(metadata: os.stat_result) -> tuple[int, ...]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_mode,
        metadata.st_uid,
        metadata.st_gid,
        metadata.st_nlink,
        metadata.st_size,
        metadata.st_mtime_ns,
        metadata.st_ctime_ns,
    )

def read_complete(descriptor: int, expected_size: int) -> bytes:
    chunks: list[bytes] = []
    total = 0
    while True:
        request_size = min(64 * 1024, expected_size - total + 1)
        if request_size <= 0:
            fail("bootstrap component grew during read")
        try:
            chunk = os.read(descriptor, request_size)
        except InterruptedError:
            continue
        if not chunk:
            break
        chunks.append(chunk)
        total += len(chunk)
        if total > expected_size:
            fail("bootstrap component grew during read")
    if total != expected_size:
        fail("bootstrap component byte count changed during read")
    return b"".join(chunks)

def read_component(root_fd: int, relative: str) -> bytes:
    parts = relative.split("/")
    parent_fd = os.dup(root_fd)
    try:
        for component in parts[:-1]:
            child = os.open(
                component,
                os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=parent_fd,
            )
            os.close(parent_fd)
            parent_fd = child
            validate_directory(os.fstat(parent_fd))
        descriptor = os.open(
            parts[-1],
            os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0),
            dir_fd=parent_fd,
        )
        try:
            before = os.fstat(descriptor)
            if (
                not stat.S_ISREG(before.st_mode)
                or before.st_uid != 0
                or before.st_nlink != 1
                or before.st_mode & 0o222
                or before.st_size <= 0
                or before.st_size > maximum_bytes
            ):
                fail("bootstrap component metadata is insecure")
            first = read_complete(descriptor, before.st_size)
            # TEST_COMPONENT_REPLACEMENT_WINDOW: the path and opened inode must remain identical.
            os.lseek(descriptor, 0, os.SEEK_SET)
            second = read_complete(descriptor, before.st_size)
            after = os.fstat(descriptor)
            current = os.stat(parts[-1], dir_fd=parent_fd, follow_symlinks=False)
            if (
                first != second
                or len(first) != before.st_size
                or len(second) != before.st_size
                or hashlib.sha256(first).digest() != hashlib.sha256(second).digest()
                or signature(before) != signature(after)
                or (after.st_dev, after.st_ino) != (current.st_dev, current.st_ino)
                or not stat.S_ISREG(current.st_mode)
            ):
                fail("bootstrap component changed during copy")
            return first
        finally:
            os.close(descriptor)
    finally:
        os.close(parent_fd)

def atomic_copy(destination: str, mode: int, content: bytes) -> None:
    parent_path, name = os.path.split(destination)
    parent_fd = open_directory(parent_path)
    temporary = f".{name}.bootstrap.{os.getpid()}.{secrets.token_hex(8)}"
    descriptor = -1
    try:
        descriptor = os.open(
            temporary,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            mode,
            dir_fd=parent_fd,
        )
        view = memoryview(content)
        while view:
            written = os.write(descriptor, view)
            if written <= 0:
                fail("bootstrap component write failed")
            view = view[written:]
        os.fchown(descriptor, 0, 0)
        os.fchmod(descriptor, mode)
        os.fsync(descriptor)
        installed = os.fstat(descriptor)
        if not stat.S_ISREG(installed.st_mode) or installed.st_nlink != 1 or installed.st_uid != 0:
            fail("installed bootstrap component metadata is invalid")
        os.close(descriptor)
        descriptor = -1
        os.replace(temporary, name, src_dir_fd=parent_fd, dst_dir_fd=parent_fd)
        os.fsync(parent_fd)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        try:
            os.unlink(temporary, dir_fd=parent_fd)
        except FileNotFoundError:
            pass
        os.close(parent_fd)

root_fd = open_directory(source_root)
try:
    for relative, destination, mode in components:
        atomic_copy(destination, mode, read_component(root_fd, relative))
finally:
    os.close(root_fd)
PY
test ! -L /opt
if [ -e "$protected_root" ] || [ -L "$protected_root" ]; then
  test -d "$protected_root"
  test ! -L "$protected_root"
fi
install -d -o root -g "$deployment_group" -m 1770 "$protected_root"
python3 - "$protected_root" "$deployment_user" "$deployment_group" <<'PY'
import os
import pwd
import grp
import stat
import sys

root, deployment_user, deployment_group = sys.argv[1:]
uid = pwd.getpwnam(deployment_user).pw_uid
gid = grp.getgrnam(deployment_group).gr_gid
directory_flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_NOFOLLOW", 0)
parent_fd = os.open(root, directory_flags)
try:
    parent = os.fstat(parent_fd)
    if (
        not stat.S_ISDIR(parent.st_mode)
        or parent.st_uid != 0
        or parent.st_gid != gid
        or stat.S_IMODE(parent.st_mode) != 0o1770
    ):
        raise SystemExit("protected deployment parent validation failed")
    flags = os.O_RDWR | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    try:
        env_fd = os.open("xeon-dev.env", flags, dir_fd=parent_fd)
    except FileNotFoundError:
        env_fd = os.open(
            "xeon-dev.env",
            flags | os.O_CREAT | os.O_EXCL,
            0o600,
            dir_fd=parent_fd,
        )
    try:
        opened = os.fstat(env_fd)
        if (
            not stat.S_ISREG(opened.st_mode)
            or opened.st_nlink != 1
            or opened.st_uid not in {0, uid}
            or opened.st_gid not in {0, gid}
        ):
            raise SystemExit("protected deployment environment validation failed")
        opened_identity = (opened.st_dev, opened.st_ino)
        # TEST_REPLACEMENT_WINDOW: descriptor operations below must remain path-independent.
        os.fchown(env_fd, uid, gid)
        os.fchmod(env_fd, 0o600)
        os.fsync(env_fd)
        sealed = os.fstat(env_fd)
        current = os.stat("xeon-dev.env", dir_fd=parent_fd, follow_symlinks=False)
        if (
            (sealed.st_dev, sealed.st_ino) != opened_identity
            or (current.st_dev, current.st_ino) != opened_identity
            or not stat.S_ISREG(current.st_mode)
            or current.st_nlink != 1
            or current.st_uid != uid
            or current.st_gid != gid
            or stat.S_IMODE(current.st_mode) != 0o600
        ):
            raise SystemExit("protected deployment environment identity changed")
    finally:
        os.close(env_fd)
finally:
    os.close(parent_fd)
PY
install -d -o root -g root -m 0755 "$deploy_root" "$deploy_root/runs" "$deploy_root/phase0-last-known-good"
install -d -o root -g root -m 0700 "$deploy_root/quarantine"
install -d -o "$deployment_user" -g "$deployment_group" -m 0700 "$deploy_root/phase0-watchdog" "$deploy_root/hooks"

sudoers_tmp="$(mktemp /etc/sudoers.d/.xframework-bolt-phase0-root.XXXXXX)"
trap 'rm -f "$sudoers_tmp"' EXIT
cat > "$sudoers_tmp" <<'EOF'
Cmnd_Alias XFRAMEWORK_BOLT_PHASE0_ROOT = /usr/local/sbin/xframework-bolt-phase0-root verify-bootstrap, /usr/local/sbin/xframework-bolt-phase0-root ensure-watchdog, /usr/local/sbin/xframework-bolt-phase0-root prepare-run *, /usr/local/sbin/xframework-bolt-phase0-root activate *
github-runner ALL=(root) NOPASSWD: XFRAMEWORK_BOLT_PHASE0_ROOT
EOF
chmod 0440 "$sudoers_tmp"
chown root:root "$sudoers_tmp"
visudo -cf "$sudoers_tmp"
mv "$sudoers_tmp" "$sudoers"
trap - EXIT

systemctl daemon-reload
systemctl enable --now xframework-bolt-phase0-watchdog.timer
"$root_helper" verify-bootstrap
