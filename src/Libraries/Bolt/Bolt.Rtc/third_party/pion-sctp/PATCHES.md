# pion/sctp, vendored

Upstream: https://github.com/pion/sctp, tag `v1.11.3` (module `github.com/pion/sctp`, MIT, see `LICENSE`).
Copied from the Go module cache; `.github/` (upstream CI) is left out. The sidecar consumes it through
`replace github.com/pion/sctp => ../third_party/pion-sctp` in `../../sidecar/go.mod`. Drop the directory and
the `replace` once upstream ships an equivalent fix (v1.12.0 still has the bug).

## Changes against v1.11.3: partial reliability (RFC 3758 / RFC 7496)

The bug: `movePendingDataChunkToInflightQueue` called `checkPartialReliabilityStatus` on the first
transmission, so on a `maxRetransmits: 0` (or `maxPacketLifeTime: 0`) stream every chunk was abandoned while
still in flight. Every SACK then moved the Advanced.Peer.Ack.Point over all outstanding chunks (C1-C3) and
the sender sent a FORWARD-TSN, which the peer SACKs: about one extra packet each way per data packet. On
timed streams the lifetime was measured from the latest transmission, so a lifetime above zero never expired.

- `checkPartialReliabilityStatus` runs only when a chunk is about to be retransmitted, before `nSent` counts
  that retransmission (A3), and reports whether it abandoned the chunk; the chunk is then not retransmitted.
  Timed lifetimes run from the first transmission (`chunkPayloadData.firstSent`).
- `abandonChunksToRetransmit` applies that, at the start of each gather, to every chunk marked for
  retransmission (T3-rtx, RACK, tail loss probe, three miss indications) and advances the
  Advanced.Peer.Ack.Point (`advancePeerTSNAckPoint`, C1-C3, shared with SACK and T3 handling).
- C2 moves the point over gap-acknowledged chunks too, stopping at the last abandoned one. Otherwise each
  FORWARD-TSN only skips up to the next received chunk, one round trip per hole, and with more than one loss
  per round trip the peer's cumulative TSN falls ever further behind. FORWARD-TSN still reports stream
  sequence numbers of abandoned chunks only (C4).
- T3-rtx restarts when the point moves (as R3 does when the earliest outstanding TSN is acknowledged), so a
  hole that takes two round trips to skip does not end in a T3-rtx expiry when RTO is under that.
- `chooseForwardTSN` sends a FORWARD-TSN only when the Advanced.Peer.Ack.Point moved since the last one; an
  unchanged one is resent after a smoothed RTT without the peer catching up, or on T3-rtx expiry (A5). It is
  bundled into an outgoing DATA or SACK packet when one goes out in the same round and has room (F2).
- RTT samples and RACK's delivered time ignore abandoned chunks (they were skipped, not delivered).

Regression test: `association_pr_rate_test.go` (1.7 Mbit/s over vnet links with loss). Upstream tests are
unchanged and pass.
