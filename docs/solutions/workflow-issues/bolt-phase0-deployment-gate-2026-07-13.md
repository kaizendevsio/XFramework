---
title: "Bolt Phase 0 Deployment Gate"
date: 2026-07-14
category: workflow-issues
module: Bolt
problem_type: workflow
component: deployment
severity: low
applies_when:
  - "Reviewing historical Bolt Phase 0 certificate deployment work"
tags: [bolt, phase-0, deployment, historical]
status: superseded
---

# Bolt Phase 0 Deployment Gate - Superseded

The 2026-07-13 certificate-heavy deployment gate is no longer an operational instruction. Its detailed private-CA, Kestrel certificate, root watchdog, deployment lease, sealed LKG, rotation, and direct-Kestrel evidence procedures remain available in Git history.

The architecture review on 2026-07-14 reassigned encrypted exposure, certificates, DNS, and network ACLs to Tailscale and the deployment layer. Bolt Hub, IdentityServer, and application containers no longer own deployment certificate lifecycle.

Follow `docs/solutions/workflow-issues/bolt-protocol-hub-media-remediation-plan-2026-07-12.md` for the active plan. Existing fail-closed controls must be removed only through the bounded transition and rollback sequence defined there.
