using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SettlementTrace;

public sealed class TraceInputException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record TraceResult(string ContractVersion, string Profile, string IntentDigest,
    string Route, string Delivery, string Service, string Freshness, string Ledger,
    string OnChainTruth, string Resend, int Duplicates,
    IReadOnlyDictionary<string, string?> Evidence, IReadOnlyList<string> Diagnostics, JsonElement OriginalTrace);

/// <summary>Pure, read-only evidence projection. No RPC, sender, ledger or verifier.</summary>
public static class Replay
{
    public const int InputLimit = 1024 * 1024;
    public const string Profile = "lifi-status-observation-v1";
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static readonly BigInteger Maximum = (BigInteger.One << 256) - 1;

    public static TraceResult Run(byte[] utf8)
    {
        if (utf8.Length > InputLimit) throw new TraceInputException("INPUT_LIMIT");
        try
        {
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            UniqueProperties(root);
            Fields(root, "traceVersion", "profile", "intent", "events");
            if (Text(root, "traceVersion") != "1.0" || Text(root, "profile") != Profile)
                throw new TraceInputException("PROFILE_UNSUPPORTED");
            var intent = root.GetProperty("intent");
            Fields(intent, "requestId", "task", "model", "inputDigest", "expectedOutputDigest", "sourceChain",
                "destinationChain", "assetId", "decimals", "minimumAtomic", "chargeAtomic");
            foreach (var key in new[] { "requestId", "task", "model", "assetId" }) Identifier(Text(intent, key));
            foreach (var key in new[] { "inputDigest", "expectedOutputDigest" })
                if (!Regex.IsMatch(Text(intent, key), "\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant))
                    throw new TraceInputException("DIGEST_INVALID");
            if (Int(intent, "sourceChain") < 1 || Int(intent, "destinationChain") < 1 || Int(intent, "decimals") is < 0 or > 36)
                throw new TraceInputException("ASSET_INVALID");
            var minimum = Atomic(Text(intent, "minimumAtomic"));
            _ = Atomic(Text(intent, "chargeAtomic"));
            var events = root.GetProperty("events");
            if (events.ValueKind != JsonValueKind.Array || events.GetArrayLength() > 256)
                throw new TraceInputException("EVENT_LIMIT");

            var identities = new Dictionary<string, string>(StringComparer.Ordinal);
            JsonElement? provider = null, service = null, freshness = null;
            string? providerId = null, serviceId = null, freshnessId = null;
            var diagnostics = new List<string>();
            var duplicates = 0;
            DateTimeOffset? last = null;
            foreach (var e in events.EnumerateArray())
            {
                Fields(e, "id", "kind", "observedAt", "native");
                var id = Text(e, "id"); Identifier(id);
                var canonical = Canonical(e);
                if (identities.TryGetValue(id, out var prior))
                {
                    if (canonical != prior) throw new TraceInputException("DUPLICATE_CONFLICT");
                    duplicates++; continue;
                }
                identities.Add(id, canonical);
                var timestamp = Text(e, "observedAt");
                if (!Regex.IsMatch(timestamp, "\\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?Z\\z", RegexOptions.CultureInvariant) || !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var observed) || observed.Offset != TimeSpan.Zero)
                    throw new TraceInputException("TIME_INVALID");
                if (last is not null && observed < last) throw new TraceInputException("TIME_ORDER");
                last = observed;
                var native = e.GetProperty("native");
                if (native.ValueKind != JsonValueKind.Object) throw new TraceInputException("NATIVE_INVALID");
                switch (Text(e, "kind"))
                {
                    case "provider": provider = native; providerId = id; break;
                    case "service": service = native; serviceId = id; break;
                    case "freshness": freshness = native; freshnessId = id; break;
                    case "timeout": diagnostics.Add("POLLING_TIMEOUT_NOT_FAILURE"); break;
                    default: throw new TraceInputException("KIND_UNSUPPORTED");
                }
            }
            var route = provider is { } p ? Route(p) : "unknown";
            var fresh = freshness is { } f ? Freshness(f) : "unassessed";
            var delivery = provider is { } leg && route is "completed" or "partial"
                ? Delivery(leg, intent, minimum) : "unknown";
            if (fresh is not ("current" or "unassessed")) { delivery = "unknown"; diagnostics.Add("FRESHNESS_" + fresh.ToUpperInvariant()); }
            var serviceState = service is { } receipt ? Service(receipt, intent) : "unknown";
            if (route == "unknown") diagnostics.Add("PROVIDER_OUTCOME_UNKNOWN");
            if (serviceState == "bindingMismatch") diagnostics.Add("SERVICE_BINDING_MISMATCH");
            if (serviceState == "unsupportedClaim") diagnostics.Add("SERVICE_UNSUPPORTED_CLAIM");
            if (delivery == "reportedAssetMismatch") diagnostics.Add("DELIVERY_IDENTITY_MISMATCH");
            if (duplicates > 0) diagnostics.Add("EXACT_DUPLICATE_IGNORED");
            var evidence = new Dictionary<string, string?>
            {
                ["route"] = providerId, ["delivery"] = providerId, ["service"] = serviceId, ["freshness"] = freshnessId
            };
            return new("1.0", Profile, Digest(intent), route, delivery, serviceState, fresh,
                "notIntegrated", "notVerified", "notAuthorized", duplicates, evidence, diagnostics, root.Clone());
        }
        catch (TraceInputException) { throw; }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { throw new TraceInputException("INPUT_INVALID"); }
    }

    private static string Route(JsonElement p)
    {
        var status = OptionalText(p, "status"); var sub = OptionalText(p, "substatus");
        return (status, sub) switch
        {
            ("DONE", "COMPLETED") => "completed", ("DONE", "PARTIAL") => "partial",
            ("DONE" or "FAILED", "REFUNDED") => "refunded",
            ("PENDING", "WAIT_SOURCE_CONFIRMATIONS" or "WAIT_DESTINATION_TRANSACTION" or "BRIDGE_NOT_AVAILABLE" or "CHAIN_NOT_AVAILABLE" or "REFUND_IN_PROGRESS") => "pending",
            ("FAILED", "NOT_PROCESSABLE_REFUND_NEEDED" or "OUT_OF_GAS" or "SLIPPAGE_EXCEEDED" or "INSUFFICIENT_ALLOWANCE" or "INSUFFICIENT_BALANCE" or "EXPIRED") => "failed",
            _ => "unknown"
        };
    }

    private static string Freshness(JsonElement n) => OptionalText(n, "state") switch
    {
        "current" => "current", "stale" => "stale", "expired" => "expired", "reorg" => "reorg", "unavailable" => "unavailable", _ => "unknown"
    };

    private static string Delivery(JsonElement n, JsonElement i, BigInteger minimum)
    {
        if (!n.TryGetProperty("receiving", out var r) || r.ValueKind != JsonValueKind.Object ||
            !r.TryGetProperty("amount", out var amount)) return "unknown";
        if (!r.TryGetProperty("chainId", out var chain) || !chain.TryGetInt32(out var chainId) || chainId != Int(i, "destinationChain") ||
            !r.TryGetProperty("token", out var token) || token.ValueKind != JsonValueKind.Object ||
            OptionalText(token, "address") != Text(i, "assetId") ||
            !token.TryGetProperty("decimals", out var decimals) || !decimals.TryGetInt32(out var scale) || scale != Int(i, "decimals"))
            return "reportedAssetMismatch";
        if (amount.ValueKind != JsonValueKind.String) throw new TraceInputException("AMOUNT_INVALID");
        return Atomic(amount.GetString()!) >= minimum ? "reportedAtLeastMinimum" : "reportedBelowMinimum";
    }

    private static string Service(JsonElement r, JsonElement i)
    {
        if (OptionalText(r, "verificationType") != "synthetic-declaration") return "unsupportedClaim";
        foreach (var key in new[] { "requestId", "task", "model", "inputDigest", "assetId", "chargeAtomic" })
            if (OptionalText(r, key) != Text(i, key)) return "bindingMismatch";
        if (OptionalText(r, "outputDigest") != Text(i, "expectedOutputDigest")) return "bindingMismatch";
        foreach (var key in new[] { "destinationChain", "decimals" })
            if (!r.TryGetProperty(key, out var value) || !value.TryGetInt32(out var number) || number != Int(i, key))
                return "bindingMismatch";
        return OptionalText(r, "state") switch { "succeeded" => "reportedSucceeded", "failed" => "reportedFailed", _ => "unknown" };
    }

    private static void Fields(JsonElement e, params string[] expected)
    {
        if (e.ValueKind != JsonValueKind.Object) throw new TraceInputException("SHAPE_INVALID");
        var actual = e.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected)) throw new TraceInputException("SHAPE_INVALID");
    }
    private static void UniqueProperties(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in e.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new TraceInputException("JSON_DUPLICATE_PROPERTY");
                UniqueProperties(p.Value);
            }
        }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var item in e.EnumerateArray()) UniqueProperties(item);
    }
    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString() ?? throw new TraceInputException("SHAPE_INVALID");
    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();
    private static string? OptionalText(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static void Identifier(string s)
    {
        if (s.Length is < 1 or > 128 || s.Any(c => c < 32 || c > 126)) throw new TraceInputException("IDENTIFIER_INVALID");
    }
    private static BigInteger Atomic(string s)
    {
        if (s.Length > 78 || !Regex.IsMatch(s, "\\A(0|[1-9][0-9]*)\\z", RegexOptions.CultureInvariant) ||
            !BigInteger.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n > Maximum)
            throw new TraceInputException("AMOUNT_INVALID");
        return n;
    }
    public static string Digest(JsonElement e) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(e))));
    private static string Canonical(JsonElement e)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) WriteCanonical(writer, e);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); WriteCanonical(writer, p.Value); }
            writer.WriteEndObject();
        }
        else if (e.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in e.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); }
        else e.WriteTo(writer);
    }

    public static string ToJson(TraceResult result) => JsonSerializer.Serialize(result, Json);
    public static string ToHtml(TraceResult result)
    {
        static string E(string value) => HtmlEncoder.Default.Encode(value);
        var rows = new (string, string, string?)[] { ("Route (provider)", result.Route, result.Evidence["route"]),
            ("Delivery (provider)", result.Delivery, result.Evidence["delivery"]), ("Service (declaration)", result.Service, result.Evidence["service"]),
            ("Freshness (declaration)", result.Freshness, result.Evidence["freshness"]), ("On-chain truth", result.OnChainTruth, null),
            ("Ledger", result.Ledger, null), ("Resend", result.Resend, null) };
        var body = string.Join("", rows.Select(r => $"<tr><th>{E(r.Item1)}</th><td>{E(r.Item2)}</td><td>{E(r.Item3 ?? "no evidence")}</td></tr>"));
        return "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">" +
            "<title>Settlement trace</title><style>body{font:16px system-ui;background:#101923;color:#e7eef5;margin:0;padding:clamp(20px,5vw,64px);max-width:1050px}h1{font-size:clamp(28px,5vw,48px)}p{color:#b9cad8;line-height:1.6}table{border-collapse:collapse;width:100%;margin:28px 0}th,td{text-align:left;padding:14px;border-bottom:1px solid #334452;overflow-wrap:anywhere}th{font-weight:500}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:13px ui-monospace;background:#172635;padding:20px;border-radius:8px}summary{cursor:pointer;padding:16px 0}code{overflow-wrap:anywhere}a{color:#8ad6bd}</style>" +
            "<h1>One trace. Separate evidence.</h1><p>Offline synthetic replay · profile " + E(result.Profile) +
            "</p><p>A completed transfer does not establish successful service delivery. Reported observations are not independently verified chain facts.</p>" +
            "<p>Intent digest <code>" + E(result.IntentDigest) + "</code></p><table><thead><tr><th>Claim</th><th>Result</th><th>Evidence ID</th></tr></thead><tbody>" + body +
            "</tbody></table><details open><summary>Runner result and original evidence</summary><pre id=\"runner-result\">" + E(ToJson(result)) +
            "</pre></details><p>No payment, retry, reservation or verification action is available in this viewer.</p></html>";
    }
}
