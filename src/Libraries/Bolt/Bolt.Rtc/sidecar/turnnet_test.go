package main

import (
	"encoding/json"
	"fmt"
	"hash/fnv"
	"math/rand"
	"net"
	"strconv"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/pion/logging"
	"github.com/pion/transport/v5/stdnet"
	"github.com/pion/turn/v5"
)

const testTurnSecret = "bolt-rtc-test-secret"

// blackholeConn is a TURN server's UDP socket behind a network that silently drops some flows: every datagram from a
// source the drop function picks is lost, consistently for that source (in production, 75-94% of new UDP flows from a
// residential ISP to Cloudflare's TURN anycast were never answered, each flow all or nothing).
type blackholeConn struct {
	net.PacketConn
	drop func(net.Addr) bool
}

func (c *blackholeConn) ReadFrom(p []byte) (int, net.Addr, error) {
	for {
		n, addr, err := c.PacketConn.ReadFrom(p)
		if err != nil || !c.drop(addr) {
			return n, addr, err
		}
	}
}

// dropHalfTheFlows drops a fixed half of all source addresses, chosen by a hash of the address.
func dropHalfTheFlows(addr net.Addr) bool {
	h := fnv.New32a()
	_, _ = h.Write([]byte(addr.String()))
	return h.Sum32()%2 == 0
}

func dropEverything(net.Addr) bool { return true }

type testTurn struct {
	udpURL string
	tcpURL string
}

// startTurn runs a pion TURN server on loopback: UDP (behind the given drop function) and TCP.
func startTurn(t *testing.T, drop func(net.Addr) bool) testTurn {
	t.Helper()
	return startTurnWrapped(t, func(conn net.PacketConn) net.PacketConn {
		if drop == nil {
			return conn
		}
		return &blackholeConn{PacketConn: conn, drop: drop}
	})
}

// startTurnWrapped runs a pion TURN server on loopback, UDP (its socket wrapped as given) and TCP.
func startTurnWrapped(t *testing.T, wrap func(net.PacketConn) net.PacketConn) testTurn {
	t.Helper()
	udp, err := net.ListenPacket("udp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	tcp, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	relay := func() turn.RelayAddressGenerator {
		return &turn.RelayAddressGeneratorStatic{RelayAddress: net.ParseIP("127.0.0.1"), Address: "127.0.0.1"}
	}
	conn := wrap(udp)
	quiet := logging.NewDefaultLoggerFactory()
	quiet.DefaultLogLevel = logging.LogLevelDisabled
	server, err := turn.NewServer(turn.ServerConfig{
		Realm:             "bolt.test",
		AuthHandler:       turn.NewLongTermAuthHandler(testTurnSecret, quiet.NewLogger("turn")),
		LoggerFactory:     quiet,
		PacketConnConfigs: []turn.PacketConnConfig{{PacketConn: conn, RelayAddressGenerator: relay()}},
		ListenerConfigs:   []turn.ListenerConfig{{Listener: tcp, RelayAddressGenerator: relay()}},
	})
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = server.Close() })
	return testTurn{
		udpURL: fmt.Sprintf("turn:%s?transport=udp", udp.LocalAddr()),
		tcpURL: fmt.Sprintf("turn:%s?transport=tcp", tcp.Addr()),
	}
}

func turnServer(t *testing.T, urls ...string) iceServer {
	t.Helper()
	username, password, err := turn.GenerateLongTermCredentials(testTurnSecret, 10*time.Minute)
	if err != nil {
		t.Fatal(err)
	}
	return iceServer{URLs: urls, Username: username, Credential: password}
}

// relayKind names a gathered candidate line's leg to TURN, from its priority's local preference (as candidateKind does).
func relayKind(candidate string) string {
	fields := strings.Fields(candidate)
	if len(fields) < 8 || fields[7] != "relay" {
		return "other"
	}
	priority, _ := strconv.ParseUint(fields[3], 10, 32)
	switch (priority >> 8) & 0xFFFF {
	case 3:
		return "relay/udp"
	case 1:
		return "relay/tcp"
	default:
		return "relay/other"
	}
}

// gather starts a relay-only offering session and returns the kinds of what it gathered, once gathering ends.
func gather(t *testing.T, servers []iceServer, flows int) ([]string, time.Duration) {
	t.Helper()
	host := startHost(t)
	host.hello("offer", func(h *hello) {
		h.RelayOnly = true
		h.ICEServers = servers
		h.TurnFlows = flows
	})
	started := time.Now()
	host.sendJSON(msgCreateOffer, offerRequest{})
	var kinds []string
	deadline := time.After(20 * time.Second)
	for {
		select {
		case f, ok := <-host.frames:
			if !ok {
				t.Error("session closed")
				return kinds, time.Since(started)
			}
			if f.kind != msgCandidate {
				continue
			}
			var candidate candidateMessage
			_ = json.Unmarshal(f.payload, &candidate)
			if candidate.Candidate == "" {
				return kinds, time.Since(started)
			}
			kinds = append(kinds, relayKind(candidate.Candidate))
		case <-deadline:
			t.Error("gathering did not end")
			return kinds, time.Since(started)
		}
	}
}

