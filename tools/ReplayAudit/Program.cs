using System.Text.Json;
using EquipmentBalancePatcher;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ReplayAudit <source audit.json> <separate output directory>");
    return 1;
}
var source = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
if (string.Equals(Path.GetDirectoryName(source), output, StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Replay output must differ from the reference report directory.");
var prior = JsonSerializer.Deserialize<AuditReport>(File.ReadAllText(source), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("Empty audit report.");
if (prior.SchemaVersion is not ("1" or "2")) throw new InvalidDataException("Unsupported reference schema.");
var report = InventoryAudit.Build(prior.Equipment.Select(e => e.Snapshot), prior.InputLoadOrder, prior.Settings);
var paths = ReportWriter.Write(report, output);
Console.WriteLine(ReportWriter.ConsoleSummary(report, paths));
Console.WriteLine($"Diagnostics before: {prior.Equipment.Sum(e => e.Diagnostics.Length)}; after: {report.Equipment.Sum(e => e.Diagnostics.Length)}");
Console.WriteLine("Replay reclassifies stored snapshots only; it does not validate the game or Synthesis GUI.");
return 0;
