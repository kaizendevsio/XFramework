// SPDX-FileCopyrightText: 2026 The Pion community <https://pion.ly>
// SPDX-License-Identifier: MIT

package sctp

import (
	"encoding/binary"
	"math/rand"
	"net"
	"sort"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"github.com/pion/logging"
	"github.com/pion/transport/v5/test"
	"github.com/pion/transport/v5/vnet"
	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

type prLinkConfig struct {
	oneWay   time.Duration
	lossRate float64 // of the sender's packets that carry DATA
	// lossAll applies lossRate to every packet in both directions.
	lossAll   bool
	unordered bool
	relType   byte
	relVal    uint32
	minCwnd   uint32
	duration  time.Duration
	// Every 20 ms a 400-byte voice frame, and every videoEvery*20 ms a burst
	// of videoFragments 1100-byte video fragments.
	videoFragments int
	videoEvery     int
}

// prLinkCounts is what crossed the link while measuring.
type prLinkCounts struct {
	seconds          float64
	messagesSent     int
	messagesRecv     int
	dataPackets      int // sender to receiver, carrying DATA
	dataDropped      int // of those, dropped by the link
	senderPackets    int
	forwardTSNs      int // FORWARD-TSN chunks that reached the receiver
	forwardTSNAlone  int // packets carrying a FORWARD-TSN and no DATA
	receiverPackets  int
	receiverSACKs    int
	duplicateTSNSeen int // DATA chunks the receiver reported as duplicates
	t3Timeouts       uint64
	fastRetransmits  uint64
	maxAckLag        time.Duration // oldest chunk the peer had neither acknowledged nor skipped
	latencies        []time.Duration
}

func (c prLinkCounts) perSecond(n int) float64 {
	return float64(n) / c.seconds
}

func (c prLinkCounts) latency(q float64) time.Duration {
	if len(c.latencies) == 0 {
		return -1
	}
	sorted := append([]time.Duration(nil), c.latencies...)
	sort.Slice(sorted, func(i, j int) bool { return sorted[i] < sorted[j] })

	return sorted[int(float64(len(sorted)-1)*q)]
}

func (c prLinkCounts) log(t *testing.T) {
	t.Helper()
	t.Logf("%.1fs: messages sent=%d received=%d, latency p50=%v p99=%v max=%v; "+
		"DATA packets %.0f/s, dropped %.1f/s; sender: packets %.0f/s, FORWARD-TSN %.1f/s "+
		"(%.1f/s in packets without DATA), T3 timeouts %d, fast retransmits %d, cumulative ack lag max %v; "+
		"receiver: packets %.0f/s, SACKs %.0f/s, duplicate TSNs %d",
		c.seconds, c.messagesSent, c.messagesRecv,
		c.latency(.5).Round(time.Millisecond), c.latency(.99).Round(time.Millisecond),
		c.latency(1).Round(time.Millisecond),
		c.perSecond(c.dataPackets), c.perSecond(c.dataDropped),
		c.perSecond(c.senderPackets), c.perSecond(c.forwardTSNs), c.perSecond(c.forwardTSNAlone),
		c.t3Timeouts, c.fastRetransmits, c.maxAckLag.Round(time.Millisecond),
		c.perSecond(c.receiverPackets), c.perSecond(c.receiverSACKs), c.duplicateTSNSeen)
}

// runPRLossyLink sends a stream with the given reliability over a
// link with a one-way delay and random loss of DATA packets, paced as a call's
// media would be. It counts the control packets each side sends and the delay
// of every message delivered.
func runPRLossyLink(t *testing.T, cfg prLinkConfig) prLinkCounts { //nolint:cyclop,maintidx
	t.Helper()

	const (
		senderIP   = "1.1.1.1"
		receiverIP = "2.2.2.2"
	)

	loggerFactory := logging.NewDefaultLoggerFactory()

	wan, err := vnet.NewRouter(&vnet.RouterConfig{
		CIDR:          "0.0.0.0/0",
		MinDelay:      cfg.oneWay,
		QueueSize:     100000,
		LoggerFactory: loggerFactory,
	})
	require.NoError(t, err)

	var (
		mu        sync.Mutex
		counts    prLinkCounts
		measuring atomic.Bool
	)
	random := rand.New(rand.NewSource(1)) //nolint:gosec // deterministic loss pattern

	wan.AddChunkFilter(func(c vnet.Chunk) bool {
		if !measuring.Load() {
			return true
		}
		pkt := &packet{}
		if err := pkt.unmarshal(true, c.UserData()); err != nil {
			return true
		}
		mu.Lock()
		defer mu.Unlock()

		fromSender := c.SourceAddr().(*net.UDPAddr).IP.String() == senderIP //nolint:forcetypeassert
		hasData, forwardTSNs := false, 0
		for _, ch := range pkt.chunks {
			switch typed := ch.(type) {
			case *chunkPayloadData:
				hasData = true
			case *chunkForwardTSN, *chunkIForwardTSN:
				forwardTSNs++
			case *chunkSelectiveAck:
				counts.receiverSACKs++
				counts.duplicateTSNSeen += len(typed.duplicateTSN)
			}
		}
		if !fromSender {
			if cfg.lossAll && random.Float64() < cfg.lossRate {
				return false
			}
			counts.receiverPackets++

			return true
		}
		if hasData {
			counts.dataPackets++
			if random.Float64() < cfg.lossRate {
				counts.dataDropped++

				return false
			}
		} else if cfg.lossAll && random.Float64() < cfg.lossRate {
			return false
		} else if forwardTSNs > 0 {
			counts.forwardTSNAlone++
		}
		counts.forwardTSNs += forwardTSNs
		counts.senderPackets++

		return true
	})

	senderNet, err := vnet.NewNet(&vnet.NetConfig{StaticIPs: []string{senderIP}})
	require.NoError(t, err)
	require.NoError(t, wan.AddNet(senderNet))
	receiverNet, err := vnet.NewNet(&vnet.NetConfig{StaticIPs: []string{receiverIP}})
	require.NoError(t, err)
	require.NoError(t, wan.AddNet(receiverNet))
	require.NoError(t, wan.Start())
	defer wan.Stop() //nolint:errcheck

	receiverConn, err := receiverNet.DialUDP("udp4",
		&net.UDPAddr{IP: net.ParseIP(receiverIP), Port: defaultSCTPSrcDstPort},
		&net.UDPAddr{IP: net.ParseIP(senderIP), Port: defaultSCTPSrcDstPort},
	)
	require.NoError(t, err)
	defer receiverConn.Close() //nolint:errcheck
	senderConn, err := senderNet.DialUDP("udp4",
		&net.UDPAddr{IP: net.ParseIP(senderIP), Port: defaultSCTPSrcDstPort},
		&net.UDPAddr{IP: net.ParseIP(receiverIP), Port: defaultSCTPSrcDstPort},
	)
	require.NoError(t, err)
	defer senderConn.Close() //nolint:errcheck

	receiverCh := make(chan *Association, 1)
	go func() {
		assoc, serr := Server(Config{ //nolint:staticcheck
			NetConn:              receiverConn,
			MaxReceiveBufferSize: 1024 * 1024,
			LoggerFactory:        loggerFactory,
		})
		assert.NoError(t, serr)
		receiverCh <- assoc
	}()
	sender, err := Client(Config{ //nolint:staticcheck
		NetConn:              senderConn,
		MaxReceiveBufferSize: 1024 * 1024,
		MinCwnd:              cfg.minCwnd,
		LoggerFactory:        loggerFactory,
	})
	require.NoError(t, err)
	defer sender.Close() //nolint:errcheck
	receiver := <-receiverCh
	require.NotNil(t, receiver)
	defer receiver.Close() //nolint:errcheck

	stream, err := sender.OpenStream(1, PayloadTypeWebRTCBinary)
	require.NoError(t, err)
	stream.SetReliabilityParams(cfg.unordered, cfg.relType, cfg.relVal)

	var latencyMu sync.Mutex
	var latencies []time.Duration
	readerDone := make(chan struct{})
	go func() {
		defer close(readerDone)
		accepted, aerr := receiver.AcceptStream()
		if aerr != nil {
			return
		}
		buf := make([]byte, 64*1024)
		for {
			n, rerr := accepted.Read(buf)
			if rerr != nil {
				return
			}
			if n < 8 || !measuring.Load() {
				continue
			}
			sentAt := time.Unix(0, int64(binary.BigEndian.Uint64(buf))) //nolint:gosec // G115
			latencyMu.Lock()
			latencies = append(latencies, time.Since(sentAt))
			latencyMu.Unlock()
		}
	}()

	// How far the peer's cumulative TSN lags: the age of the oldest chunk it
	// has not acknowledged or been told to skip.
	var maxAckLag atomic.Int64
	lagStop := make(chan struct{})
	lagDone := make(chan struct{})
	go func() {
		defer close(lagDone)
		for {
			select {
			case <-lagStop:
				return
			case <-time.After(20 * time.Millisecond):
			}
			sender.lock.RLock()
			if c, ok := sender.inflightQueue.get(sender.cumulativeTSNAckPoint + 1); ok && measuring.Load() {
				if lag := int64(time.Since(c.firstSent)); lag > maxAckLag.Load() {
					maxAckLag.Store(lag)
				}
			}
			sender.lock.RUnlock()
		}
	}()

	voice := make([]byte, 400)
	video := make([]byte, 1100)
	send := func(payload []byte) {
		binary.BigEndian.PutUint64(payload, uint64(time.Now().UnixNano())) //nolint:gosec // G115
		_, werr := stream.Write(payload)
		require.NoError(t, werr)
	}

	// One message unmeasured, so the receiver has accepted the stream.
	send(voice)
	time.Sleep(4 * cfg.oneWay)

	sender.lock.Lock()
	sender.stats.reset()
	sender.lock.Unlock()
	measuring.Store(true)
	start := time.Now()
	ticker := time.NewTicker(20 * time.Millisecond)
	sent := 0
	for n := 0; time.Since(start) < cfg.duration; n++ {
		<-ticker.C
		send(voice)
		sent++
		if n%cfg.videoEvery == 0 {
			for range cfg.videoFragments {
				send(video)
				sent++
			}
		}
	}
	ticker.Stop()
	// Let the tail drain, including the FORWARD-TSN for a lost last message.
	time.Sleep(6 * cfg.oneWay)
	elapsed := time.Since(start)
	measuring.Store(false)

	mu.Lock()
	result := counts
	mu.Unlock()
	result.seconds = elapsed.Seconds()
	result.messagesSent = sent
	latencyMu.Lock()
	result.latencies = latencies
	result.messagesRecv = len(latencies)
	latencyMu.Unlock()
	sender.lock.RLock()
	result.t3Timeouts = sender.stats.getNumT3Timeouts()
	result.fastRetransmits = sender.stats.getNumFastRetrans()
	sender.lock.RUnlock()
	close(lagStop)
	<-lagDone
	result.maxAckLag = time.Duration(maxAckLag.Load())

	_ = sender.Close()
	_ = receiver.Close()
	<-readerDone

	return result
}

// TestUnreliableStreamForwardTSNRate: a partially reliable stream must not
// make the sender emit a FORWARD-TSN for chunks that are merely in flight.
// FORWARD-TSN is for chunks that were lost and that the policy forbids
// retransmitting, so its rate follows the loss rate, not the packet rate, and
// the receiver answers it with no more SACKs than that. Everything the link
// did not drop must still be delivered: unordered about one way after it was
// sent, ordered without waiting for a retransmission timeout. The peer's
// cumulative TSN must keep up even with several losses per round trip.
func TestUnreliableStreamForwardTSNRate(t *testing.T) {
	if testing.Short() {
		t.Skip("runs for several seconds")
	}

	for _, tc := range []struct {
		name      string
		oneWay    time.Duration
		lossRate  float64
		lossAll   bool
		unordered bool
		relType   byte
		relVal    uint32
		// of the 99th percentile; 0 for one way plus 50 ms
		maxLatency time.Duration
	}{
		{"unordered maxRetransmits 0", 25 * time.Millisecond, 0.01, false, true, ReliabilityTypeRexmit, 0, 0},
		{"ordered maxRetransmits 0", 25 * time.Millisecond, 0.01, false, false, ReliabilityTypeRexmit, 0, 500 * time.Millisecond},
		{"unordered maxPacketLifeTime 40 ms", 25 * time.Millisecond, 0.01, false, true, ReliabilityTypeTimed, 40, 0},
		// About 2.6 losses per round trip, of DATA, SACK and FORWARD-TSN alike.
		{"unordered maxRetransmits 0, 150 ms, 4% loss both ways", 150 * time.Millisecond, 0.04, true, true, ReliabilityTypeRexmit, 0, 0},
	} {
		t.Run(tc.name, func(t *testing.T) {
			lim := test.TimeOut(60 * time.Second)
			defer lim.Stop()

			counts := runPRLossyLink(t, prLinkConfig{
				oneWay:    tc.oneWay,
				lossRate:  tc.lossRate,
				lossAll:   tc.lossAll,
				unordered: tc.unordered,
				relType:   tc.relType,
				relVal:    tc.relVal,
				minCwnd:   128 * 1024,
				duration:  4 * time.Second,
				// 1.7 Mbit/s of payload.
				videoFragments: 7,
				videoEvery:     2,
			})
			counts.log(t)

			// Nothing the link delivered is discarded, and nothing is retransmitted.
			assert.GreaterOrEqual(t, counts.messagesRecv, counts.messagesSent-counts.dataDropped,
				"a message that crossed the link was discarded")
			assert.LessOrEqual(t, counts.dataPackets, counts.messagesSent, "a message was retransmitted")

			// At most one FORWARD-TSN per loss event, plus a resend or two if one is lost.
			assert.LessOrEqual(t, counts.forwardTSNs, 2*counts.dataDropped+4,
				"FORWARD-TSN rate exceeds the loss rate")
			// The receiver acknowledges every other packet, and every packet while a
			// gap is open; FORWARD-TSN must not add an acknowledgement per packet.
			assert.LessOrEqual(t, counts.receiverSACKs, counts.dataPackets*6/10+10*counts.dataDropped+10,
				"receiver SACK rate is driven by FORWARD-TSN")

			maxLatency := tc.maxLatency
			if maxLatency == 0 {
				maxLatency = tc.oneWay + 50*time.Millisecond
			}
			assert.Less(t, counts.latency(.99), maxLatency, "a message waited for something")
			// A lost chunk is skipped within a few round trips: detected after one,
			// acknowledged as skipped after the next.
			assert.Less(t, counts.maxAckLag, 8*tc.oneWay+200*time.Millisecond, "the peer's cumulative TSN fell behind")
			assert.LessOrEqual(t, counts.t3Timeouts, uint64(1), "lost messages were skipped on retransmission timeouts")
		})
	}
}
