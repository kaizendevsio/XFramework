#!/usr/bin/env python3
"""Separate production transport results: real WASM/pacer/relay, synthetic codecs (no native comparator)."""
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else 'bench-results')
paths = sorted(root.rglob('*.production.json'))
if paths:
    print('\nProduction media path with synthetic codecs\n')
    print('| run | direction | final sender / receiver path | shaped-path validity | sent fps / kbps | rendered fps | longest frame gap ms | decoder resets | lifetime audio delivery % (sequence span) | lifetime audio delay p50 / p99 ms | settled picture | quality failures |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|')
    for path in paths:
        result = json.loads(path.read_text())
        for direction in ('A->B', 'B->A'):
            d = result.get(direction, {})
            tier = d.get('senderTier') or {}
            sender_path, receiver_path = d.get('senderPath'), d.get('receiverPath')
            datagram = sender_path == 'UDP/relay' and receiver_path == 'UDP/relay'
            validity = 'final UDP; inspect timeline' if datagram else 'invalid shaped path: fallback or unknown'
            audio = d.get('audioDelivered')
            audio_percent = f'{audio * 100:.2f}' if audio is not None else '-'
            print(f"| {path.name.removesuffix('.production.json')} | {direction} | {sender_path} / {receiver_path} | {validity} | {d.get('sentFps')} / {d.get('sentKbps')} | "
                  f"{d.get('renderedFps')} | {d.get('longestFreezeMs')} | {d.get('decoderResets')} | {audio_percent} | "
                  f"{d.get('audioP50')} / {d.get('audioP99')} | {tier.get('height')}p{tier.get('framerate')} | {result.get('failures')} |")
    print('\nBoth production snapshots use their real media service, pacer, recovery, SFrame and relay. Codec payloads and decoder are synthetic; '
          'this measures transport behavior, not real decoder CPU or thermal load. Both camera preferences are 1080p30; production rate control may adapt. '
          'Video FPS and longest frame gap cover the measured window; audio and receive recovery counters cover the call lifetime, including startup/warmup. '
          'Audio metrics span received sequence numbers and lifetime delay samples; they exclude unobserved leading/trailing loss and are not directly comparable to the modeled cohort metric. '
          'WebSocket fallback bypasses TURN/netem and invalidates shaped-path performance claims even if the quality assertions pass. '
          'The test waits up to 45 seconds for UDP but continues after timeout; final UDP alone does not establish continuous UDP throughout measurement. '
          'Severe shaping injects loss on A downlink; A uplink has delay without injected loss, so this does not directly verify sender-uplink loss repairs. '
          'Revisions are included in the uploaded artifacts.')