func count(kinds []string, kind string) int {
	n := 0
	for _, k := range kinds {
		if k == kind {
			n++
		}
	}
	return n
}

// The production failure: the relay's leg to TURN over UDP gathered nothing, call after call, because pion tries one
// 5-tuple per TURN URL and the network dropped most 5-tuples. Started from several flows, the allocation succeeds.
func TestATurnLegOverUdpIsGathered_WhenTheNetworkDropsMostFlows(t *testing.T) {
	server := startTurn(t, dropHalfTheFlows)
	const sessions = 10
	run := func(flows int) (gathered int) {
		var wg sync.WaitGroup
		var mu sync.Mutex
		for i := 0; i < sessions; i++ {
			wg.Add(1)
			go func() {
				defer wg.Done()
				kinds, _ := gather(t, []iceServer{turnServer(t, server.udpURL)}, flows)
				if count(kinds, "relay/udp") > 0 {
					mu.Lock()
					gathered++
					mu.Unlock()
				}
			}()
		}
		wg.Wait()
		return gathered
	}
	single := run(1)
	spread := run(defaultTurnFlows)
	t.Logf("sessions with a UDP relay candidate, half the flows dropped: one flow %d/%d, %d flows %d/%d",
		single, sessions, defaultTurnFlows, spread, sessions)
	if spread != sessions {
		t.Fatalf("every session must gather a UDP relay candidate, got %d/%d", spread, sessions)
	}
	if single == sessions {
		t.Fatal("with a single flow per allocation some sessions should have failed; the test no longer models the problem")
	}
}

// With TURN over UDP answering, a TCP/TLS leg is not gathered at all: the browser can never nominate it over UDP.
func TestATcpTurnLegIsNotGathered_WhenUdpAnswers(t *testing.T) {
	server := startTurn(t, nil)
	kinds, _ := gather(t, []iceServer{turnServer(t, server.udpURL, server.tcpURL)}, 0)
	if count(kinds, "relay/udp") != 1 || count(kinds, "relay/tcp") != 0 {
		t.Fatalf("gathered %v, want only relay/udp", kinds)
	}
}

// With TURN over UDP dead, the TCP/TLS leg is the fallback: gathered once UDP has had its chance.
func TestATcpTurnLegIsTheFallback_WhenUdpGetsNoAnswer(t *testing.T) {
	server := startTurn(t, dropEverything)
	kinds, elapsed := gather(t, []iceServer{turnServer(t, server.udpURL, server.tcpURL)}, 0)
	if count(kinds, "relay/tcp") != 1 || count(kinds, "relay/udp") != 0 {
		t.Fatalf("gathered %v, want only relay/tcp", kinds)
	}
	t.Logf("gathering ended after %v", elapsed)
}

