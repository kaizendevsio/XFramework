package main

import (
	"crypto/subtle"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log"
	"net"
	"sync"
	"sync/atomic"
	"time"

	"github.com/pion/ice/v4"
	"github.com/pion/webrtc/v4"
)

const (
	defaultMaxMessage = 1200
	// A host that respects the buffered reports never comes near this; it only bounds memory.
	hardBufferedCap = 4 * 1024 * 1024
	// The media channel is negotiated out of band (both sides create it with this ID), so no DCEP
	// round trip is needed before media can flow.
	mediaChannelID    = 0
	mediaChannelLabel = "bolt-media"
	controlQueue      = 256
	dataQueue         = 2048
)

type sessionConfig struct {
	token string
}

// session is one peer connection driven over one local socket.
type session struct {
	id     uint64
	conn   net.Conn
	cfg    sessionConfig
	logger *log.Logger

	control chan []byte
	data    chan []byte
	done    chan struct{}
	once    sync.Once

	mu                sync.Mutex
	pc                *webrtc.PeerConnection
	dc                *webrtc.DataChannel
	role              string
	maxMessage        int
	remoteSet         bool
	pendingCandidates []webrtc.ICECandidateInit
	path              *pathInfo

	dropped atomic.Uint32
	cwnd    atomic.Uint32
	srttMs  atomic.Uint32
}

func newSession(id uint64, conn net.Conn, cfg sessionConfig, logger *log.Logger) *session {
	return &session{
		id: id, conn: conn, cfg: cfg, logger: logger,
		control: make(chan []byte, controlQueue),
		data:    make(chan []byte, dataQueue),
		done:    make(chan struct{}),
	}
}

func (s *session) close() {
	s.once.Do(func() {
		close(s.done)
		_ = s.conn.Close()
		s.mu.Lock()
		pc := s.pc
		s.mu.Unlock()
		if pc != nil {
			_ = pc.Close()
		}
	})
}

func (s *session) run() {
	defer s.close()
	go s.writeLoop()
	header := make([]byte, 4)
	_ = s.conn.SetReadDeadline(time.Now().Add(10 * time.Second))
	first, err := readFrame(s.conn, header)
	if err != nil || first.kind != msgHello {
		s.logger.Printf("session %d: no hello", s.id)
		return
	}
	var h hello
	if err := json.Unmarshal(first.payload, &h); err != nil {
		s.fatal("invalid hello")
		return
	}
	if subtle.ConstantTimeCompare([]byte(h.Token), []byte(s.cfg.token)) != 1 {
		s.logger.Printf("session %d: rejected hello", s.id)
		return
	}
	if err := s.start(h); err != nil {
		s.fatal(err.Error())
		return
	}
	_ = s.conn.SetReadDeadline(time.Time{})
	for {
		f, err := readFrame(s.conn, header)
		if err != nil {
			if !errors.Is(err, io.EOF) && !errors.Is(err, net.ErrClosed) {
				s.logger.Printf("session %d: local read ended: %v", s.id, err)
			}
			return
		}
		if !s.handle(f) {
			return
		}
	}
}

