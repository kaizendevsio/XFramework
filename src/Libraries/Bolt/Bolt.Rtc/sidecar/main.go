// Command bolt-rtc is the WebRTC data-channel endpoint of the Bolt call relay.
//
// The relay process starts it, talks to it over a local socket (one connection per peer), and
// keeps every decision about media itself: this process only terminates ICE (through TURN),
// DTLS and SCTP, and moves unordered, unretransmitted data-channel messages between the socket
// and the peer. Media stays end-to-end encrypted (SFrame) the whole way; it never sees keys.
//
// It exits when its standard input closes (the relay went away) or on SIGTERM/SIGINT.
package main

import (
	"bufio"
	"errors"
	"flag"
	"fmt"
	"io"
	"log"
	"net"
	"os"
	"os/signal"
	"sync"
	"sync/atomic"
	"syscall"
)

func main() {
	socket := flag.String("socket", "", "path of the local socket to listen on")
	maxSessions := flag.Int("max-sessions", 256, "most peers served at once")
	flag.Parse()
	logger := log.New(os.Stderr, "bolt-rtc: ", log.LstdFlags|log.LUTC)
	if *socket == "" {
		logger.Fatal("-socket is required")
	}
	token := os.Getenv("BOLT_RTC_TOKEN")
	if len(token) < 32 {
		logger.Fatal("BOLT_RTC_TOKEN must be set by the host")
	}
	_ = os.Remove(*socket)
	listener, err := net.Listen("unix", *socket)
	if err != nil {
		logger.Fatalf("listen: %v", err)
	}
	_ = os.Chmod(*socket, 0o600)

	server := &server{cfg: sessionConfig{token: token}, logger: logger, slots: make(chan struct{}, *maxSessions), sessions: map[uint64]*session{}}
	stop := make(chan struct{})
	var stopOnce sync.Once
	shutdown := func() { stopOnce.Do(func() { close(stop); _ = listener.Close() }) }

	signals := make(chan os.Signal, 1)
	signal.Notify(signals, syscall.SIGINT, syscall.SIGTERM)
	go func() { <-signals; shutdown() }()
	// The host keeps our stdin open for as long as it lives.
	go func() { _, _ = io.Copy(io.Discard, bufio.NewReader(os.Stdin)); shutdown() }()

	fmt.Println("READY")
	go server.serve(listener)
	<-stop
	server.closeAll()
	_ = os.Remove(*socket)
}

type server struct {
	cfg      sessionConfig
	logger   *log.Logger
	slots    chan struct{}
	next     atomic.Uint64
	mu       sync.Mutex
	sessions map[uint64]*session
}

func (s *server) serve(listener net.Listener) {
	for {
		conn, err := listener.Accept()
		if err != nil {
			if !errors.Is(err, net.ErrClosed) {
				s.logger.Printf("accept: %v", err)
			}
			return
		}
		select {
		case s.slots <- struct{}{}:
		default:
			s.logger.Printf("session limit reached")
			_ = conn.Close()
			continue
		}
		id := s.next.Add(1)
		current := newSession(id, conn, s.cfg, s.logger)
		s.mu.Lock()
		s.sessions[id] = current
		s.mu.Unlock()
		go func() {
			defer func() {
				s.mu.Lock()
				delete(s.sessions, id)
				s.mu.Unlock()
				<-s.slots
			}()
			current.run()
		}()
	}
}

func (s *server) closeAll() {
	s.mu.Lock()
	sessions := make([]*session, 0, len(s.sessions))
	for _, current := range s.sessions {
		sessions = append(sessions, current)
	}
	s.mu.Unlock()
	for _, current := range sessions {
		current.close()
	}
}
