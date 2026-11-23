using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SettlementTrace;

if (args.Length != 2) { Console.Error.WriteLine("Usage: TraceChecks fixtures/cases.json artifacts"); return 2; }
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAILED: " + name); checks++; }
void Reject(byte[] bytes, string code, string name)
{
    try { Replay.Run(bytes); throw new Exception("Unexpected acceptance: " + name); }
    catch (TraceInputException e) { Check(e.Code == code, name + ": " + e.Code); }
}
byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());
using var corpus = JsonDocument.Parse(File.ReadAllBytes(args[0]));
var cases = corpus.RootElement.GetProperty("cases");
JsonElement demo = default;
foreach (var c in cases.EnumerateArray())
{
    var name = c.GetProperty("name").GetString()!;
    var input = c.GetProperty("trace");
    var bytes = Encoding.UTF8.GetBytes(input.GetRawText());
    var expected = c.GetProperty("expected");
    if (expected.TryGetProperty("error", out var error))
    { Reject(bytes, error.GetString()!, name); continue; }
    var result = Replay.Run(bytes);
    using var actual = JsonDocument.Parse(Replay.ToJson(result));
    foreach (var field in expected.EnumerateObject())
        Check(actual.RootElement.GetProperty(field.Name).GetRawText() == field.Value.GetRawText(), name + "/" + field.Name);
    Check(JsonElement.DeepEquals(result.OriginalTrace, input), name + "/native-preservation");
    Check(result.IntentDigest.Length == 64, name + "/digest");
    if (name == "paid-service-failed") demo = input.Clone();
}
var original = JsonNode.Parse(demo.GetRawText())!;
var baseline = Replay.Run(Bytes(original));
var reordered = JsonNode.Parse(demo.GetRawText())!;
var intent = reordered["intent"]!.AsObject();
var properties = intent.Reverse().Select(kv => (kv.Key, kv.Value?.DeepClone())).ToArray();
intent.Clear(); foreach (var (key, value) in properties) intent[key] = value;
Check(baseline.IntentDigest == Replay.Run(Bytes(reordered)).IntentDigest, "canonical-property-order");
reordered["intent"]!["requestId"] = "demo-B";
Check(baseline.IntentDigest != Replay.Run(Bytes(reordered)).IntentDigest, "changed-intent-digest");
var duplicated = demo.GetRawText().Replace("\"status\": \"DONE\"", "\"status\": \"DONE\", \"status\": \"FAILED\"");
Check(duplicated != demo.GetRawText(), "duplicate-property-test-injection");
Reject(Encoding.UTF8.GetBytes(duplicated), "JSON_DUPLICATE_PROPERTY", "duplicate-json-native-key");
Reject(new byte[Replay.InputLimit+1], "INPUT_LIMIT", "input-byte-cap");
Reject(Encoding.UTF8.GetBytes("{}"), "SHAPE_INVALID", "missing-root-fields");
Reject(Encoding.UTF8.GetBytes("{"), "INPUT_INVALID", "malformed-json");
Reject(Encoding.UTF8.GetBytes(new string('[',20)+new string(']',20)), "INPUT_INVALID", "json-depth-cap");
var oversized = JsonNode.Parse(demo.GetRawText())!;
var es = oversized["events"]!.AsArray();
for (var i=es.Count;i<257;i++) es.Add(es[0]!.DeepClone());
Reject(Bytes(oversized), "EVENT_LIMIT", "event-cap-even-for-duplicates");
var missing = JsonNode.Parse(demo.GetRawText())!;
missing["events"]![1]!["native"]!.AsObject().Remove("requestId");
Check(Replay.Run(Bytes(missing)).Service == "bindingMismatch", "missing-receipt-request");
var unknown = JsonNode.Parse(demo.GetRawText())!;
unknown["events"]![0]!["native"]!["substatus"] = "FUTURE";
Check(Replay.Run(Bytes(unknown)).Route == "unknown", "future-enum-no-success");
foreach (var invalidTime in new[] { "10:00Z", "2026-11-23T10:00:00+00:00", "2026-02-30T10:00:00Z" })
{
    var badTime = JsonNode.Parse(demo.GetRawText())!;
    badTime["events"]![0]!["observedAt"] = invalidTime;
    Reject(Bytes(badTime), "TIME_INVALID", "strict-UTC-timestamp");
}
var xss = JsonNode.Parse(demo.GetRawText())!;
xss["events"]![0]!["native"]!["untrusted"] = "</pre><script>alert(1)</script><img src=x onerror=alert(2)>";
var xssResult = Replay.Run(Bytes(xss));
var html = Replay.ToHtml(xssResult);
Check(!html.Contains("<script>",StringComparison.OrdinalIgnoreCase) && !html.Contains("<img src=x",StringComparison.OrdinalIgnoreCase), "viewer-encodes-native-markup");
var match = Regex.Match(html, "<pre id=\"runner-result\">(.*?)</pre>", RegexOptions.Singleline);
Check(match.Success, "viewer-result-present");
Check(System.Net.WebUtility.HtmlDecode(match.Groups[1].Value) == Replay.ToJson(xssResult), "viewer-runner-exact-json");
Check(!html.Contains("src=\"http") && !html.Contains("fetch("), "viewer-offline");
var resultAgain = Replay.Run(Bytes(original));
Check(Replay.ToJson(baseline) == Replay.ToJson(resultAgain), "deterministic-replay");
Directory.CreateDirectory(args[1]);
File.WriteAllText(Path.Combine(args[1], "demo.json"), JsonSerializer.Serialize(demo, Replay.Json));
File.WriteAllText(Path.Combine(args[1], "demo-result.json"), Replay.ToJson(baseline));
File.WriteAllText(Path.Combine(args[1], "demo.html"), Replay.ToHtml(baseline));
var report = JsonSerializer.Serialize(new { schema="g2-verification-v1", cases=cases.GetArrayLength(), checks, failed=0,
    scope="Offline synthetic corpus + parser, identity, determinism, escaping, viewer/runner agreement. No RPC, financial effect or actual verifier." }, Replay.Json);
File.WriteAllText(Path.Combine(args[1], "verification.json"), report);
Console.WriteLine(report);
return 0;
