package main

import (
	"bytes"
	"encoding/binary"
	"encoding/json"
	"io"
	"log"
	"net"
	"strings"
	"testing"
	"time"

	"github.com/pion/webrtc/v4"
)

const testToken = "0123456789abcdef0123456789abcdef"

type testHost struct {
	t       *testing.T
	conn    net.Conn
	frames  chan frame
	session *session
}

func startHost(t *testing.T) *testHost {
	t.Helper()
	hostSide, sessionSide := net.Pipe()
	logger := log.New(io.Discard, "", 0)
	if testing.Verbose() {
		logger = log.New(&testWriter{t}, "", 0)
	}
	current := newSession(1, sessionSide, sessionConfig{token: testToken}, logger)
	go current.run()
	host := &testHost{t: t, conn: hostSide, frames: make(chan frame, 4096), session: current}
	go func() {
		header := make([]byte, 4)
		for {
			f, err := readFrame(hostSide, header)
			if err != nil {
				close(host.frames)
				return
			}
			host.frames <- f
		}
	}()
	t.Cleanup(func() { _ = hostSide.Close(); current.close() })
	return host
}

type testWriter struct{ t *testing.T }

func (w *testWriter) Write(p []byte) (int, error) { w.t.Log(strings.TrimSpace(string(p))); return len(p), nil }

func (h *testHost) send(kind byte, payload []byte) {
	h.t.Helper()
	_ = h.conn.SetWriteDeadline(time.Now().Add(5 * time.Second))
	if _, err := h.conn.Write(encodeFrame(kind, payload)); err != nil {
		h.t.Fatalf("write: %v", err)
	}
}

func (h *testHost) sendJSON(kind byte, value any) {
	payload, _ := json.Marshal(value)
	h.send(kind, payload)
}

func (h *testHost) hello(role string, extra func(*hello)) {
	value := hello{Token: testToken, Role: role, AllowLoopback: true, MaxMessageBytes: 1200}
	if extra != nil {
		extra(&value)
	}
	h.sendJSON(msgHello, value)
}

// connect runs a full offer/answer and trickle exchange between two hosts until both channels are open.
func connect(t *testing.T, offerer, answerer *testHost) {
	t.Helper()
	offerer.sendJSON(msgCreateOffer, offerRequest{})
	openA, openB := false, false
	deadline := time.After(20 * time.Second)
	for !openA || !openB {
		select {
		case f, ok := <-offerer.frames:
			if !ok {
				t.Fatal("offerer closed")
			}
			switch f.kind {
			case msgSDP, msgCandidate:
				answerer.send(f.kind, f.payload)
			case msgState:
				openA = openA || channelOpen(f.payload)
			case msgError:
				t.Fatalf("offerer error: %s", f.payload)
			}
		case f, ok := <-answerer.frames:
			if !ok {
				t.Fatal("answerer closed")
			}
			switch f.kind {
			case msgSDP, msgCandidate:
				offerer.send(f.kind, f.payload)
			case msgState:
				openB = openB || channelOpen(f.payload)
			case msgError:
				t.Fatalf("answerer error: %s", f.payload)
			}
		case <-deadline:
			t.Fatal("data channel did not open")
		}
	}
}

func channelOpen(payload []byte) bool {
	var state stateMessage
	return json.Unmarshal(payload, &state) == nil && state.Channel == "open"
}

func waitFor(t *testing.T, host *testHost, kind byte, match func([]byte) bool) []byte {
	t.Helper()
	deadline := time.After(10 * time.Second)
	for {
		select {
		case f, ok := <-host.frames:
			if !ok {
				t.Fatal("host closed")
			}
			if f.kind == kind && (match == nil || match(f.payload)) {
				return f.payload
			}
		case <-deadline:
			t.Fatalf("no message of kind %d", kind)
		}
	}
}

func TestPeersOpenAnUnorderedUnreliableChannelAndCarryMessagesBothWays(t *testing.T) {
	offerer, answerer := startHost(t), startHost(t)
	offerer.hello("offer", nil)
	answerer.hello("answer", func(h *hello) { h.MinCwnd = 128 * 1024 })
	connect(t, offerer, answerer)

	for _, current := range []*session{offerer.session, answerer.session} {
		current.mu.Lock()
		dc := current.dc
		current.mu.Unlock()
		if dc.Ordered() {
			t.Fatal("media channel must be unordered")
		}
		if dc.MaxRetransmits() == nil || *dc.MaxRetransmits() != 0 {
			t.Fatal("media channel must never retransmit")
		}
		if !dc.Negotiated() || dc.ID() == nil || *dc.ID() != mediaChannelID {
			t.Fatal("media channel must be pre-negotiated")
		}
	}

	message := bytes.Repeat([]byte{0x21}, 1100)
	offerer.send(msgData, message)
	got := waitFor(t, answerer, msgData, nil)
	if !bytes.Equal(got, message) {
		t.Fatal("answerer received a different message")
	}
	answerer.send(msgData, []byte{0x22, 1, 2, 3})
	if got := waitFor(t, offerer, msgData, nil); !bytes.Equal(got, []byte{0x22, 1, 2, 3}) {
		t.Fatal("offerer received a different message")
	}
	report := waitFor(t, answerer, msgBuffered, nil)
	if len(report) != 16 {
		t.Fatalf("buffered report is %d bytes", len(report))
	}
}

