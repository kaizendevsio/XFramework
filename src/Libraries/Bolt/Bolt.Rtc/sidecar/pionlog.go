package main

import (
	"fmt"
	"strings"
	"sync/atomic"

	"github.com/pion/logging"
)

// pionLogLimit bounds how many of pion's own lines one session may write: enough for every TURN leg's failure, never a
// flood from a misbehaving peer.
const pionLogLimit = 40

// pionLogs forwards pion's warnings and errors for ICE and the TURN client (gathering, allocation, keepalive) to the
// session's log, and only errors from everything else. pion's TURN and ICE messages carry server addresses and error
// texts, never a credential; the session's TURN usernames and passwords are scrubbed anyway, should one ever appear.
type pionLogs struct {
	session uint64
	print   func(string)
	secrets []string
	written atomic.Int32
}

func newPionLogs(session uint64, print func(string), servers []iceServer) *pionLogs {
	logs := &pionLogs{session: session, print: print}
	for _, server := range servers {
		for _, secret := range []string{server.Username, server.Credential} {
			if len(secret) >= 4 {
				logs.secrets = append(logs.secrets, secret)
			}
		}
	}
	return logs
}

func (l *pionLogs) NewLogger(scope string) logging.LeveledLogger {
	return &pionLogger{logs: l, scope: scope, warn: scope == "ice" || scope == "turnc"}
}

// pionNoise are warnings pion repeats in normal operation (checks before the other side's candidates arrive, reads on a
// candidate that is being closed); they would only use up the session's budget.
var pionNoise = []string{"Failed to ping without candidate pairs", "Failed to read from candidate", "use of closed network connection"}

func (l *pionLogs) write(scope, level, message string) {
	for _, noise := range pionNoise {
		if strings.Contains(message, noise) {
			return
		}
	}
	if l.written.Add(1) > pionLogLimit {
		return
	}
	for _, secret := range l.secrets {
		message = strings.ReplaceAll(message, secret, "***")
	}
	l.print(fmt.Sprintf("session %d: pion %s %s: %s", l.session, scope, level, strings.TrimSpace(message)))
}

type pionLogger struct {
	logs  *pionLogs
	scope string
	warn  bool
}

func (p *pionLogger) Trace(string)          {}
func (p *pionLogger) Tracef(string, ...any) {}
func (p *pionLogger) Debug(string)          {}
func (p *pionLogger) Debugf(string, ...any) {}
func (p *pionLogger) Info(string)           {}
func (p *pionLogger) Infof(string, ...any)  {}

func (p *pionLogger) Warn(message string) {
	if p.warn {
		p.logs.write(p.scope, "warning", message)
	}
}

func (p *pionLogger) Warnf(format string, args ...any) {
	if p.warn {
		p.logs.write(p.scope, "warning", fmt.Sprintf(format, args...))
	}
}

func (p *pionLogger) Error(message string) { p.logs.write(p.scope, "error", message) }

func (p *pionLogger) Errorf(format string, args ...any) {
	p.logs.write(p.scope, "error", fmt.Sprintf(format, args...))
}
