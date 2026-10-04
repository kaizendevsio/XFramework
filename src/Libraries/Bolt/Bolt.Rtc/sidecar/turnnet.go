package main

import (
	"errors"
	"fmt"
	"net"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	"github.com/pion/transport/v5"
)

const (
	// defaultTurnFlows is how many sockets one UDP TURN allocation starts from. Some networks black-hole most UDP
	// 5-tuples to a TURN anycast address while passing others (in production, from a residential ISP behind carrier-grade
	// NAT, 75-94% of new flows to Cloudflare's TURN anycast got no answer, each flow consistently, while TCP and IPv6 always
	// worked). pion sends the Allocate from one socket and retransmits on that same 5-tuple, so a dead flow is dead for all
	// seven tries. Sixteen flows, of which the first to answer is kept, leave a 1-in-60 chance of none at 23% good flows.
	defaultTurnFlows = 16
	// defaultTCPFallbackAfter is how long a TURN-over-TCP or TLS leg waits for TURN over UDP to answer before it is
	// gathered anyway. When UDP answers first the TCP/TLS leg is not gathered at all, so the browser can never nominate it
	// over a working UDP leg (WebKit did, run 37125841243).
	defaultTCPFallbackAfter = 2500 * time.Millisecond
	// turnCensus is how long the flows that lost the race stay open after the first answer, to count how many of them
	// would have answered too (for the log, which says how lossy the path is). They only ever carried unauthenticated
	// Allocate requests, which a TURN server answers statelessly (401), so nothing is left behind on the server.
	turnCensus = time.Second
	// recentUDPAnswer is how long an answer over UDP counts against a TCP/TLS leg (an ICE restart gathers again).
	recentUDPAnswer = 30 * time.Second
)

var errTCPFallbackNotNeeded = errors.New("TURN over UDP answered; no TCP/TLS leg needed")

// turnNet is the network one peer connection's ICE agent gathers through. It changes two things, both only for TURN
// (pion's ICE agent calls ListenPacket only for a UDP TURN leg and DialTCP only for a TCP/TLS TURN leg):
//   - a UDP TURN leg starts from several sockets at once and keeps the first one the server answers (spreadConn);
//   - a TCP/TLS TURN leg is a fallback: it is dialed only if no UDP TURN leg answers within tcpFallbackAfter.
//
// Everything else goes to the underlying network unchanged. It reports what happened per TURN server address
// (never a credential: it never sees one) through report.
type turnNet struct {
	transport.Net
	flows            int
	expectUDP        bool
	tcpFallbackAfter time.Duration
	report           func(string)

	mu         sync.Mutex
	changed    chan struct{}
	answeredAt time.Time
}

func newTurnNet(base transport.Net, flows int, expectUDP bool, report func(string)) *turnNet {
	if flows < 1 {
		flows = 1
	}
	if report == nil {
		report = func(string) {}
	}
	return &turnNet{Net: base, flows: flows, expectUDP: expectUDP, tcpFallbackAfter: defaultTCPFallbackAfter, report: report,
		changed: make(chan struct{})}
}

// notify wakes whoever waits on the UDP outcome. The caller holds mu.
func (n *turnNet) notify() {
	close(n.changed)
	n.changed = make(chan struct{})
}

func (n *turnNet) udpAnswered() {
	n.mu.Lock()
	n.answeredAt = time.Now()
	n.notify()
	n.mu.Unlock()
}

// ListenPacket opens a UDP TURN leg: one socket per flow, all bound like the one pion asked for.
func (n *turnNet) ListenPacket(network, address string) (net.PacketConn, error) {
	if n.flows == 1 || !strings.HasPrefix(network, "udp") || !strings.HasSuffix(address, ":0") {
		return n.Net.ListenPacket(network, address)
	}
	conns := make([]net.PacketConn, 0, n.flows)
	for len(conns) < n.flows {
		conn, err := n.Net.ListenPacket(network, address)
		if err != nil {
			if len(conns) == 0 {
				return nil, err
			}
			break
		}
		conns = append(conns, conn)
	}
	return newSpreadConn(n, network, conns), nil
}

// DialTCP dials a TCP or TLS TURN leg, but only as a fallback: while TURN over UDP might still answer it waits, and if
// UDP answers it gives up, so pion gathers no TCP/TLS candidate at all.
func (n *turnNet) DialTCP(network string, laddr, raddr *net.TCPAddr) (transport.TCPConn, error) {
	if !n.expectUDP {
		return n.Net.DialTCP(network, laddr, raddr)
	}
	started := time.Now()
	for {
		n.mu.Lock()
		answered := !n.answeredAt.IsZero() && time.Since(n.answeredAt) < recentUDPAnswer
		changed := n.changed
		n.mu.Unlock()
		if answered {
			n.report(fmt.Sprintf("TURN %s %s skipped: TURN over UDP answered", network, raddr))
			return nil, errTCPFallbackNotNeeded
		}
		remaining := n.tcpFallbackAfter - time.Since(started)
		if remaining <= 0 {
			break
		}
		select {
		case <-changed:
		case <-time.After(remaining):
		}
	}
	n.report(fmt.Sprintf("TURN %s %s: no answer over UDP in %d ms, using it as the fallback", network, raddr, time.Since(started).Milliseconds()))
	return n.Net.DialTCP(network, laddr, raddr)
}

type spreadPacket struct {
	data []byte
	addr net.Addr
}

