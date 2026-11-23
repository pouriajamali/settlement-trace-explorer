using System.Text.Json;
using SettlementTrace;

try
{
    if (args.Length is not (2 or 4) || args[0] != "replay" || (args.Length == 4 && args[2] != "--html"))
    { Console.Error.WriteLine("Usage: TraceCli replay trace.json [--html new-output.html]"); return 2; }
    var info = new FileInfo(args[1]);
    if (info.Length > Replay.InputLimit) throw new TraceInputException("INPUT_LIMIT");
    var result = Replay.Run(File.ReadAllBytes(args[1]));
    if (args.Length == 4)
    {
        using var stream = new FileStream(args[3], FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(Replay.ToHtml(result));
    }
    Console.WriteLine(Replay.ToJson(result));
    return 0;
}
catch (TraceInputException e) { Console.Error.WriteLine(JsonSerializer.Serialize(new { error = e.Code })); return 2; }
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{ Console.Error.WriteLine("{\"error\":\"FILE_IO\"}"); return 3; }
