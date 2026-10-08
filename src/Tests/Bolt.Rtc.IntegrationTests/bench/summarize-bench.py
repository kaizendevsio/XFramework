#!/usr/bin/env python3
"""Media benchmark results (bench/*.json from run-bench.sh) as Markdown tables."""
import json
import pathlib
import sys

MODES = [("native", "native WebRTC"), ("bolt-baseline", "Bolt baseline"), ("bolt-current", "Bolt current")]


def kbps(bytes_, seconds):
    return round(bytes_ * 8 / seconds / 1000) if seconds else None


def main(directory):
    root = pathlib.Path(directory)
    scenarios = sorted(root.rglob("*.scenario"))
    rows = [
        "| link | path | sender wire kbps | receiver leg down kbps | receiver leg up kbps | receiver leg pkt/s down / up | "
        "codec payload kbps | overhead ratio | "
        "bytes/packet over payload | video received | fps | freezes (excess s) | video delay p50 / p99 ms (samples) | audio delivered | audio transport delay p50 / p99 ms (samples) | "
        "audio packet | SFrame B/frame | repairs |",
        "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    for scenario_file in scenarios:
        name, downlink, uplink, video = scenario_file.read_text().strip().split("|")
        for mode, label in MODES:
            path = scenario_file.with_name(f"{name}.{mode}.json")
            if not path.exists():
                rows.append(f"| {name} | {label} | failed | | | | | | | | | | | | | | | |")
                continue
            r = json.loads(path.read_text())
            s = r.get("seconds") or 0
            wire = r.get("wire") or {}
            up = wire.get("sender_up", {})
            down = wire.get("receiver_down", {})
            receiver_up = wire.get("receiver_up", {})
            feedback = receiver_up.get("bytes", 0)
            receiver_pps = f"{round(down.get('packets', 0) / s)} / {round(receiver_up.get('packets', 0) / s)}" if s else "-"
            media = r.get("mediaBytes") or 0
            packets = up.get("packets") or 0
            ratio = f"{up.get('bytes', 0) / media:.2f}" if media else "-"
            per_packet = f"{(up.get('bytes', 0) - media) / packets:.0f}" if packets else "-"
            p = r.get("picture") or {}
            a = r.get("audio") or {}
            label += f" ({str(r.get('implementationRevision', 'unknown'))[:8]})"
            if mode == "native":
                repairs = f"NACK {r.get('nacks', 0)}, RTX {r.get('retransmittedKbps', 0)} kbps, PLI {r.get('keyframeRequests', 0)}"
                audio_packet, sframe = "20 ms", "20 (stand-in)" if r.get("transform", "none") != "none" else "0"
            else:
                rec = r.get("recovery") or {}
                repairs = (f"NACK {rec.get('nacked', 0)} asked / {rec.get('recovered', 0)} repaired, keyframe req {r.get('keyframeRequests', 0)}"
                           if r.get("nack") else f"keyframe req {r.get('keyframeRequests', 0)}")
                audio_packet, sframe = f"{r.get('audioPacketMs', 20)} ms", r.get("sframeBytesPerFrame", "-")
            rows.append(
                f"| {name} | {label} | {kbps(up.get('bytes', 0), s)} | {kbps(down.get('bytes', 0), s)} | {kbps(feedback, s)} | {receiver_pps} | "
                f"{kbps(media, s)} | {ratio} | {per_packet} | {p.get('resolution', '-')} | {p.get('fps', '-')} | "
                f"{p.get('freezes', '-')} ({p.get('frozenSeconds', '-')}) | {p.get('delayP50', '-')} / {p.get('delayP99', '-')} ({p.get('delaySamples', 0)}) | "
                f"{r.get('audioDelivered', '-')}% | {a.get('delayP50', '-')} / {a.get('delayP99', '-')} ({a.get('delaySamples', 0)}) | {audio_packet} | {sframe} | {repairs} |"
            )
    print("\n".join(rows))
    print("\nModeled WebCodecs/native comparison only: excludes production pacer, relay media lanes and audio redundancy. "
          "Freeze threshold is fixed at 250 ms; reported duration is gap excess, including window boundaries. "
          "Audio delay is encoded output to encoded arrival, excluding capture/encode and audible playout; "
          "delivery counts the measured send cohort with a fixed drain. Video delay requires readable barcodes; sample counts expose coverage. "
          "Baseline/current use the same metric version and source profile; old metric-version-1 freeze/delivery numbers are not directly comparable.")
    failures = list(root.rglob("failures.txt"))
    if failures:
        print("\nFailed runs: " + "; ".join(f.read_text().strip().replace("\n", ", ") for f in failures))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "bench-results")
