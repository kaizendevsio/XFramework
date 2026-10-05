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


def datagram(receiver):
    transport = receiver.get("transport") or {}
    # The plain receiver nests the channel's summary beside its keyframe requests; the resuming one does not.
    return transport.get("datagram", transport) if isinstance(transport, dict) else {}


def path(receiver):
    return datagram(receiver).get("path", "WebSocket")


# Phase 3 pairs: a UDP run and its WebSocket twin with identical settings (or the closest earlier row).
PAIRS = [
    ("udp-512k-1000ms-1pct", "ws-512k-1000ms-1pct"),
    ("udp-512k-1000ms-3pct", "ws-512k-1000ms-3pct"),
    ("udp-512k-1000ms-from720p", "after-512k-1000ms-from720p"),
    ("udp-512k-500ms-1pct", "ws-512k-500ms-1pct"),
    ("udp-4g-2mbit", "after-4g-2mbit"),
    ("udp-4g-outage-2s", "after-4g-outage-2s"),
]

# Bandwidth efficiency: this branch's client (compact SFrame, 60 ms audio on a scarce link, NACK) against the same
# relay with the previous client's choices (legacy SFrame, 20 ms audio, no NACK), same link.
EFFICIENCY = [
    ("udp-512k-1000ms-1pct", "udp-512k-1000ms-1pct-before"),
    ("udp-512k-1000ms-3pct", "udp-512k-1000ms-3pct-before"),
    ("udp-512k-500ms-1pct", "udp-512k-500ms-1pct-before"),
    ("ws-512k-1000ms-1pct", "ws-512k-1000ms-1pct-before"),
]


def main(directory):
    root = pathlib.Path(directory)
    names = sorted({p.name.split(".")[0] for p in root.rglob("*.relay.log")})
    runs = []
    for name in names:
        relay = summary(next(root.rglob(f"{name}.relay.log")))
        receiver = summary(next(iter(root.rglob(f"{name}.receiver.log")), root / "missing"))
        runs.append((name, relay, receiver))

    rows = [
        "| run | path | outcome | audio one-way ms p50 / p90 / p99 / max | after 20 s: p50 / p99 / max | audio delivered | "
        "longest audio gap | decodable pictures | video frozen s | keyframes | relay drops (audio / video) |",
        "|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    rate_rows = [
        "| run | settled video kbps (median) | settled estimate kbps | final picture | converged at | "
        "picture changes (after settling) | video suspended s | first picture | time to target | picture timeline |",
        "|---|---|---|---|---|---|---|---|---|---|",
    ]
    def reached_text(rate):
        if "targetHeight" not in rate: return "-"
        reached = rate.get("reachedTargetAtS")
        return f"{rate['targetHeight']}p after {reached:.1f} s" if reached is not None else f"{rate['targetHeight']}p never"

    for name, relay, receiver in runs:
        delay = receiver.get("audioDelayMs", {})
        settled = receiver.get("audioDelayAfter20sMs", {})
        counters = relay.get("counters", {})
        drops = f"{counters.get('media.relay_drops[audio]', 0)} / {counters.get('media.relay_drops[video]', 0)}"
        rows.append(
            f"| {name} | {path(receiver)} | {relay.get('outcome', 'no summary')} | "
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
                f"{rate.get('suspendedSeconds', '-')} | {rate.get('firstRung', '-')} | "
                f"{reached_text(rate)} | {rate.get('rungTimeline', '')} |"
            )
    print("\n".join(rows))

    # Phase 3: UDP against WebSocket, same link, same sender.
    by_name = {name: (relay, receiver) for name, relay, receiver in runs}
    compare = [
        "",
        "UDP (data channel through TURN) against the WebSocket, same link and sender. Audio delay is one way, "
        "after the first 20 s; \"video\" is the settled picture and how long video was frozen or suspended.",
        "",
        "| link | path | audio p50 / p99 ms | audio delivered | longest gap | video | frozen / suspended s | recovered after |",
        "|---|---|---|---|---|---|---|---|",
    ]
    for udp, ws in PAIRS:
        for name in (udp, ws):
            if name not in by_name:
                continue
            relay, receiver = by_name[name]
            settled = receiver.get("audioDelayAfter20sMs", {})
            rate = relay.get("rate") or {}
            compare.append(
                f"| {name} | {path(receiver)} | {settled.get('p50', '-')} / {settled.get('p99', '-')} | "
                f"{receiver.get('audioDelivered', '-')}% | {receiver.get('longestAudioGapMs', '-')} ms | "
                f"{rate.get('finalRung', '-')} ({rate.get('settledVideoKbpsMedian', '-')} kbps) | "
                f"{receiver.get('frozenSeconds', '-')} / {rate.get('suspendedSeconds', '-')} | {ms(receiver.get('recoverAfterMs'))} |"
            )
    if len(compare) > 5:
        print("\n".join(compare))

    efficiency = [
        "",
        "Bandwidth efficiency, after against before (the previous client's legacy SFrame, 20 ms audio and no NACK) on the "
        "same relay and link. Audio delay is one way after 20 s; NACK is asked / recovered / given up / declined.",
        "",
        "| run | audio p50 / p99 ms | audio delivered | audio packet | settled video kbps | final picture | decodable / sent | "
        "frozen / suspended s | NACK | keyframe requests | media kbps received |",
        "|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    for after, before in EFFICIENCY:
        for name in (after, before):
            if name not in by_name:
                continue
            relay, receiver = by_name[name]
            settled = receiver.get("audioDelayAfter20sMs", {})
            rate = relay.get("rate") or {}
            transport = receiver.get("transport") or {}
            nack = transport.get("nack") if isinstance(transport, dict) else None
            nack_text = f"{nack.get('asked')} / {nack.get('recovered')} / {nack.get('abandoned')} / {nack.get('declined')}" if nack else "off"
            efficiency.append(
                f"| {name} | {settled.get('p50', '-')} / {settled.get('p99', '-')} | {receiver.get('audioDelivered', '-')}% | "
                f"{rate.get('audioPacketMs', '-')} ms | {rate.get('settledVideoKbpsMedian', '-')} | {rate.get('finalRung', '-')} | "
                f"{receiver.get('picturesDecodable', '-')} / {relay.get('videoPicturesSent', '-')} | "
                f"{receiver.get('frozenSeconds', '-')} / {rate.get('suspendedSeconds', '-')} | {nack_text} | "
                f"{transport.get('keyframeRequests', '-') if isinstance(transport, dict) else '-'} | {receiver.get('mediaKbps', '-')} |"
            )
    if len(efficiency) > 6:
        print("\n".join(efficiency))

    transports = [(name, relay, receiver) for name, relay, receiver in runs if datagram(receiver)]
    if transports:
        rows = ["", "Datagram path per UDP run:", "",
                "| run | final path | opened at | opens / failures / ICE restarts | frames on the channel / on the socket | "
                "relay: sent on channel / fell back / redundant audio | timeline |",
                "|---|---|---|---|---|---|---|"]
        for name, relay, receiver in transports:
            t = datagram(receiver)
            d = relay.get("datagram") or {}
            rows.append(
                f"| {name} | {t.get('path')} | {t.get('openedAtS', '-')} s | {t.get('opens')} / {t.get('failures')} / {t.get('iceRestarts')} | "
                f"{t.get('datagramFrames')} / {t.get('socketMediaFrames')} | "
                f"{d.get('sent', '-')} / {d.get('fellBackToSocket', '-')} / {d.get('redundantAudio', '-')} | {t.get('timeline', '')[:300]} |"
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
