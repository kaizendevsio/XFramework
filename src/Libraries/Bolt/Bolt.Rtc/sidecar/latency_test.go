package main

import (
	"encoding/binary"
	"math/rand"
	"net"
	"os"
	"sort"
	"strconv"
	"sync"
	"testing"
	"time"

	"github.com/pion/ice/v4"
	"github.com/pion/logging"
	"github.com/pion/transport/v5/vnet"
	"github.com/pion/webrtc/v4"
)

// lossyLink measures one-way message latency on the media channel across a virtual network with a long delay
// and random loss in the relay-to-phone direction: an unordered, unretransmitted channel must deliver every
// message that is not lost about one way after it was sent, never a round trip later.
func lossyLink(t *testing.T, oneWay time.Duration, lossPercent float64, minCwnd uint32, duration time.Duration) []time.Duration {
	t.Helper()
	router, err := vnet.NewRouter(&vnet.RouterConfig{CIDR: "10.0.0.0/24", MinDelay: oneWay, QueueSize: 100000, LoggerFactory: logging.NewDefaultLoggerFactory()})
	if err != nil {
		t.Fatal(err)
	}
	relayNet, _ := vnet.NewNet(&vnet.NetConfig{StaticIPs: []string{"10.0.0.1"}})
	phoneNet, _ := vnet.NewNet(&vnet.NetConfig{StaticIPs: []string{"10.0.0.2"}})
	if err := router.AddNet(relayNet); err != nil {
		t.Fatal(err)
	}
	if err := router.AddNet(phoneNet); err != nil {
		t.Fatal(err)
	}
	random := rand.New(rand.NewSource(7))
	var lossMu sync.Mutex
	router.AddChunkFilter(func(c vnet.Chunk) bool {
		if c.SourceAddr().(*net.UDPAddr).IP.String() != "10.0.0.1" {
			return true
		}
		lossMu.Lock()
		defer lossMu.Unlock()
		return random.Float64()*100 >= lossPercent
	})
	if err := router.Start(); err != nil {
		t.Fatal(err)
	}
	defer func() { _ = router.Stop() }()

	peer := func(network *vnet.Net, cwnd uint32, opens bool) (*webrtc.PeerConnection, *webrtc.DataChannel) {
		settings := webrtc.SettingEngine{}
		settings.SetNet(network)
		settings.SetICEMulticastDNSMode(ice.MulticastDNSModeDisabled)
		settings.EnableSCTPZeroChecksum(true)
		settings.SetICETimeouts(5*time.Second, 15*time.Second, 2*time.Second)
		if cwnd > 0 {
			settings.SetSCTPMinCwnd(cwnd)
		}
		pc, err := webrtc.NewAPI(webrtc.WithSettingEngine(settings)).NewPeerConnection(webrtc.Configuration{})
		if err != nil {
			t.Fatal(err)
		}
		if !opens {
			return pc, nil
		}
		ordered := false
		var retransmits uint16 = 0
		dc, err := pc.CreateDataChannel("bolt-media", &webrtc.DataChannelInit{Ordered: &ordered, MaxRetransmits: &retransmits})
		if err != nil {
			t.Fatal(err)
		}
		return pc, dc
	}
	// As in a call: the phone opens the channel, the relay accepts it and sends media on it.
	relay, _ := peer(relayNet, minCwnd, false)
	phone, phoneChannel := peer(phoneNet, 0, true)
	defer func() { _ = relay.Close(); _ = phone.Close() }()

	accepted := make(chan *webrtc.DataChannel, 1)
	relay.OnDataChannel(func(dc *webrtc.DataChannel) { dc.OnOpen(func() { accepted <- dc }) })
	var mu sync.Mutex
	var latencies []time.Duration
	phoneChannel.OnMessage(func(message webrtc.DataChannelMessage) {
		sent := time.Unix(0, int64(binary.LittleEndian.Uint64(message.Data)))
		mu.Lock()
		latencies = append(latencies, time.Since(sent))
		mu.Unlock()
	})
	relay.OnICECandidate(func(c *webrtc.ICECandidate) {
		if c != nil {
			_ = phone.AddICECandidate(c.ToJSON())
		}
	})
	phone.OnICECandidate(func(c *webrtc.ICECandidate) {
		if c != nil {
			_ = relay.AddICECandidate(c.ToJSON())
		}
	})
	offer, _ := phone.CreateOffer(nil)
	_ = phone.SetLocalDescription(offer)
	_ = relay.SetRemoteDescription(offer)
	answer, _ := relay.CreateAnswer(nil)
	_ = relay.SetLocalDescription(answer)
	_ = phone.SetRemoteDescription(answer)
	var relayChannel *webrtc.DataChannel
	select {
	case relayChannel = <-accepted:
	case <-time.After(30 * time.Second):
		t.Fatal("channel did not open")
	}

	// Voice: a 400-byte frame every 20 ms. Video: a 1100-byte fragment burst of 6 every 80 ms.
	payload := make([]byte, 400)
	fragment := make([]byte, 1100)
	stop := time.Now().Add(duration)
	tick := time.NewTicker(20 * time.Millisecond)
	defer tick.Stop()
	for n := 0; time.Now().Before(stop); n++ {
		<-tick.C
		binary.LittleEndian.PutUint64(payload, uint64(time.Now().UnixNano()))
		_ = relayChannel.Send(payload)
		if n%4 == 0 {
			for i := 0; i < 6; i++ {
				binary.LittleEndian.PutUint64(fragment, uint64(time.Now().UnixNano()))
				_ = relayChannel.Send(fragment)
			}
		}
	}
	time.Sleep(3 * oneWay)
	mu.Lock()
	defer mu.Unlock()
	return append([]time.Duration(nil), latencies...)
}

func percentile(values []time.Duration, q float64) time.Duration {
	if len(values) == 0 {
		return -1
	}
	sorted := append([]time.Duration(nil), values...)
	sort.Slice(sorted, func(i, j int) bool { return sorted[i] < sorted[j] })
	return sorted[int(float64(len(sorted)-1)*q)]
}

// TestUnreliableChannelNeverWaitsARoundTrip is the property the relay relies on: at 500 ms one way with 3% loss,
// what arrives arrives about one way later. It takes ~30 s of wall time, so it runs only when asked
// (BOLT_RTC_LATENCY=1, as CI does).
func TestUnreliableChannelNeverWaitsARoundTrip(t *testing.T) {
	if os.Getenv("BOLT_RTC_LATENCY") != "1" {
		t.Skip("set BOLT_RTC_LATENCY=1")
	}
	oneWay := 500 * time.Millisecond
	loss := 3.0
	if value, err := strconv.ParseFloat(os.Getenv("BOLT_RTC_LOSS"), 64); err == nil {
		loss = value
	}
	latencies := lossyLink(t, oneWay, loss, 128*1024, 20*time.Second)
	late := 0
	for _, latency := range latencies {
		if latency > oneWay+300*time.Millisecond {
			late++
		}
	}
	t.Logf("messages=%d p50=%v p90=%v p99=%v max=%v late(>%v)=%d",
		len(latencies), percentile(latencies, .5), percentile(latencies, .9), percentile(latencies, .99), percentile(latencies, 1), oneWay+300*time.Millisecond, late)
	if p99 := percentile(latencies, .99); p99 > oneWay+300*time.Millisecond {
		t.Fatalf("p99 %v: messages waited for something on an unreliable, unordered channel", p99)
	}
}
