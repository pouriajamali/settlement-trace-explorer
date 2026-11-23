# Validation and phase status

This source preview is tested on one local Windows host with installed SDK 10.0.303. No upstream baseline build, GitHub CI, Linux/macOS run, live chain operation or cryptographic verifier has been validated for this project.

## Reproducible local checks

```sh
python tools/check_corpus.py
dotnet run --project tests/TraceChecks -c Release -- fixtures/cases.json artifacts
dotnet publish src/TraceCli -c Release --no-self-contained -p:UseAppHost=false -o artifacts/package
python tools/check_cli.py
```

The frozen corpus contains 49 original synthetic cases, 40 valid traces and 9 explicit rejection expectations. Expectations were authored before the runner and cover native status, refund, missing/zero/large amounts, asset identity, service failure/binding/unsupported claims, freshness declarations, duplicate identity and unknown enums. The .NET harness also checks parser bounds, property duplication, canonical intent identity, deterministic replay, strict UTC timestamps and native-markup escaping. CLI checks run the published DLL from a new directory and compare parsed CLI output, library output and HTML readback. It also tests non-overwrite and nonzero error paths. A later trace is allowed to report unknown/failure; this is not test failure.

The viewer embeds the runner's JSON as encoded text and presents the same axes. It has no JavaScript, outbound fetches or third-party assets. Its content-security policy disables active sources. Automated DOM/text readback and injection checks are part of validation; no independent visual usability study is claimed.

## Gates retained

| Phase | State in this source preview | Remaining evidence |
|---|---|---|
| P0 | Bounded offline adapter/demo selected | No market/novelty/AI claim |
| P1 | Contract and independent expectation corpus | External domain review remains valuable |
| P2 | Local runner/CLI/viewer checks and fresh-DLL execution | Remote CI/release and other OS execution are separate |
| P3 | First-profile API exists; full complement gate not passed | CMP-02 token-aware accounting/recovery adapter; versioned external consumer boundaries |
| P4 | Planned | Fixed real AI comparator, holdout, equal budget, time/cost/citation/error measurements; no resend/financial authority |
| P5 | Planned | Owner-controlled accounting storage, real crash/commit/restart/duplicate/stale-worker/indexer-lag tests with oracle and DB/host limits |
| P6 | Planned | Demand-driven second profile, migration regression and independent usage; commercial pilot evidence |

## Complement boundaries

CMP-01: recorded freshness declarations and replay, not RPC/finality verification or endpoint ranking. CMP-02: intent fingerprint and duplicate detection only; no ledger integration, atomic reservation, financial effect or crash recovery. CMP-03: one provider observation profile; no implied CCTP/CCIP/IBC/LayerZero/Wormhole/Hyperlane support. CMP-06: local synthetic consumer fixtures, not G6's actual verifier or a negotiated shared schema. CMP-05: optional and not integrated. Each complement retains its P0-P6 charter; implemented fixture boundaries do not complete the full complement phases.

## Contribution and reporting

Good contributions are a paired valid/invalid fixture with independently explained expectation, an unknown native enum example, or a profile proposal with version, trust assumptions and oracle. Keep data synthetic or establish publication rights. Include CLI command, input, expected/actual result and environment. Avoid real addresses, private receipts, keys and employer/customer data.

No AI benefit, production readiness, accounting certification, financial safety, customer demand, stars or income result is asserted by these tests.
