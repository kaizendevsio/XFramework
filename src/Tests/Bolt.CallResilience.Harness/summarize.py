#!/usr/bin/env python3
"""Turns harness logs (<name>.relay.log / <name>.receiver.log) into Markdown tables."""
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


def ms(value):
    return "-" if value is None else f"{value} ms"


def main(directory):
    root = pathlib.Path(directory)
    names = sorted({p.name.split(".")[0] for p in root.rglob("*.relay.log")})
    runs = []
    for name in names:
        relay = summary(next(root.rglob(f"{name}.relay.log")))
        receiver = summary(next(iter(root.rglob(f"{name}.receiver.log")), root / "missing"))
        runs.append((name, relay, receiver))

    rows = [
        "| run | outcome | audio one-way ms p50 / p90 / p99 / max | after 20 s: p50 / p99 / max | audio delivered | "
        "longest audio gap | decodable pictures | video frozen s | keyframes | relay drops (audio / video) |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    rate_rows = [
        "| run | settled video kbps (median) | settled estimate kbps | final picture | converged at | "
        "picture changes (after settling) | video suspended s | picture timeline |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for name, relay, receiver in runs:
        delay = receiver.get("audioDelayMs", {})
        settled = receiver.get("audioDelayAfter20sMs", {})
        counters = relay.get("counters", {})
        drops = f"{counters.get('media.relay_drops[audio]', 0)} / {counters.get('media.relay_drops[video]', 0)}"
        rows.append(
            f"| {name} | {relay.get('outcome', 'no summary')} | "
            f"{delay.get('p50', '-')} / {delay.get('p90', '-')} / {delay.get('p99', '-')} / {delay.get('max', '-')} | "
            f"{settled.get('p50', '-')} / {settled.get('p99', '-')} / {settled.get('max', '-')} | "
            f"{receiver.get('audioDelivered', '-')}% | {receiver.get('longestAudioGapMs', '-')} ms | "
            f"{receiver.get('picturesDecodable', '-')} of {relay.get('videoPicturesSent', '-')} | "
            f"{receiver.get('frozenSeconds', '-')} | {receiver.get('keyframes', '-')} | {drops} |"
        )
        rate = relay.get("rate")
        if rate:
            converged = rate.get("convergedAtS")
            rate_rows.append(
                f"| {name} | {rate.get('settledVideoKbpsMedian', '-')} | {rate.get('settledEstimateKbpsMedian', '-')} | "
                f"{rate.get('finalRung', '-')} | {f'{converged:.1f} s' if converged is not None else '-'} | "
                f"{rate.get('rungChanges', '-')} ({rate.get('rungChangesAfterSettle', '-')}) | "
                f"{rate.get('suspendedSeconds', '-')} | {rate.get('rungTimeline', '')} |"
            )
    print("\n".join(rows))
    if len(rate_rows) > 2:
        print("\nAdaptive sender (runs with ADAPTIVE=1):\n")
        print("\n".join(rate_rows))

    # Resumable calls (RESUME=1): what the phone did about the disturbance, and whether the other side
    # (the sender, whose call is "outcome") ever saw the call end.
    resumed = [run for run in runs if run[2].get("mode") == "resume"]
    if not resumed:
        return
    rows = [
        "",
        "Resumable calls: the receiver loses its connection and resumes it with a fresh ticket. "
        "\"Other side\" is the sender's call; \"back after network\" is from the network returning "
        "(or the app unfreezing) to the first live audio packet / decodable picture (delivered within 2 s of being sent, "
        "so the backlog of the gap does not count).",
        "",
        "| run | event | other side | receiver | resumes | loss noticed -> resumed | audio back after network | "
        "picture back after network | longest audio gap | seat held (relay) |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    for name, relay, receiver in resumed:
        resumes = receiver.get("resumes", [])
        took = ", ".join(f"{r.get('timeToResumeMs')} ms ({r.get('attempts')} try)" for r in resumes) or "-"
        holds = relay.get("resume", {}) or {}
        held = ", ".join(f"{round(h['resumedAtS'] - h['awayAtS'], 1)} s" for h in holds.get("holds", [])) or "-"
        event = receiver.get("eventKind", "-")
        if receiver.get("eventSeconds"):
            event += f" {receiver['eventSeconds']} s"
        state = "gave up" if receiver.get("gaveUp") else "in call"
        rows.append(
            f"| {name} | {event} | {relay.get('outcome', 'no summary')} | {state} | {len(resumes)} | {took} | "
            f"{ms(receiver.get('audioBackAfterMs'))} | {ms(receiver.get('videoBackAfterMs'))} | "
            f"{receiver.get('longestAudioGapMs', '-')} ms | {held} |"
        )
    print("\n".join(rows))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "logs")
