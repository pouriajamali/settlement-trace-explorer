# Settlement Trace Explorer

Replay a payment observation alongside request-bound service evidence, without sending money.

P0 selected one offline, synthetic LI.FI `/v1/status` profile. The project will preserve provider-native payloads and keep route outcome, observed asset delivery, refund, and reported service completion separate. A service receipt is a declaration, not cryptographic verification or a quality guarantee.

The source preview includes a pure .NET library, CLI and generated offline viewer. See [the P0 decision](docs/P0-decision.md), [contract v1](docs/contract-v1.md), and [phase evidence](docs/validation.md) for tested scope and remaining gates.

## Run the offline demo

Source prerequisites: exact .NET SDK **10.0.303** selected by global.json, plus Python 3 for the independent corpus and CLI readback tools. No external NuGet packages or package feeds are needed. The fixture corpus is checked in; regeneration is optional.

```sh
dotnet run --project tests/TraceChecks -c Release -- fixtures/cases.json artifacts
dotnet run --project src/TraceCli -c Release -- replay artifacts/demo.json --html artifacts/replay.html
```

Open `artifacts/replay.html`. Expected: route `completed`, delivery `reportedAtLeastMinimum`, service `reportedFailed`. Those are separate claims. The entire original trace and evidence IDs are visible in the same result. Use a fresh HTML output path: existing files are deliberately refused. A prebuilt view is in [demo/index.html](demo/index.html), with [input](demo/trace.json) and [runner result](demo/result.json); download/open locally to view it, because GitHub's source browser does not execute HTML.

For a framework-dependent local bundle and black-box CLI verification:

```sh
dotnet publish src/TraceCli -c Release --no-self-contained -p:UseAppHost=false -o artifacts/package
python tools/check_cli.py
dotnet artifacts/package/TraceCli.dll replay demo/trace.json
```

The DLL bundle requires a .NET 10 runtime. It is not self-contained and no downloadable release or cross-platform execution is claimed yet. CLI exit codes: 0 for a valid trace (including reported failures/unknowns), 2 for invalid input/usage, 3 for file I/O. A valid trace exit code does not mean payment or service success.

## Consume the library

Reference `src/SettlementTrace/SettlementTrace.csproj` from a .NET 10 application:

```csharp
var result = SettlementTrace.Replay.Run(File.ReadAllBytes("trace.json"));
Console.WriteLine(SettlementTrace.Replay.ToJson(result));
```

`Run` is pure and performs no I/O. The CLI reads one bounded file and optionally writes a new HTML file. No live integration, database, retry loop, wallet, verifier or model call is hidden behind the demo.

## Scope

This project extends an integration workflow with an evidence trace. LI.FI already documents partial/refunded outcomes, quoted versus executed amounts, and unknown-hash reconciliation. Those are existing capabilities, not claimed inventions here. The additional artifact joins independently supplied service evidence to the original intent and makes missing or conflicting evidence visible.

No wallet, bridge, transfer sender, general ledger or production accounting service is included. Fixtures are original synthetic data and require no account, key, money or network.

## Roadmap

P0: scoped evidence/reuse decision. P1: versioned contract and independent fixtures. P2: offline .NET CLI and shared-result viewer. P3: first-profile reusable API/adapters. P4: fair AI-versus-rule evaluation if a measurable gap justifies it. P5: bounded persistence/recovery evidence. P6: demand-driven adapters, migration and independent usage.

RPC freshness/reorg evidence (CMP-01), ledger/recovery boundaries (CMP-02), and conformance profiles (CMP-03) stay within this repository. Service-receipt verification (CMP-06) is owned externally; dataset admission (CMP-05) is optional. Roadmap items are not implemented support claims.

MIT licensed. Developed with AI assistance as independent public portfolio work.
