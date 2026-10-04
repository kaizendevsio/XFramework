package main

import (
	"encoding/binary"
	"errors"
	"fmt"
	"io"
)

// Message types on the local socket between the .NET host and this process. Every message is
// [u32 little-endian length of type+payload][u8 type][payload]. One socket carries one peer.
const (
	msgHello       byte = 0x01 // host -> sidecar, JSON hello
	msgSDP         byte = 0x02 // both ways, JSON {type, sdp}
	msgCandidate   byte = 0x03 // both ways, JSON {candidate, sdpMid, sdpMLineIndex}; empty candidate = end of candidates
	msgState       byte = 0x04 // sidecar -> host, JSON state
	msgData        byte = 0x05 // both ways, one data channel message
	msgBuffered    byte = 0x06 // sidecar -> host, binary [u32 buffered][u32 cwnd][u32 srttMs][u32 dropped]
	msgCreateOffer byte = 0x07 // host -> sidecar, JSON {iceRestart}
	msgError       byte = 0x08 // sidecar -> host, JSON {message}
	msgClose       byte = 0x09 // host -> sidecar, no payload
)

// maxFrame bounds one local message: an SDP or a data channel message, never more.
const maxFrame = 64 * 1024

var errFrameTooLarge = errors.New("local frame too large")

type frame struct {
	kind    byte
	payload []byte
}

func readFrame(r io.Reader, header []byte) (frame, error) {
	if _, err := io.ReadFull(r, header[:4]); err != nil {
		return frame{}, err
	}
	length := binary.LittleEndian.Uint32(header[:4])
	if length == 0 || length > maxFrame+1 {
		return frame{}, fmt.Errorf("%w: %d", errFrameTooLarge, length)
	}
	buffer := make([]byte, length)
	if _, err := io.ReadFull(r, buffer); err != nil {
		return frame{}, err
	}
	return frame{kind: buffer[0], payload: buffer[1:]}, nil
}

func encodeFrame(kind byte, payload []byte) []byte {
	out := make([]byte, 5+len(payload))
	binary.LittleEndian.PutUint32(out, uint32(1+len(payload)))
	out[4] = kind
	copy(out[5:], payload)
	return out
}

func encodeBuffered(buffered, cwnd, srttMs, dropped uint32) []byte {
	payload := make([]byte, 16)
	binary.LittleEndian.PutUint32(payload[0:], buffered)
	binary.LittleEndian.PutUint32(payload[4:], cwnd)
	binary.LittleEndian.PutUint32(payload[8:], srttMs)
	binary.LittleEndian.PutUint32(payload[12:], dropped)
	return payload
}

type iceServer struct {
	URLs       []string `json:"urls"`
	Username   string   `json:"username,omitempty"`
	Credential string   `json:"credential,omitempty"`
}

type hello struct {
	Token           string      `json:"token"`
	Role            string      `json:"role"` // "answer" (the relay) or "offer" (a test client)
	ICEServers      []iceServer `json:"iceServers"`
	RelayOnly       bool        `json:"relayOnly"`
	MaxMessageBytes int         `json:"maxMessageBytes"`
	MinCwnd         uint32      `json:"minCwnd"`
	AllowLoopback   bool        `json:"allowLoopback"`
	// TurnFlows is how many sockets each UDP TURN allocation starts from (0: defaultTurnFlows; 1: pion's own single
	// socket, for comparison in tests).
	TurnFlows int `json:"turnFlows,omitempty"`
}

type sdpMessage struct {
	Type string `json:"type"`
	SDP  string `json:"sdp"`
}

type candidateMessage struct {
	Candidate     string  `json:"candidate"`
	SDPMid        *string `json:"sdpMid,omitempty"`
	SDPMLineIndex *uint16 `json:"sdpMLineIndex,omitempty"`
}

type pathInfo struct {
	Local         string  `json:"local"`
	LocalProtocol string  `json:"localProtocol"`
	RelayProtocol string  `json:"relayProtocol,omitempty"`
	Remote        string  `json:"remote"`
	RttMs         float64 `json:"rttMs"`
}

type stateMessage struct {
	ICE     string    `json:"ice"`
	Peer    string    `json:"peer"`
	Channel string    `json:"channel"`
	Path    *pathInfo `json:"path,omitempty"`
}

type offerRequest struct {
	ICERestart bool `json:"iceRestart"`
}

type errorMessage struct {
	Message string `json:"message"`
}