func (s *session) start(h hello) error {
	if h.Role != "answer" && h.Role != "offer" {
		return errors.New("unknown role")
	}
	s.role = h.Role
	s.maxMessage = h.MaxMessageBytes
	if s.maxMessage <= 0 {
		s.maxMessage = defaultMaxMessage
	}
	if s.maxMessage < 256 || s.maxMessage > 16*1024 {
		return errors.New("maxMessageBytes out of range")
	}
	if len(h.ICEServers) > 8 {
		return errors.New("too many ICE servers")
	}

	settings := webrtc.SettingEngine{}
	// Media congestion is controlled above this transport (the relay's queues and reports, the
	// sender's delay-based controller). A floor under SCTP's loss-based window keeps one random
	// loss on a long path from throttling the call below what that controller asked for.
	if h.MinCwnd > 0 {
		minCwnd := h.MinCwnd
		if minCwnd > 1024*1024 {
			minCwnd = 1024 * 1024
		}
		settings.SetSCTPMinCwnd(minCwnd)
	}
	settings.EnableSCTPZeroChecksum(true)
	settings.SetICETimeouts(5*time.Second, 15*time.Second, 2*time.Second)
	// mDNS host candidates are for browsers hiding LAN addresses; the relay never needs them, and the
	// multicast listener would be one more socket open on the host.
	settings.SetICEMulticastDNSMode(ice.MulticastDNSModeDisabled)
	if h.AllowLoopback {
		// Tests: loopback only, so a test run opens nothing on the host's real interfaces.
		settings.SetIncludeLoopbackCandidate(true)
		settings.SetIPFilter(func(ip net.IP) bool { return ip.IsLoopback() })
		settings.SetNetworkTypes([]webrtc.NetworkType{webrtc.NetworkTypeUDP4})
	}
	settings.SetSCTPMaxMessageSize(uint32(s.maxMessage))
	api := webrtc.NewAPI(webrtc.WithSettingEngine(settings))

	config := webrtc.Configuration{}
	for _, server := range h.ICEServers {
		config.ICEServers = append(config.ICEServers, webrtc.ICEServer{
			URLs: server.URLs, Username: server.Username, Credential: server.Credential,
		})
	}
	if h.RelayOnly {
		config.ICETransportPolicy = webrtc.ICETransportPolicyRelay
	}
	pc, err := api.NewPeerConnection(config)
	if err != nil {
		return fmt.Errorf("peer connection: %w", err)
	}
	negotiated := true
	ordered := false
	var id uint16 = mediaChannelID
	var retransmits uint16 = 0
	dc, err := pc.CreateDataChannel(mediaChannelLabel, &webrtc.DataChannelInit{
		Negotiated: &negotiated, ID: &id, Ordered: &ordered, MaxRetransmits: &retransmits,
	})
	if err != nil {
		_ = pc.Close()
		return fmt.Errorf("data channel: %w", err)
	}
	s.mu.Lock()
	s.pc, s.dc = pc, dc
	s.mu.Unlock()

	pc.OnICECandidate(func(candidate *webrtc.ICECandidate) {
		message := candidateMessage{}
		if candidate != nil {
			init := candidate.ToJSON()
			message = candidateMessage{Candidate: init.Candidate, SDPMid: init.SDPMid, SDPMLineIndex: init.SDPMLineIndex}
		}
		s.sendJSON(msgCandidate, message)
	})
	pc.OnICEConnectionStateChange(func(webrtc.ICEConnectionState) { s.sendState() })
	pc.OnConnectionStateChange(func(webrtc.PeerConnectionState) { s.sendState() })
	dc.SetBufferedAmountLowThreshold(16 * 1024)
	dc.OnBufferedAmountLow(func() { s.reportBuffered() })
	dc.OnOpen(func() {
		s.sendState()
		go s.reportLoop()
	})
	dc.OnClose(func() { s.sendState() })
	dc.OnMessage(func(message webrtc.DataChannelMessage) {
		if message.IsString || len(message.Data) == 0 || len(message.Data) > s.maxMessage {
			return
		}
		select {
		case s.data <- encodeFrame(msgData, message.Data):
		default:
			s.dropped.Add(1)
		}
	})
	return nil
}

// handle applies one message from the host. False ends the session.
func (s *session) handle(f frame) bool {
	switch f.kind {
	case msgData:
		s.send(f.payload)
	case msgSDP:
		var message sdpMessage
		if json.Unmarshal(f.payload, &message) != nil {
			s.fail("invalid sdp message")
			return true
		}
		if err := s.applySDP(message); err != nil {
			s.fail(err.Error())
		}
	case msgCandidate:
		var message candidateMessage
		if json.Unmarshal(f.payload, &message) != nil {
			s.fail("invalid candidate")
			return true
		}
		s.addCandidate(webrtc.ICECandidateInit{Candidate: message.Candidate, SDPMid: message.SDPMid, SDPMLineIndex: message.SDPMLineIndex})
	case msgCreateOffer:
		var request offerRequest
		_ = json.Unmarshal(f.payload, &request)
		if err := s.createOffer(request.ICERestart); err != nil {
			s.fail(err.Error())
		}
	case msgClose:
		return false
	default:
		s.fail("unknown message")
	}
	return true
}

