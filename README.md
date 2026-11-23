# Settlement Trace Explorer

Replay a payment observation alongside request-bound service evidence, without sending money.

P0 selected one offline, synthetic LI.FI `/v1/status` profile. The project will preserve provider-native payloads and keep route outcome, observed asset delivery, refund, and reported service completion separate. A service receipt is a declaration, not cryptographic verification or a quality guarantee.

No runtime is published yet. See [the P0 decision](docs/P0-decision.md) for the comparison, reuse constraints and phase gates.

## Scope

This project extends an integration workflow with an evidence trace. LI.FI already documents partial/refunded outcomes, quoted versus executed amounts, and unknown-hash reconciliation. Those are existing capabilities, not claimed inventions here. The additional artifact joins independently supplied service evidence to the original intent and makes missing or conflicting evidence visible.

No wallet, bridge, transfer sender, general ledger or production accounting service is included. Fixtures are original synthetic data and require no account, key, money or network.

## Roadmap

P0: scoped evidence/reuse decision. P1: versioned contract and independent fixtures. P2: offline .NET CLI and shared-result viewer. P3: first-profile reusable API/adapters. P4: fair AI-versus-rule evaluation if a measurable gap justifies it. P5: bounded persistence/recovery evidence. P6: demand-driven adapters, migration and independent usage.

RPC freshness/reorg evidence (CMP-01), ledger/recovery boundaries (CMP-02), and conformance profiles (CMP-03) stay within this repository. Service-receipt verification (CMP-06) is owned externally; dataset admission (CMP-05) is optional. Roadmap items are not implemented support claims.

MIT licensed. Developed with AI assistance as independent public portfolio work.
