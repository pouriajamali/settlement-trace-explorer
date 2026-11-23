# P3 integration gate

Decision: **BLOCKED for the full phase**, while the read-only first-profile API/CLI remains usable. This does not change implementation authorization and does not request routine approval to start.

The full P3 charter includes CMP-02 ledger/recovery compatibility. An intent digest, a duplicate-event check and a provider-reported refund are not a financial receipt, atomic reservation or recovery effect. The current API therefore continues to emit `ledger=notIntegrated` and `resend=notAuthorized`.

## Executed compatibility probe

Inspected Financial Reconciliation Ledger commit `36cfa2f042a50b2bffa9680dca09dd1d311fa430`, `src/Ledger/Models.cs` blob `9a89a91c7cee1cda7e0507e574571c73950c8378`. Ran that unmodified parser in an isolated net10.0 harness, with its MIT license retained locally. This is a source compatibility experiment, not the full upstream project's net11 build, database implementation or CI.

| Input | Expected | Actual |
|---|---|---|
| Native LAB, scale2, balanced cash/clearing | Accepted synthetic post | Accepted synthetic post |
| Synthetic token identifier and scale6 | POST_PROFILE_UNIT_INVALID | POST_PROFILE_UNIT_INVALID |
| LAB with scale6 | POST_PROFILE_UNIT_INVALID | POST_PROFILE_UNIT_INVALID |
| Extra chain identity | JSON_FIELDS_INVALID | JSON_FIELDS_INVALID |
| uint256 maximum debit | INTEGER_ENCODING_INVALID | INTEGER_ENCODING_INVALID |

All five expectations matched. These are valid limits of a deliberately synthetic ledger, not upstream defects. Rewriting the asset as LAB, rescaling units without a contract, omitting chain identity, or truncating uint256 to long would break the selected trace profile. Trading Execution Lab's GateEvent/exposure API and audit-only outbox likewise cannot be labelled a transfer or payment ledger.

## Required next technical work

Select a token/chain/precision-aware adapter route. If it extends a shared baseline, identify its single owner and versioned contract before changing it, then validate affected consumers. If an alternate adapter is selected, audit its license, semantics, exact arithmetic, transaction/identity behavior and recovery oracle. Keep the demo independent through fixtures in the meantime. No general-purpose replacement ledger or forced dependency on six live services is justified by this failure.

P3 acceptance still requires the first-profile CMP-01/02/03 boundaries with versioned consumer examples; reservation concurrency belongs to its own storage test, not a sequential simulation. Later P4 AI comparison, P5 actual accounting/recovery and P6 demand/independent consumption gates remain planned and unsatisfied. This preview does not complete the complement roadmap.