func (s *session) send(payload []byte) {
	s.mu.Lock()
	dc := s.dc
	s.mu.Unlock()
	if dc == nil || dc.ReadyState() != webrtc.DataChannelStateOpen || len(payload) > s.maxMessage ||
		dc.BufferedAmount() > hardBufferedCap {
		s.dropped.Add(1)
		return
	}
	// Send copies into SCTP chunks; the payload is ours alone.
	if err := dc.Send(payload); err != nil {
		s.dropped.Add(1)
	}
}

func (s *session) applySDP(message sdpMessage) error {
	s.mu.Lock()
	pc := s.pc
	s.mu.Unlock()
	if pc == nil {
		return errors.New("no peer")
	}
	switch {
	case s.role == "answer" && message.Type == "offer":
		if err := pc.SetRemoteDescription(webrtc.SessionDescription{Type: webrtc.SDPTypeOffer, SDP: message.SDP}); err != nil {
			return fmt.Errorf("remote offer: %w", err)
		}
		s.flushCandidates()
		answer, err := pc.CreateAnswer(nil)
		if err != nil {
			return fmt.Errorf("answer: %w", err)
		}
		if err := pc.SetLocalDescription(answer); err != nil {
			return fmt.Errorf("local answer: %w", err)
		}
		s.sendJSON(msgSDP, sdpMessage{Type: "answer", SDP: pc.LocalDescription().SDP})
	case s.role == "offer" && message.Type == "answer":
		if err := pc.SetRemoteDescription(webrtc.SessionDescription{Type: webrtc.SDPTypeAnswer, SDP: message.SDP}); err != nil {
			return fmt.Errorf("remote answer: %w", err)
		}
		s.flushCandidates()
	default:
		return errors.New("unexpected description")
	}
	return nil
}

func (s *session) createOffer(iceRestart bool) error {
	if s.role != "offer" {
		return errors.New("not the offerer")
	}
	s.mu.Lock()
	pc := s.pc
	s.mu.Unlock()
	offer, err := pc.CreateOffer(&webrtc.OfferOptions{ICERestart: iceRestart})
	if err != nil {
		return fmt.Errorf("offer: %w", err)
	}
	if err := pc.SetLocalDescription(offer); err != nil {
		return fmt.Errorf("local offer: %w", err)
	}
	s.sendJSON(msgSDP, sdpMessage{Type: "offer", SDP: pc.LocalDescription().SDP})
	return nil
}

func (s *session) addCandidate(candidate webrtc.ICECandidateInit) {
	s.mu.Lock()
	if !s.remoteSet && s.pc.RemoteDescription() == nil {
		if len(s.pendingCandidates) < 64 {
			s.pendingCandidates = append(s.pendingCandidates, candidate)
		}
		s.mu.Unlock()
		return
	}
	pc := s.pc
	s.mu.Unlock()
	if candidate.Candidate == "" {
		return // End of candidates: nothing to add.
	}
	if err := pc.AddICECandidate(candidate); err != nil {
		s.logger.Printf("session %d: remote candidate ignored", s.id)
	}
}

func (s *session) flushCandidates() {
	s.mu.Lock()
	s.remoteSet = true
	pending := s.pendingCandidates
	s.pendingCandidates = nil
	pc := s.pc
	s.mu.Unlock()
	for _, candidate := range pending {
		if candidate.Candidate != "" {
			_ = pc.AddICECandidate(candidate)
		}
	}
}

// reportLoop keeps the host's view of the channel's buffer current while it is open, and samples
// the path (candidate types, RTT) and the SCTP window twice a second.
func (s *session) reportLoop() {
	fast := time.NewTicker(20 * time.Millisecond)
	slow := time.NewTicker(500 * time.Millisecond)
	defer fast.Stop()
	defer slow.Stop()
	var last uint64 = ^uint64(0)
	quiet := 0
	for {
		select {
		case <-s.done:
			return
		case <-fast.C:
			s.mu.Lock()
			dc := s.dc
			s.mu.Unlock()
			if dc.ReadyState() != webrtc.DataChannelStateOpen {
				return
			}
			buffered := dc.BufferedAmount()
			quiet++
			if buffered != last || quiet >= 25 {
				last = buffered
				quiet = 0
				s.reportBuffered()
			}
		case <-slow.C:
			s.sample()
		}
	}
}

