#!/usr/bin/env python3
"""Read opt-in call traces over SSH, alongside container CPU/memory; no public telemetry reader."""
import argparse
import datetime as dt
import json
import pathlib
import subprocess
import time


def ssh(target, key, command):
    args = ["ssh", "-o", "BatchMode=yes", "-o", "ConnectTimeout=8"]
    if key:
        args += ["-i", key]
    result = subprocess.run(args + [target, command], capture_output=True, text=True, timeout=15)
    if result.returncode:
        raise RuntimeError(result.stderr.strip() or "SSH command failed")
    return result.stdout


def summary(row):
    sample = row["sample"]
    m = sample["metrics"]
    now = dt.datetime.now(dt.timezone.utc)
    age = (now - dt.datetime.fromisoformat(row["receivedAt"].replace("Z", "+00:00"))).total_seconds()
    incoming = ", ".join(f'{r["metrics"].get("Width")}x{r["metrics"].get("Height")} '
                         f'{r["metrics"].get("RenderedFps")}fps drops={r["metrics"].get("LocalDrops")}' for r in sample["remotes"])
    return (f'{sample["device"]} test={row["sessionId"][:8]} call={sample["callId"][:8]} '
            f'#{sample["sequence"]} age={age:.1f}s path={sample["capturePath"]} '
            f'out={m.get("Width")}x{m.get("Height")} {m.get("EncodedFps")}fps '
            f'budget={m.get("TotalBudgetKbps")}kbps queue={m.get("QueueDelayMs")}ms '
            f'cut={m.get("LastCutFromKbps")}->{m.get("LastCutToKbps")} '
            f'cause={m.get("LastCutCause")} in=[{incoming}]')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="xeon@xeon-dev")
    parser.add_argument("--key", default=str(pathlib.Path.home() / ".ssh/codex_proxmox_ed25519"))
    parser.add_argument("--seconds", type=int, default=60)
    parser.add_argument("--output", required=True, help="Local NDJSON evidence file; appends distinct observed samples.")
    args = parser.parse_args()
    if not 1 <= args.seconds <= 1800:
        parser.error("--seconds must be 1..1800")
    path = pathlib.Path(args.output)
    path.parent.mkdir(parents=True, exist_ok=True)
    seen, deadline, stats_at = set(), time.monotonic() + args.seconds, 0
    command = "docker exec xframework-yap sh -c 'tail -q -n 1 /tmp/yap-call-diagnostics/*.ndjson 2>/dev/null || true'"
    with path.open("a", encoding="utf-8") as output:
        while True:
            try:
                text = ssh(args.host, args.key, command)
                if not text.strip():
                    print("No samples yet. Enable live call diagnostics on both devices, then start the call.", flush=True)
                for line in text.splitlines():
                    try:
                        row = json.loads(line)
                        identity = (row["sessionId"], row["sample"]["sequence"])
                        if identity in seen:
                            continue
                        seen.add(identity)
                        output.write(json.dumps({"kind": "device", **row}) + "\n")
                        print(summary(row), flush=True)
                    except (ValueError, KeyError):
                        continue  # An append in progress is read again next poll.
                if time.monotonic() >= stats_at:
                    stats = ssh(args.host, args.key, 'docker stats --no-stream --format "{{json .}}" xframework-yap xframework-bolt-hub xframework-communications')
                    for line in stats.splitlines():
                        row = json.loads(line)
                        output.write(json.dumps({"kind": "server", "observedAt": dt.datetime.now(dt.timezone.utc).isoformat(), "stats": row}) + "\n")
                    stats_at = time.monotonic() + 10
                output.flush()
            except (RuntimeError, subprocess.TimeoutExpired, ValueError) as error:
                print(f"Collection gap: {error}", flush=True)
            if time.monotonic() >= deadline:
                break
            time.sleep(min(2, max(0, deadline - time.monotonic())))


if __name__ == "__main__":
    main()