// spreadConn is one UDP TURN leg started from several sockets ("flows"). Until the TURN server answers, every write goes
// out on every flow; the first flow that receives anything becomes the leg, and every later read and write uses it
// alone. Only the first request of an allocation (unauthenticated, answered statelessly with a 401) and its
// retransmissions are ever sent on more than one flow.
type spreadConn struct {
	owner   *turnNet
	network string
	conns   []net.PacketConn
	started time.Time

	in        chan spreadPacket
	handoff   chan struct{}
	closed    chan struct{}
	closeOnce sync.Once

	winner   atomic.Int32
	firstMs  atomic.Int64 // when the first answer came, after the first send
	answered atomic.Int32 // flows that received anything, the winner included
	sends    atomic.Int32 // write rounds before a winner was known
	dest     atomic.Value // net.Addr: the TURN server
	writeErr atomic.Value // string: the last error writing to every flow
	reported atomic.Bool
}

func newSpreadConn(owner *turnNet, network string, conns []net.PacketConn) *spreadConn {
	s := &spreadConn{owner: owner, network: network, conns: conns, started: time.Now(),
		in: make(chan spreadPacket, 16), handoff: make(chan struct{}), closed: make(chan struct{})}
	s.winner.Store(-1)
	for i := range conns {
		go s.readFlow(i)
	}
	return s
}

func (s *spreadConn) readFlow(i int) {
	buffer := make([]byte, 64*1024)
	counted := false
	for {
		n, addr, err := s.conns[i].ReadFrom(buffer)
		if err != nil {
			return
		}
		if !counted {
			counted = true
			s.answered.Add(1)
		}
		if s.winner.CompareAndSwap(-1, int32(i)) {
			s.firstMs.Store(time.Since(s.started).Milliseconds())
			s.owner.udpAnswered()
			time.AfterFunc(turnCensus, s.endCensus)
		}
		if s.winner.Load() != int32(i) {
			continue // Another flow won: this answer only counts for the census.
		}
		packet := spreadPacket{data: append([]byte(nil), buffer[:n]...), addr: addr}
		select {
		case s.in <- packet:
		case <-s.closed:
			return
		}
		// From here on ReadFrom reads the winning socket itself.
		close(s.handoff)
		return
	}
}

// endCensus closes the flows that lost and says how the race went.
func (s *spreadConn) endCensus() {
	w := int(s.winner.Load())
	for i, conn := range s.conns {
		if i != w {
			_ = conn.Close()
		}
	}
	s.reportOnce(fmt.Sprintf("TURN %s %s: flow %d of %d answered first after %d ms; %d of %d flows answered",
		s.network, s.destination(), w+1, len(s.conns), s.firstMs.Load(), s.answered.Load(), len(s.conns)))
}

func (s *spreadConn) destination() string {
	if addr, ok := s.dest.Load().(net.Addr); ok {
		return addr.String()
	}
	return "(nothing sent)"
}

func (s *spreadConn) reportOnce(line string) {
	if s.reported.CompareAndSwap(false, true) {
		s.owner.report(line)
	}
}

func (s *spreadConn) ReadFrom(p []byte) (int, net.Addr, error) {
	for {
		select {
		case packet := <-s.in:
			return copy(p, packet.data), packet.addr, nil
		default:
		}
		select {
		case packet := <-s.in:
			return copy(p, packet.data), packet.addr, nil
		case <-s.handoff:
			select {
			case packet := <-s.in:
				return copy(p, packet.data), packet.addr, nil
			default:
			}
			return s.conns[s.winner.Load()].ReadFrom(p)
		case <-s.closed:
			return 0, nil, net.ErrClosed
		}
	}
}

func (s *spreadConn) WriteTo(p []byte, addr net.Addr) (int, error) {
	if w := s.winner.Load(); w >= 0 {
		return s.conns[w].WriteTo(p, addr)
	}
	if s.dest.Load() == nil {
		s.dest.Store(addr)
	}
	s.sends.Add(1)
	var lastErr error
	sent := false
	for _, conn := range s.conns {
		if _, err := conn.WriteTo(p, addr); err != nil {
			lastErr = err
		} else {
			sent = true
		}
	}
	if !sent {
		s.writeErr.Store(lastErr.Error())
		return 0, lastErr
	}
	return len(p), nil
}

func (s *spreadConn) Close() error {
	s.closeOnce.Do(func() {
		close(s.closed)
		for _, conn := range s.conns {
			_ = conn.Close()
		}
		if s.winner.Load() < 0 {
			reason := fmt.Sprintf("none of %d flows answered in %.1f s (%d sends on each)", len(s.conns), time.Since(s.started).Seconds(), s.sends.Load())
			if message, ok := s.writeErr.Load().(string); ok {
				reason = "could not send: " + message
			}
			s.reportOnce(fmt.Sprintf("TURN %s %s: %s", s.network, s.destination(), reason))
		}
	})
	return nil
}

func (s *spreadConn) LocalAddr() net.Addr {
	if w := s.winner.Load(); w >= 0 {
		return s.conns[w].LocalAddr()
	}
	return s.conns[0].LocalAddr()
}

func (s *spreadConn) SetDeadline(t time.Time) error {
	for _, conn := range s.conns {
		_ = conn.SetDeadline(t)
	}
	return nil
}

func (s *spreadConn) SetReadDeadline(t time.Time) error {
	for _, conn := range s.conns {
		_ = conn.SetReadDeadline(t)
	}
	return nil
}

func (s *spreadConn) SetWriteDeadline(t time.Time) error {
	for _, conn := range s.conns {
		_ = conn.SetWriteDeadline(t)
	}
	return nil
}
