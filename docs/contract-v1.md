# Trace contract v1

Status: first-profile offline preview contract. `traceVersion` is `1.0`; `profile` is `lifi-status-observation-v1`. Unknown profile/version is rejected. This is a fixture-level observation adapter, not a declaration of live network/SDK compatibility.

## Intent and identity

`intent` contains requestId, task, model, inputDigest, expectedOutputDigest, sourceChain, destinationChain, assetId, decimals, minimumAtomic and chargeAtomic. IDs are printable ASCII (1..128 chars); digests are 64 lowercase hex chars. Source/destination are positive integer chain IDs. Amounts are decimal integer strings in atomic units (0..2^256-1), no decimal point, exponent, sign or leading zero. Decimals is 0..36 and is part of asset identity. No conversion from LAB or floating point is permitted. Zero is present data; missing is unknown.

Intent identity is SHA256 over recursively ordinal-key-sorted compact UTF-8 JSON. The output carries this digest. Reordering object properties preserves identity; changing any field changes it. This fingerprint detects content differences; it authenticates no sender. `expectedOutputDigest` is a synthetic known-output fixture binding, not an AI quality measurement.

## Events

`events` is an array of at most 256 observations. Each event contains `id`, `kind`, `observedAt` (UTC ISO timestamp ending Z) and `native` (object). Native objects are retained without field projection in output. IDs are unique except byte-equivalent canonical replay: a repeated ID with identical canonical content is ignored and counted; changed content conflicts and rejects the trace. First-seen event timestamps must not decrease. Observation order is not block/finality truth. Missing provider data and elapsed time do not prove failure.

Kinds:

* `provider`: LI.FI-shaped status/substatus, optional receiving with amount, chainId and token {address, decimals}. Only enumerated native status/substatus pairs have known route meaning. Unknown enums remain unknown. FAILED/REFUNDED and DONE/REFUNDED both mean provider-reported refund, not asset credit proof. Receipt amounts must match the declared destination/asset/decimals before comparison to minimumAtomic. Preserve unknown native fields. A later observation supersedes an earlier observation of the same kind, without deleting history.
* `freshness`: optional local declaration `state` in current/stale/expired/reorg/unavailable with native network fields. This version records the declaration; it does not infer EVM finality, rank RPCs or verify blocks. Non-current freshness invalidates an unqualified delivery interpretation.
* `service`: independently supplied synthetic receipt containing requestId, task, model, inputDigest, outputDigest, chargeAtomic, destinationChain, assetId, decimals, state and verificationType. Only `verificationType=synthetic-declaration` is understood. Request/input/output/task/model/charge/asset bindings must all match intent before reporting success/failure. `reportedSucceeded` does not mean verified execution, correct output or accepted cryptographic proof. Unsupported verification claims stay unsupported. This is a G2 consumer fixture, not an implementation/redefinition of G6 CMP-06.
* `timeout`: local polling-budget observation; never converts unknown to failed and never authorizes resend.

## Independent output axes

`route`: unknown/pending/completed/partial/refunded/failed, all provider-reported.

`delivery`: unknown/reportedAtLeastMinimum/reportedBelowMinimum/reportedAssetMismatch. Even a reported amount at least the minimum is not an independently checked chain credit. Partial route and delivery amount are separate axes; a refund amount is not destination delivery.

`service`: unknown/reportedSucceeded/reportedFailed/bindingMismatch/unsupportedClaim.

`freshness`: unassessed/current/stale/expired/reorg/unavailable/unknown.

`onChainTruth` is always `notVerified`; `ledger` is always `notIntegrated`; `resend` is always `notAuthorized`. This read-only trace API performs no reservation, accounting write, payment or resend. Unknowns never become zero.

Output also includes intentDigest, evidence event IDs for each axis, diagnostics, duplicate count, and the complete original trace. No single green success flag compresses these distinct claims.

## Input and compatibility boundaries

Input <=1 MiB, JSON depth <=16, root/intent/event schema fields fixed, no duplicate JSON property names anywhere. Unknown native fields are retained. Invalid amount/shape/time rejects the trace with an explicit input error and nonzero CLI exit. A malformed/changed duplicate ID rejects, rather than silently overriding. Schema changes require a new version, consumer examples and regression fixtures. No executable scripts/HTML from native input are run; viewer encodes all text.

## Planned boundaries beyond this contract

CMP-01 first profile supports recorded freshness declarations only; actual RPC/finality and two-endpoint tests require their own truth/observation profile. CMP-02 identity is an offline fingerprint; atomic reservation and crash recovery require a validated storage adapter. CMP-03 is one provider observation profile; CCTP/CCIP/IBC/LayerZero/Wormhole/Hyperlane are not implemented. Optional CMP-05 admission and G6's actual verifier remain separate versioned inputs. No compatibility with an unissued shared schema is claimed.
