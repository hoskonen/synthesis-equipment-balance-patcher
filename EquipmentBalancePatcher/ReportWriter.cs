using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EquipmentBalancePatcher;

public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Json(AuditReport report) => JsonSerializer.Serialize(report, JsonOptions) + "\n";

    public static string ResolveDirectory(string configured, string? settingsDirectory)
    {
        if (string.IsNullOrWhiteSpace(configured)) throw new ArgumentException("Report output directory cannot be empty.");
        if (Path.IsPathFullyQualified(configured)) return Path.GetFullPath(configured);
        var root = settingsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EquipmentBalancePatcher");
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("Settings-data directory must be absolute.");
        return Path.GetFullPath(Path.Combine(root, configured));
    }

    public static void ValidateDirectory(string directory)
    {
        var absolute = Path.GetFullPath(directory);
        if (File.Exists(absolute)) throw new IOException("Report directory points to an existing file: " + absolute);
        // Do not follow junctions or symlinks while writing reports.
        for (var current = new DirectoryInfo(absolute); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Report directory contains a junction or symbolic link: " + current.FullName);
        Directory.CreateDirectory(absolute);
        var probe = Path.Combine(absolute, ".ebp-write-probe-" + Guid.NewGuid().ToString("N"));
        try { using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write); }
        finally { if (File.Exists(probe)) File.Delete(probe); }
    }

    public static string[] Write(AuditReport report, string directory, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        ValidateDirectory(directory);
        var payloads = new[] {
            (Name: "equipment-inventory.csv", Text: InventoryCsv(report)),
            (Name: "equipment-groups.csv", Text: GroupsCsv(report)),
            (Name: "classification-issues.csv", Text: IssuesCsv(report)),
            (Name: "keyword-summary.csv", Text: KeywordsCsv(report)),
            (Name: "audit.json", Text: Json(report))
        };
        var staged = new List<(string Temporary, string Destination)>();
        try
        {
            foreach (var (name, content) in payloads)
            {
                cancel.ThrowIfCancellationRequested();
                var destination = Path.Combine(Path.GetFullPath(directory), name);
                if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Report destination is a symbolic link: " + destination);
                var temporary = Path.Combine(Path.GetFullPath(directory), ".ebp-report-" + Guid.NewGuid().ToString("N") + ".tmp");
                staged.Add((temporary, destination));
                using var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(content);
            }
            // Cancellation before commit preserves the preceding report set. Each replacement is atomic;
            // a filesystem failure during commit can leave a mixed set and is propagated as an error.
            cancel.ThrowIfCancellationRequested();
            foreach (var (temporary, destination) in staged) File.Move(temporary, destination, overwrite: true);
            return staged.Select(file => file.Destination).ToArray();
        }
        finally
        {
            foreach (var (temporary, _) in staged) if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string InventoryCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "recordType", "formKey", "editorId", "name", "definingPlugin", "winningPlugin", "deleted", "playable",
            "category", "material", "classificationStatus", "handedness", "armorType", "slotMask", "shieldFlag",
            "animationType", "skill", "damage", "speed", "reach", "criticalDamage", "stagger", "armorRating", "weight", "value",
            "enchantmentFormKey", "enchantmentEditorId", "enchantmentStatus", "enchantmentAmount", "hasScripts", "hasVirtualMachineAdapter",
            "templateFormKey", "templateEditorId", "templateStatus", "equipmentTypeFormKey", "equipmentTypeEditorId", "equipmentTypeStatus",
            "keywordsJson", "flagsJson", "classificationEvidenceJson", "classificationReasonsJson",
            "statisticsEligible", "baselineEligible", "analysisEligible", "statisticsReasonsJson", "baselineReasonsJson", "analysisReasonsJson", "diagnosticsJson",
            "materialFamily", "materialVariant", "scope", "scopeReasonsJson", "statSemantics");
        foreach (var e in report.Equipment)
        {
            var s = e.Snapshot; var c = e.Classification; var eligibility = e.Eligibility;
            Row(text, s.RecordType, s.FormKey, s.EditorId, s.Name, s.DefiningPlugin, s.WinningPlugin, s.Deleted, s.Playable,
                c.Category, c.Material, c.Status, c.Handedness, s.ArmorType, s.SlotMask, s.ShieldFlag,
                s.AnimationType, s.Skill, s.Damage, s.Speed, s.Reach, s.CriticalDamage, s.Stagger, s.ArmorRating, s.Weight, s.Value,
                s.Enchantment?.FormKey, s.Enchantment?.EditorId, s.Enchantment?.Status, s.EnchantmentAmount, s.HasScripts, s.HasVirtualMachineAdapter,
                s.Template?.FormKey, s.Template?.EditorId, s.Template?.Status, s.EquipmentType?.FormKey, s.EquipmentType?.EditorId, s.EquipmentType?.Status,
                Compact(s.Keywords), Compact(s.Flags), Compact(c.Evidence), Compact(c.ReasonCodes),
                eligibility.InventoryStatisticsEligible, eligibility.BaselineEligible, eligibility.AnalysisEligible,
                Compact(eligibility.InventoryStatisticsReasons), Compact(eligibility.BaselineReasons), Compact(eligibility.AnalysisReasons), Compact(e.Diagnostics),
                c.MaterialFamily, c.MaterialVariant, c.Scope, Compact(c.ScopeReasons), s.StatSemantics);
        }
        return text.ToString();
    }

    public static string GroupsCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "recordType", "category", "armorType", "material", "enchanted", "count", "baselineEligibleCount", "baselineExcludedCount",
            "metric", "availableCount", "missingCount", "minimum", "maximum", "mean", "median");
        foreach (var group in report.Groups)
            foreach (var (name, metric) in group.Metrics)
                Row(text, group.RecordType, group.Category, group.ArmorType, group.Material, group.Enchanted, group.Count,
                    group.BaselineEligibleCount, group.BaselineExcludedCount, name, metric.AvailableCount, metric.MissingCount,
                    metric.Minimum, metric.Maximum, metric.Mean, metric.Median);
        return text.ToString();
    }

    public static string IssuesCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "recordType", "formKey", "editorId", "winningPlugin", "code", "message", "severity", "relevance");
        foreach (var e in report.Equipment)
            foreach (var d in e.Diagnostics) Row(text, e.Snapshot.RecordType, e.Snapshot.FormKey, e.Snapshot.EditorId,
                e.Snapshot.WinningPlugin, d.Code, d.Message, d.Severity, d.Relevance);
        return text.ToString();
    }

    public static string KeywordsCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "formKey", "editorIdsJson", "resolutionStatusesJson", "diagnosticCategory", "potentiallyRelevant",
            "affectedRecords", "recordTypesJson", "originPluginsJson", "winningPluginsJson", "examplesJson");
        foreach (var k in report.KeywordSummary)
            Row(text, k.FormKey, Compact(k.EditorIds), Compact(k.ResolutionStatuses), k.DiagnosticCategory,
                k.PotentiallyRelevant, k.AffectedRecords, Compact(k.RecordTypes), Compact(k.OriginPlugins),
                Compact(k.WinningPlugins), Compact(k.Examples));
        return text.ToString();
    }

    private static string Compact<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public static string CsvCell(object? value)
    {
        var raw = value switch { null => "", bool boolean => boolean ? "true" : "false",
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString() ?? "" };
        return raw.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? '"' + raw.Replace("\"", "\"\"") + '"' : raw;
    }

    private static void Row(StringBuilder text, params object?[] cells) => text.Append(string.Join(',', cells.Select(CsvCell))).Append('\n');

    public static string ConsoleSummary(AuditReport report, IEnumerable<string> paths)
    {
        var s = report.Summary;
        var text = new StringBuilder();
        text.AppendLine("Equipment Balance Patcher v0.1b — read-only equipment inventory");
        text.AppendLine($"Inspected WEAP: {s.Weapons}; ARMO: {s.Armor}; deleted: {s.Deleted}");
        text.AppendLine($"Classification — confident: {s.Confident}; partial: {s.Partial}; unclassified: {s.Unclassified}");
        text.AppendLine($"Templated: {s.Templated}; enchanted: {s.Enchanted}; scripted: {s.Scripted}; unresolved reference occurrences: {s.UnresolvedReferences}");
        text.AppendLine($"Statistics eligible: {s.StatisticsEligible}; baseline eligible: {s.BaselineEligible}; baseline excluded: {s.BaselineExcluded}");
        text.AppendLine("Category breakdown (all inventoried records):");
        foreach (var (category, count) in s.Categories) text.AppendLine($"  {category}: {count}");
        text.AppendLine("Material breakdown (all inventoried records):");
        foreach (var (material, count) in s.Materials) text.AppendLine($"  {material}: {count}");
        text.AppendLine("Eligibility exclusions by stage/type/reason (overlapping; do not sum):");
        foreach (var reason in report.EligibilitySummary)
            text.AppendLine($"  {reason.Stage}/{reason.RecordType}/{reason.ReasonCategory}/{reason.Reason}: {reason.AffectedRecords}");
        text.AppendLine("Reports:");
        foreach (var path in paths) text.AppendLine("  " + path);
        text.AppendLine("No game records modified. No balance evaluation performed.");
        return text.ToString();
    }
}
