"""Summarize native JSONL and existing Yap live diagnostics without mixing devices/runs."""
import argparse
import collections
import json
import math
import statistics
from pathlib import Path


def percentile(values, fraction):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, math.ceil(len(ordered) * fraction) - 1)] if ordered else None


def summarize(paths, warmup):
    groups = collections.defaultdict(list)
    seen = set()
    for path in paths:
        for line in Path(path).read_text(encoding="utf-8-sig").splitlines():
            row = json.loads(line)
            if row.get("kind") == "yap-native-video-v1":
                if row["seconds"] < warmup:
                    continue
                key = ("native", str(path), row["run"])
                sample = row
                identity = (*key, row["time"])
            elif "sample" in row:
                raw = row["sample"]
                metrics = raw.get("metrics", {})
                if metrics.get("ElapsedMs", 0) < warmup * 1000:
                    continue
                key = ("web", raw.get("device"), raw.get("callId"), row.get("sessionId"))
                remote = next(iter(raw.get("remotes", [])), {}).get("metrics", {})
                sample = dict(captureFps=metrics.get("CaptureFps"), encodedFps=metrics.get("EncodedFps"),
                              receivedFps=remote.get("ReceivedFps"), outputFps=remote.get("RenderedFps"),
                              outputMaxGapMs=remote.get("DrawGapMaxMs"))
                identity = (*key, raw.get("sequence"))
            else:
                raise ValueError(f"Unsupported report record in {path}")
            if identity not in seen:
                seen.add(identity)
                groups[key].append(sample)
    result = []
    for key, rows in groups.items():
        item = {"group": key, "samples": len(rows), "metrics": {}, "thermalStates": {}}
        for metric in ("captureFps", "encodedFps", "receivedFps", "outputFps", "captureMaxGapMs",
                       "outputMaxGapMs", "cpuCorePercent", "memoryBytes", "sentKbps", "receivedKbps"):
            values = [r[metric] for r in rows if isinstance(r.get(metric), (int, float)) and math.isfinite(r[metric])]
            item["metrics"][metric] = {"median": statistics.median(values), "p10": percentile(values, .1),
                                       "p95": percentile(values, .95)} if values else None
        item["thermalStates"] = dict(collections.Counter(r.get("device", {}).get("thermal", "unavailable") for r in rows))
        item["settingsObserved"] = list({json.dumps(r.get("settings"), sort_keys=True) for r in rows if "settings" in r})
        item["note"] = "Output counters differ by pipeline; neither proves physical display FPS. Native CPU excludes WebView content processes."
        result.append(item)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reports", nargs="+")
    parser.add_argument("--warmup-seconds", type=float, default=120)
    args = parser.parse_args()
    print(json.dumps(summarize(args.reports, args.warmup_seconds), indent=2))
