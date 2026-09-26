#!/usr/bin/env python3
"""Turns harness logs (<name>.relay.log / <name>.receiver.log) into one Markdown table."""
import json
import pathlib
import sys


def summary(path):
    if not path.exists():
        return {}
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("SUMMARY "):
            return json.loads(line[len("SUMMARY "):])
    return {}


def main(directory):
    root = pathlib.Path(directory)
    names = sorted({p.name.split(".")[0] for p in root.rglob("*.relay.log")})
    rows = [
        "| run | outcome | audio one-way ms p50 / p90 / p99 / max | audio delivered | longest audio gap | "
        "decodable pictures | video frozen s | keyframes | relay drops (audio / video) |",
        "|---|---|---|---|---|---|---|---|---|",
    ]
    for name in names:
        relay = summary(next(root.rglob(f"{name}.relay.log")))
        receiver = summary(next(iter(root.rglob(f"{name}.receiver.log")), root / "missing"))
        delay = receiver.get("audioDelayMs", {})
        counters = relay.get("counters", {})
        drops = f"{counters.get('media.relay_drops[audio]', 0)} / {counters.get('media.relay_drops[video]', 0)}"
        rows.append(
            f"| {name} | {relay.get('outcome', 'no summary')} | "
            f"{delay.get('p50', '-')} / {delay.get('p90', '-')} / {delay.get('p99', '-')} / {delay.get('max', '-')} | "
            f"{receiver.get('audioDelivered', '-')}% | {receiver.get('longestAudioGapMs', '-')} ms | "
            f"{receiver.get('picturesDecodable', '-')} of {relay.get('videoPicturesSent', '-')} | "
            f"{receiver.get('frozenSeconds', '-')} | {receiver.get('keyframes', '-')} | {drops} |"
        )
    print("\n".join(rows))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "logs")
