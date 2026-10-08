#!/usr/bin/env python3
"""Separate production transport results: real WASM/pacer/relay, synthetic codecs (no native comparator)."""
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'bench-results')
paths = sorted(root.rglob('*.production.json'))
if paths:
    print('\nProduction media path with synthetic codecs\n')
    print('| run | direction | sent fps / kbps | rendered fps | longest frame gap ms | decoder resets | audio delivery (sequence span) | audio delay p50 / p99 ms | settled picture | quality failures |')
    print('|---|---|---|---|---|---|---|---|---|---|')
    for path in paths:
        result = json.loads(path.read_text())
        for direction in ('A->B', 'B->A'):
            d = result.get(direction, {})
            tier = d.get('senderTier') or {}
            print(f"| {path.name.removesuffix('.production.json')} | {direction} | {d.get('sentFps')} / {d.get('sentKbps')} | "
                  f"{d.get('renderedFps')} | {d.get('longestFreezeMs')} | {d.get('decoderResets')} | {d.get('audioDelivered')} | "
                  f"{d.get('audioP50')} / {d.get('audioP99')} | {tier.get('height')}p{tier.get('framerate')} | {result.get('failures')} |")
    print('\nBoth production snapshots use their real media service, pacer, recovery, SFrame and relay. Codec payloads and decoder are synthetic; '
          'this measures transport behavior, not real decoder CPU or thermal load. Both camera preferences are 1080p30; production rate control may adapt. '
          'Longest frame gap includes measured window boundaries. Existing production audio metrics span received sequence numbers and lifetime delay samples; '
          'they exclude unobserved leading/trailing loss and are not directly comparable to the modeled cohort metric. Revisions are included in the uploaded artifacts.')