// The flows that lost the race are closed, and later reads and writes use the winner alone.
func TestASpreadConnKeepsTheFlowThatAnsweredFirst(t *testing.T) {
	echo, err := net.ListenPacket("udp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer func() { _ = echo.Close() }()
	var answerOnly net.Addr
	var mu sync.Mutex
	go func() {
		buffer := make([]byte, 1500)
		for {
			n, from, err := echo.ReadFrom(buffer)
			if err != nil {
				return
			}
			mu.Lock()
			// Only the third source to write is ever answered.
			if answerOnly == nil && string(buffer[:n]) == "hello" {
				answerOnly = from
				mu.Unlock()
				continue
			}
			allowed := answerOnly != nil && answerOnly.String() == from.String()
			mu.Unlock()
			if allowed {
				_, _ = echo.WriteTo(buffer[:n], from)
			}
		}
	}()
	var lines []string
	var linesMu sync.Mutex
	network := newTurnNet(mustStdNet(t), 4, true, func(line string) { linesMu.Lock(); lines = append(lines, line); linesMu.Unlock() })
	conn, err := network.ListenPacket("udp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer func() { _ = conn.Close() }()
	spread := conn.(*spreadConn)
	if len(spread.conns) != 4 {
		t.Fatalf("%d flows", len(spread.conns))
	}
	// The first write picks which source the echo server will answer: the first flow it reads from.
	if _, err := conn.WriteTo([]byte("hello"), echo.LocalAddr()); err != nil {
		t.Fatal(err)
	}
	time.Sleep(100 * time.Millisecond)
	if _, err := conn.WriteTo([]byte("ping"), echo.LocalAddr()); err != nil {
		t.Fatal(err)
	}
	buffer := make([]byte, 100)
	_ = conn.SetReadDeadline(time.Now().Add(2 * time.Second))
	n, _, err := conn.ReadFrom(buffer)
	if err != nil || string(buffer[:n]) != "ping" {
		t.Fatalf("read %q, %v", buffer[:n], err)
	}
	mu.Lock()
	expected := answerOnly.String()
	mu.Unlock()
	if conn.LocalAddr().String() != expected {
		t.Fatalf("the leg is %v, but only %v was answered", conn.LocalAddr(), expected)
	}
	// After the winner is known, a write goes out on it alone and its answer is read from it directly.
	if _, err := conn.WriteTo([]byte("again"), echo.LocalAddr()); err != nil {
		t.Fatal(err)
	}
	n, _, err = conn.ReadFrom(buffer)
	if err != nil || string(buffer[:n]) != "again" {
		t.Fatalf("read %q, %v", buffer[:n], err)
	}
	time.Sleep(turnCensus + 200*time.Millisecond)
	linesMu.Lock()
	defer linesMu.Unlock()
	if len(lines) != 1 || !strings.Contains(lines[0], "1 of 4 flows answered") {
		t.Fatalf("report %q", lines)
	}
}

func mustStdNet(t *testing.T) *stdnet.Net {
	t.Helper()
	base, err := stdnet.NewNet()
	if err != nil {
		t.Fatal(err)
	}
	return base
}

// lossyConn loses a share of the datagrams through a TURN server's socket, each way, at random.
type lossyConn struct {
	net.PacketConn
	loss float64
}

func (c *lossyConn) ReadFrom(p []byte) (int, net.Addr, error) {
	for {
		n, addr, err := c.PacketConn.ReadFrom(p)
		if err != nil || rand.Float64() >= c.loss {
			return n, addr, err
		}
	}
}

func (c *lossyConn) WriteTo(p []byte, addr net.Addr) (int, error) {
	if rand.Float64() < c.loss {
		return len(p), nil
	}
	return c.PacketConn.WriteTo(p, addr)
}

// overLossyLeg connects a peer through a clean TURN server to one whose leg loses 30% of datagrams each way (about half
// of all round trips) and says when ICE and DTLS had connected both sides (0: not within 20 s).
func overLossyLeg(t *testing.T) time.Duration {
	t.Helper()
	clean := startTurn(t, nil)
	lossy := startTurnWrapped(t, func(conn net.PacketConn) net.PacketConn { return &lossyConn{PacketConn: conn, loss: 0.3} })
	offerer, answerer := startHost(t), startHost(t)
	offerer.hello("offer", func(h *hello) { h.RelayOnly = true; h.ICEServers = []iceServer{turnServer(t, clean.udpURL)} })
	answerer.hello("answer", func(h *hello) { h.RelayOnly = true; h.ICEServers = []iceServer{turnServer(t, lossy.udpURL)} })
	started := time.Now()
	offerer.sendJSON(msgCreateOffer, offerRequest{})
	connected := func(f frame) bool {
		var state stateMessage
		return f.kind == msgState && json.Unmarshal(f.payload, &state) == nil && state.Peer == "connected"
	}
	doneA, doneB := false, false
	deadline := time.After(20 * time.Second)
	for !doneA || !doneB {
		select {
		case f := <-offerer.frames:
			if f.kind == msgSDP || f.kind == msgCandidate {
				answerer.send(f.kind, f.payload)
			}
			doneA = doneA || connected(f)
		case f := <-answerer.frames:
			if f.kind == msgSDP || f.kind == msgCandidate {
				offerer.send(f.kind, f.payload)
			}
			doneB = doneB || connected(f)
		case <-deadline:
			return 0
		}
	}
	return time.Since(started)
}

// Through a TURN leg that loses half of its round trips, ICE and DTLS connect well inside the host's 15 s window. With
// pion's defaults (7 connectivity checks per pair, never reset; DTLS retransmission from 1 s, doubling) the only pair
// could fail for good before both TURN servers held permissions, or the handshake backed off past the window.
func TestIceAndDtlsConnectThroughATurnLegThatLosesHalfItsRoundTrips(t *testing.T) {
	if testing.Short() {
		t.Skip("slow")
	}
	const runs = 4
	var tuned []time.Duration
	for i := 0; i < runs; i++ {
		took := overLossyLeg(t)
		tuned = append(tuned, took)
		if took == 0 || took > 12*time.Second {
			t.Errorf("run %d: ICE and DTLS took %v (0: not within 20 s)", i, took)
		}
	}
	t.Logf("tuned: %v", tuned)
	if testing.Verbose() {
		savedChecks, savedDTLS := maxBindingRequests, dtlsRetransmission
		maxBindingRequests, dtlsRetransmission = 7, time.Second
		defer func() { maxBindingRequests, dtlsRetransmission = savedChecks, savedDTLS }()
		var before []time.Duration
		for i := 0; i < runs; i++ {
			before = append(before, overLossyLeg(t))
		}
		t.Logf("pion's defaults (0 = not within 20 s): %v", before)
	}
}
