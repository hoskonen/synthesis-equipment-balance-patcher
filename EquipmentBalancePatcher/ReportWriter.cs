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
            (Name: "template-analysis.csv", Text: TemplatesCsv(report)),
            (Name: "balance-findings.csv", Text: BalanceFindingsCsv(report)),
            (Name: "balance-groups.csv", Text: BalanceGroupsCsv(report)),
            (Name: "reference-comparisons.csv", Text: ReferenceComparisonsCsv(report)),
            (Name: "weapon-evidence.csv", Text: WeaponEvidenceCsv(report)),
            (Name: "WeaponDryRun.txt", Text: WeaponDryRunReport.Render(report)),
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

    public static string TemplatesCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "recordType", "childFormKey", "childEditorId", "childName", "childWinningPlugin", "directParent", "rootParent",
            "templateDepth", "chainStatus", "fieldName", "rawChildValueJson", "proposedEffectiveValueJson", "sourceFormKey",
            "sourceWinningPlugin", "ruleId", "resolutionStatus", "fieldTemplateDepth", "valuesDiffer", "validationStatus", "resolutionDiagnosticsJson");
        foreach (var t in report.TemplateAnalysis)
            foreach (var f in t.Fields)
                Row(text, t.Chain.RecordType, t.ChildFormKey, t.ChildEditorId, t.ChildName, t.ChildWinningPlugin,
                    t.Chain.DirectParent, t.Chain.RootParent, t.Chain.Depth, t.Chain.Status, f.Name,
                    f.RawChildValue.GetRawText(), f.ProposedEffectiveValue.GetRawText(), f.SourceFormKey,
                    f.SourceWinningPlugin, f.RuleId, f.ResolutionStatus, f.TemplateDepth, f.ValuesDiffer,
                    f.ValidationStatus, Compact(t.Diagnostics));
        return text.ToString();
    }

    public static string WeaponEvidenceCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "formKey", "editorId", "name", "definingPlugin", "winningPlugin", "subtype", "animationType", "flagsJson",
            "playable", "hasScripts", "templateFormKey", "classificationStatus", "classifiedMaterial", "baselineEligible", "analysisEligible",
            "ordinaryInspected", "evidenceStatus", "evidenceConfidence", "materialKeywordsJson", "keywordMaterialsJson", "keywordStatus",
            "nameMaterialsJson", "nameAgreement", "craftingEvidenceStatus", "referenceFormKey", "referenceName", "referenceWinningPlugin",
            "referenceStatus", "referenceMetricsJson", "reasonsJson");
        var equipment = report.Equipment.ToDictionary(e => e.Snapshot.FormKey, StringComparer.OrdinalIgnoreCase);
        var references = (report.ReferenceAnalysis?.Catalog ?? []).ToDictionary(r => r.Identity.FormKey, StringComparer.OrdinalIgnoreCase);
        foreach (var r in report.WeaponEvidenceAnalysis?.Records ?? [])
        {
            var e = equipment[r.FormKey]; var s = e.Snapshot;
            var reference = r.ReferenceFormKey is null ? null : references[r.ReferenceFormKey];
            Row(text, s.FormKey, s.EditorId, s.Name, s.DefiningPlugin, s.WinningPlugin, e.Classification.Category, s.AnimationType,
                Compact(s.Flags), s.Playable, s.HasScripts, s.Template?.FormKey, e.Classification.Status, e.Classification.Material,
                e.Eligibility.BaselineEligible, e.Eligibility.AnalysisEligible, r.OrdinaryInspected, r.Status, r.Confidence,
                Compact(r.MaterialKeywords), Compact(r.KeywordMaterials), r.KeywordStatus, Compact(r.NameMaterials), r.NameAgreement,
                r.CraftingEvidenceStatus, r.ReferenceFormKey, reference?.Identity.Name, reference?.WinningPlugin, r.ReferenceStatus,
                Compact(r.ReferenceMetrics), Compact(r.Reasons));
        }
        return text.ToString();
    }

    public static string ReferenceComparisonsCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "formKey", "editorId", "name", "definingPlugin", "winningPlugin", "recordType", "category", "material", "armorType",
            "referenceFormKey", "referenceName", "referenceWinningName", "referenceWinningPlugin", "metric", "candidateValue", "referenceValue",
            "signedDifference", "absoluteDifference", "percentageDifference", "comparisonStatus", "recordStatus", "reasonsJson");
        if (report.ReferenceAnalysis is not { } analysis) return text.ToString();
        var equipment = report.Equipment.ToDictionary(e => e.Snapshot.FormKey, StringComparer.OrdinalIgnoreCase);
        var references = analysis.Catalog.ToDictionary(r => r.Identity.FormKey, StringComparer.OrdinalIgnoreCase);
        foreach (var result in analysis.Records.Where(r => r.Status != "Excluded"))
        {
            var e = equipment[result.FormKey]; var s = e.Snapshot;
            var reference = result.ReferenceFormKey is null ? null : references[result.ReferenceFormKey];
            void Write(ReferenceMetric? metric) => Row(text, s.FormKey, s.EditorId, s.Name, s.DefiningPlugin, s.WinningPlugin,
                s.RecordType, e.Classification.Category, e.Classification.Material, s.ArmorType, result.ReferenceFormKey,
                reference?.Identity.Name, reference?.WinningName, reference?.WinningPlugin, metric?.Metric, metric?.CandidateValue,
                metric?.ReferenceValue, metric?.SignedDifference, metric?.AbsoluteDifference, metric?.PercentageDifference,
                metric?.Status ?? result.Status, result.Status, Compact(result.Reasons.Concat(metric?.Reasons ?? []).Distinct().Order(StringComparer.Ordinal).ToArray()));
            if (result.Metrics.Length == 0) Write(null);
            else foreach (var metric in result.Metrics) Write(metric);
        }
        return text.ToString();
    }

    public static string BalanceFindingsCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "recordType", "formKey", "editorId", "name", "definingPlugin", "winningPlugin", "category", "material",
            "cohortId", "metric", "value", "referenceMedian", "referenceMinimum", "referenceMaximum", "independentPeerCount",
            "signedDifference", "absoluteDifference", "percentageDifference", "robustDeviationScore", "findingType", "severity",
            "confidence", "explanation", "falsePositiveReasonsJson", "peerFormKeysJson", "referenceMad");
        foreach (var f in report.BalanceAnalysis?.Findings ?? [])
            Row(text, f.RecordType, f.FormKey, f.EditorId, f.Name, f.DefiningPlugin, f.WinningPlugin, f.Category, f.Material,
                f.CohortId, f.Metric, f.Value, f.Reference.Median, f.Reference.Minimum, f.Reference.Maximum, f.Reference.Count,
                f.SignedDifference, f.AbsoluteDifference, f.PercentageDifference, f.RobustDeviationScore, f.FindingType,
                f.Severity, f.Confidence, f.Explanation, Compact(f.FalsePositiveReasons), Compact(f.PeerFormKeys), f.Reference.Mad);
        return text.ToString();
    }

    public static string BalanceGroupsCsv(AuditReport report)
    {
        var text = new StringBuilder();
        Row(text, "cohortId", "recordType", "category", "armorType", "material", "animationType", "skill", "equipmentType",
            "slotMask", "shieldFlag", "flags", "metric", "candidateCount", "independentReferenceCount", "invalidReferenceMetrics",
            "minimum", "maximum", "mean", "median", "mad", "q1", "q3", "status", "maximumDefiningShare", "maximumWinningShare",
            "definingPluginsJson", "winningPluginsJson", "possibleDuplicateRecords", "warningsJson", "memberFormKeysJson",
            "referenceFormKeysJson", "referenceDesignIdsJson");
        foreach (var g in report.BalanceAnalysis?.Groups ?? [])
        {
            var c = g.Cohort; var d = g.Distribution; var p = g.Concentration;
            Row(text, g.CohortId, c.RecordType, c.Category, c.ArmorType, c.Material, c.AnimationType, c.Skill, c.EquipmentType,
                c.SlotMask, c.ShieldFlag, c.Flags, g.Metric, g.CandidateCount, d.Count, g.InvalidReferenceMetrics,
                d.Minimum, d.Maximum, d.Mean, d.Median, d.Mad, d.Q1, d.Q3, g.Status, p.MaximumDefiningShare, p.MaximumWinningShare,
                Compact(p.DefiningPlugins), Compact(p.WinningPlugins), g.PossibleDuplicateRecords, Compact(g.Warnings),
                Compact(g.MemberFormKeys), Compact(g.ReferenceFormKeys), Compact(g.ReferenceDesignIds));
        }
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
        var text = new StringBuilder();
        text.Append(FormattableString.Invariant($"Inventory: {report.Summary.Weapons} WEAP; {report.Summary.Armor} ARMO; {report.Summary.Templated} templated records.\n"));
        if (report.BalanceAnalysis is { } balance)
            text.Append(FormattableString.Invariant($"Statistical audit: {balance.Summary.AnalyzedRecords} supported records; {balance.Summary.StatisticalFindings} provisional metric findings.\n"));
        text.Append("Reports:\n");
        foreach (var path in paths) text.Append("  ").Append(path).Append('\n');
        text.Append('\n').Append(WeaponDryRunReport.Render(report));
        return text.ToString();
    }
}