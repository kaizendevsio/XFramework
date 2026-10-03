package main

import (
	"encoding/json"
	"testing"
	"time"
)

// An ICE restart on an open channel (what a participant does after a network change) takes the answering
// side's ICE agent back to checking: pion drops the selected pair and every local candidate (its TURN
// allocations included) and gathers again. For that whole time the data channel still reports "open", so
// the host must not judge usability by the channel alone: nothing it sends can leave until ICE is
// connected again. This pins what the sidecar reports, which the .NET side maps to "stalled".
func TestAnIceRestartReportsTheChannelOpenWhileIceIsNotConnected(t *testing.T) {
	offerer, answerer := startHost(t), startHost(t)
	offerer.hello("offer", nil)
	answerer.hello("answer", nil)
	connect(t, offerer, answerer)

	offerer.sendJSON(msgCreateOffer, offerRequest{ICERestart: true})
	var states []stateMessage
	sawNotConnected, reconnected := false, false
	deadline := time.After(20 * time.Second)
	for !reconnected {
		select {
		case f, ok := <-offerer.frames:
			if !ok {
				t.Fatal("offerer closed")
			}
			if f.kind == msgSDP || f.kind == msgCandidate {
				answerer.send(f.kind, f.payload)
			}
		case f, ok := <-answerer.frames:
			if !ok {
				t.Fatal("answerer closed")
			}
			switch f.kind {
			case msgSDP, msgCandidate:
				offerer.send(f.kind, f.payload)
			case msgState:
				var state stateMessage
				if json.Unmarshal(f.payload, &state) != nil {
					continue
				}
				states = append(states, state)
				if state.Channel == "open" && state.ICE != "connected" && state.ICE != "completed" {
					sawNotConnected = true
				}
				if sawNotConnected && state.ICE == "connected" && state.Channel == "open" {
					reconnected = true
				}
			}
		case <-deadline:
			t.Fatalf("the restart did not complete; states: %+v", states)
		}
	}
	if !sawNotConnected {
		t.Fatalf("expected an open channel with ICE not connected during the restart; states: %+v", states)
	}
	t.Logf("answerer states during the restart: %+v", states)
}