func (s *session) reportBuffered() {
	s.mu.Lock()
	dc := s.dc
	s.mu.Unlock()
	if dc == nil {
		return
	}
	buffered := dc.BufferedAmount()
	if buffered > 0xFFFFFFFF {
		buffered = 0xFFFFFFFF
	}
	payload := encodeFrame(msgBuffered, encodeBuffered(uint32(buffered), s.cwnd.Load(), s.srttMs.Load(), s.dropped.Load()))
	select {
	case s.control <- payload:
	default:
	}
}

func (s *session) sample() {
	s.mu.Lock()
	pc := s.pc
	s.mu.Unlock()
	report := pc.GetStats()
	var path *pathInfo
	for _, value := range report {
		switch stat := value.(type) {
		case webrtc.SCTPTransportStats:
			s.cwnd.Store(stat.CongestionWindow)
			s.srttMs.Store(uint32(stat.SmoothedRoundTripTime * 1000))
		case webrtc.ICECandidatePairStats:
			if !stat.Nominated || stat.State != webrtc.StatsICECandidatePairStateSucceeded {
				continue
			}
			local, lok := report[stat.LocalCandidateID].(webrtc.ICECandidateStats)
			remote, rok := report[stat.RemoteCandidateID].(webrtc.ICECandidateStats)
			if !lok || !rok {
				continue
			}
			path = &pathInfo{
				Local: local.CandidateType.String(), LocalProtocol: local.Protocol, RelayProtocol: local.RelayProtocol,
				Remote: remote.CandidateType.String(), RttMs: stat.CurrentRoundTripTime * 1000,
			}
		}
	}
	if path == nil {
		return
	}
	s.mu.Lock()
	changed := s.path == nil || s.path.Local != path.Local || s.path.Remote != path.Remote ||
		s.path.RelayProtocol != path.RelayProtocol || abs(s.path.RttMs-path.RttMs) > 20
	if changed {
		s.path = path
	}
	s.mu.Unlock()
	if changed {
		s.sendState()
	}
}

func abs(value float64) float64 {
	if value < 0 {
		return -value
	}
	return value
}

func (s *session) sendState() {
	s.mu.Lock()
	pc, dc, path := s.pc, s.dc, s.path
	s.mu.Unlock()
	if pc == nil {
		return
	}
	state := stateMessage{ICE: pc.ICEConnectionState().String(), Peer: pc.ConnectionState().String(), Channel: dc.ReadyState().String(), Path: path}
	s.sendJSON(msgState, state)
}

func (s *session) fail(message string) {
	s.logger.Printf("session %d: %s", s.id, message)
	s.sendJSON(msgError, errorMessage{Message: message})
}

// fatal tells the host why the session cannot start, before the socket closes.
func (s *session) fatal(message string) {
	s.logger.Printf("session %d: %s", s.id, message)
	payload, _ := json.Marshal(errorMessage{Message: message})
	s.write(encodeFrame(msgError, payload))
}

func (s *session) sendJSON(kind byte, value any) {
	payload, err := json.Marshal(value)
	if err != nil {
		return
	}
	select {
	case s.control <- encodeFrame(kind, payload):
	case <-s.done:
	case <-time.After(5 * time.Second):
		// The host stopped reading: nothing more can be told to it.
		s.logger.Printf("session %d: host not reading, closing", s.id)
		go s.close()
	}
}

func (s *session) writeLoop() {
	for {
		// Control first: a state change must not wait behind a burst of media.
		select {
		case payload := <-s.control:
			if !s.write(payload) {
				return
			}
			continue
		default:
		}
		select {
		case payload := <-s.control:
			if !s.write(payload) {
				return
			}
		case payload := <-s.data:
			if !s.write(payload) {
				return
			}
		case <-s.done:
			return
		}
	}
}

func (s *session) write(payload []byte) bool {
	_ = s.conn.SetWriteDeadline(time.Now().Add(10 * time.Second))
	if _, err := s.conn.Write(payload); err != nil {
		go s.close()
		return false
	}
	return true
}
