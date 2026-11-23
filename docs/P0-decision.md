# P0 decision — 2026-11-23

Decision: **GO for a bounded offline evidence adapter/demo**, not a new payment SDK or ledger.

## Workflow and profile

An integrator records an intent for a service, imports a LI.FI `/v1/status` observation and independently supplies a synthetic service receipt. Profile `lifi-status-observation-v1` targets EVM-style source chain 1 and destination chain 137 for fixtures only, with explicitly synthetic asset identifiers. Amounts are unsigned decimal integer strings in declared atomic units, never floating point. Asset identity includes chain, identifier and decimals. No network support or on-chain finality is inferred from the fixture chain IDs.

Provider evidence is an observation at a recorded UTC instant. Provider-reported execution is not an independently checked chain receipt. Missing data, indexer lag, expiry and reorg reports stay explicit unknown/observation states. Unknown enum values cannot imply completion. The service result and its request/input/output/charge bindings are a separate evidence domain, with an explicit synthetic-declaration trust label.

## Existing capability and actual comparison

Read [LI.FI status guide](https://docs.li.fi/introduction/user-flows-and-examples/status-tracking) on 2026-11-23. It already explains PARTIAL/REFUNDED, quote minimum versus actual amount, unknown hashes and bounded reconciliation. This project does not claim to fix missing vendor support for those concepts.

Inspected `@lifi/sdk` source package version 4.13.0 and [getStatus.ts](https://github.com/lifinance/sdk/blob/main/packages/sdk/src/actions/getStatus.ts), blob `90549cd354a4f74fd12ccf74cec4abbf10921f9b`. Executed that isolated source function with explicit TypeScript erasure and an injected offline transport: 6/6 checks (missing identifier rejection, four native payloads preserved, query fields retained). This is not an installed SDK or real transport test. A separate synthetic identifiability probe retained the same provider observation across four different service histories: success, failure, absent receipt, wrong request. Its 2/2 assertions passed. Provider payload alone cannot identify which service history occurred; the useful extra input is a request-bound receipt.

The probes are P0 evidence, not a competitive benchmark, network conformance result or oracle for later implementation. P1 expected results will be authored independently of the runner.

## Baseline audit and reuse decisions

| Baseline | Inspected commit | Decision |
|---|---|---|
| [Financial Reconciliation Ledger](https://github.com/pouriajamali/financial-reconciliation-ledger) | `36cfa2f042a50b2bffa9680dca09dd1d311fa430` | MIT; internal parser/CLI fixes LAB scale2, cash/clearing/fees, long bounds and synthetic debit totals. Reuse identity/exact-receipt principles; do not convert LAB to token accounting. |
| [Trading Execution Lab](https://github.com/pouriajamali/trading-execution-lab) | `cae308b752bd1434849c07b64b7f28f747b2e646` | MIT; DurableGate accepts order GateEvent and exposure, commits SQLite state/receipt/audit-only outbox. Reuse effect/identity boundaries; do not map orders to transfers. |
| [Service Reliability Lab](https://github.com/pouriajamali/service-reliability-lab) | `3a06d0e4db3e45d784b9dc84c1eabaf27a6b9885` | MIT; ReadPolicy/IReadDependency retries reads, not financial writes. Offline profile requires no retry dependency. |

README, license and relevant contract/source were inspected through GitHub. No upstream build, CI or release was rerun. No upstream source is copied or package referenced. No shared baseline is changed. Live accounting and multi-worker reservations remain gated on an appropriate validated adapter; no duplicate general-purpose ledger will be introduced.

## Advancement and stopping rules

P1 must preserve native fields, exact amounts and independently specified partial/refund/unknown/timeout/duplicate/service-failure results. P2 must run without secrets/network and render the runner's own output. P3 must version the first profile and test its consumer boundary. A second profile needs its own oracle. AI must not decide resend or accounting validity; its evaluation needs a fixed baseline, holdout, equal budget, error/citation/cost/time measurements. Recovery requires actual storage/crash/duplicate tests and declared host/DB limits. External usage, demand and compatibility are required for mature maintenance/product claims. None are satisfied merely by this P0.
