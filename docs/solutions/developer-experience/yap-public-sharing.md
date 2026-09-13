# Yap public sharing

Yap is shared at **https://xeon-dev.tailed40e.ts.net:8443/** using Tailscale Funnel.
Friends can open it without Tailscale, register, and sign in. Chat APIs still require
the normal Yap session and conversation membership. The public site and private
port 5188 use the same backend; browser offline storage is separate per origin.

Only port 8443 is public, proxying to `http://127.0.0.1:5188`. The existing listeners
on 443 (another app), 5188 (private Yap), 7000, and 8261 remain tailnet-only.

The opt-in setup is `scripts/configure-yap-public-ingress.sh`, run on xeon-dev with
the existing Tailscale operator account or sudo. It refuses to replace another
service on 8443. Funnel runs in the background and survives host restarts. The
regular deploy script manages private port 5188 and leaves this public route intact;
there is no need to enable Funnel on each deployment.

To stop public sharing, run `tailscale funnel --https=8443 off` on xeon-dev.
Do not use `tailscale funnel reset`, which would affect other listeners.

Verification on 13 September 2026: requests explicitly resolved to the public
Funnel relay (instead of the tailnet IP) returned HTTP 200 for `/` and HTTP 401 for
anonymous `/api/chat/conversations`. Tailscale reported only 8443 as Funnel-enabled.

[Tailscale Funnel documentation](https://tailscale.com/docs/features/tailscale-funnel)
describes supported public ports, HTTPS requirements, and bandwidth limits.