func TestOversizedMessagesAreDroppedNotSent(t *testing.T) {
	offerer, answerer := startHost(t), startHost(t)
	offerer.hello("offer", nil)
	answerer.hello("answer", nil)
	connect(t, offerer, answerer)
	offerer.send(msgData, make([]byte, 1201))
	offerer.send(msgData, []byte{0x21, 9})
	if got := waitFor(t, answerer, msgData, nil); !bytes.Equal(got, []byte{0x21, 9}) {
		t.Fatal("the oversized message must not arrive")
	}
	dropped := waitFor(t, offerer, msgBuffered, func(payload []byte) bool { return binary.LittleEndian.Uint32(payload[12:]) >= 1 })
	if binary.LittleEndian.Uint32(dropped[12:]) < 1 {
		t.Fatal("the drop must be counted")
	}
}

func TestAWrongTokenGetsNoSession(t *testing.T) {
	host := startHost(t)
	host.sendJSON(msgHello, hello{Token: strings.Repeat("x", 32), Role: "answer"})
	select {
	case _, ok := <-host.frames:
		if ok {
			t.Fatal("a rejected host must get nothing back")
		}
	case <-time.After(5 * time.Second):
		t.Fatal("the session must close")
	}
	host.session.mu.Lock()
	defer host.session.mu.Unlock()
	if host.session.pc != nil {
		t.Fatal("no peer connection may be created before authentication")
	}
}

func TestHelloIsValidated(t *testing.T) {
	cases := map[string]func(*hello){
		"role":    func(h *hello) { h.Role = "relay" },
		"message": func(h *hello) { h.MaxMessageBytes = 100 },
		"servers": func(h *hello) { h.ICEServers = make([]iceServer, 9) },
	}
	for name, mutate := range cases {
		t.Run(name, func(t *testing.T) {
			host := startHost(t)
			value := hello{Token: testToken, Role: "answer"}
			mutate(&value)
			host.sendJSON(msgHello, value)
			waitFor(t, host, msgError, nil)
		})
	}
}

func TestAnAnswererRefusesToOffer(t *testing.T) {
	host := startHost(t)
	host.hello("answer", nil)
	host.sendJSON(msgCreateOffer, offerRequest{})
	waitFor(t, host, msgError, nil)
}

func TestRelayOnlyPeersGatherNoHostCandidates(t *testing.T) {
	host := startHost(t)
	host.hello("offer", func(h *hello) { h.RelayOnly = true })
	host.sendJSON(msgCreateOffer, offerRequest{})
	offer := waitFor(t, host, msgSDP, nil)
	if strings.Contains(string(offer), "typ host") {
		t.Fatal("a relay-only peer must not offer host candidates")
	}
	// With no TURN server configured nothing can be gathered at all: the end of candidates comes at once.
	end := waitFor(t, host, msgCandidate, nil)
	var candidate candidateMessage
	_ = json.Unmarshal(end, &candidate)
	if candidate.Candidate != "" {
		t.Fatalf("unexpected candidate %q", candidate.Candidate)
	}
	host.session.mu.Lock()
	policy := host.session.pc.GetConfiguration().ICETransportPolicy
	host.session.mu.Unlock()
	if policy != webrtc.ICETransportPolicyRelay {
		t.Fatal("relay policy must be applied")
	}
}

func TestFramesAreBounded(t *testing.T) {
	var buffer bytes.Buffer
	header := make([]byte, 4)
	binary.LittleEndian.PutUint32(header, maxFrame+2)
	buffer.Write(header)
	if _, err := readFrame(&buffer, make([]byte, 4)); err == nil {
		t.Fatal("an oversized local frame must be refused")
	}
	encoded := encodeFrame(msgData, []byte{1, 2, 3})
	f, err := readFrame(bytes.NewReader(encoded), make([]byte, 4))
	if err != nil || f.kind != msgData || !bytes.Equal(f.payload, []byte{1, 2, 3}) {
		t.Fatal("frames must round-trip")
	}
}
