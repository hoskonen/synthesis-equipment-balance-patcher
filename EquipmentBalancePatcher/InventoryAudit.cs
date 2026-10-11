namespace EquipmentBalancePatcher;

public static class InventoryAudit
{
    public const string PatcherVersion = "0.1h";
    public const string SchemaVersion = "6";
    public const string ClassificationVersion = "2";

    public static AuditReport Build(IEnumerable<EquipmentSnapshot> snapshots, IEnumerable<InputPlugin> inputs,
        Settings settings, CancellationToken cancel = default, Func<string, string?>? missingTemplateTargetStatus = null)
    {
        BalanceAudit.Validate(settings);
        var equipment = snapshots.OrderBy(s => s.RecordType, StringComparer.Ordinal)
            .ThenBy(s => s.FormKey, StringComparer.Ordinal).Select(snapshot =>
            {
                cancel.ThrowIfCancellationRequested();
                return EquipmentClassifier.Classify(snapshot);
            }).ToArray();
        var groups = equipment.Where(e => e.Eligibility.InventoryStatisticsEligible)
            .GroupBy(e => (e.Snapshot.RecordType, Category: e.Classification.Category!, e.Snapshot.ArmorType,
                Material: e.Classification.Material!, Enchanted: e.Snapshot.Enchantment is not null))
            .OrderBy(group => group.Key.RecordType, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ArmorType, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Category, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Material, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Enchanted).Select(group =>
            {
                cancel.ThrowIfCancellationRequested();
                var records = group.ToArray();
                var metrics = new SortedDictionary<string, MetricStatistics>(StringComparer.Ordinal);
                foreach (var metric in MetricNames(group.Key.RecordType))
                    metrics.Add(metric, Describe(records.Select(e => Metric(e.Snapshot, metric))));
                var baselineCount = records.Count(e => e.Eligibility.BaselineEligible);
                return new GroupStatistics(group.Key.RecordType, group.Key.Category, group.Key.ArmorType,
                    group.Key.Material, group.Key.Enchanted, records.Length, baselineCount,
                    records.Length - baselineCount, metrics);
            }).ToArray();
        var categories = Counts(equipment.Select(e => $"{e.Snapshot.RecordType}:{e.Classification.Category ?? "Unknown"}"));
        var materials = Counts(equipment.Select(e => e.Classification.Material ?? "Unknown"));
        var summary = new InventorySummary(
            equipment.Count(e => e.Snapshot.RecordType == "WEAP"), equipment.Count(e => e.Snapshot.RecordType == "ARMO"),
            equipment.Count(e => e.Snapshot.Deleted), equipment.Count(e => e.Classification.Status == "Confident"),
            equipment.Count(e => e.Classification.Status == "Partial"), equipment.Count(e => e.Classification.Status == "Unclassified"),
            equipment.Count(e => e.Snapshot.Template is not null), equipment.Count(e => e.Snapshot.Enchantment is not null),
            equipment.Count(e => e.Snapshot.HasScripts), equipment.Count(e => e.Eligibility.BaselineEligible),
            equipment.Count(e => !e.Eligibility.BaselineEligible), equipment.Count(e => e.Eligibility.InventoryStatisticsEligible),
            equipment.Sum(e => e.Snapshot.Keywords.Count(k => k.Status != "Resolved")
                + EquipmentClassifier.References(e.Snapshot).Count(r => r.Link.Status != "Resolved")), categories, materials);
        var issues = equipment.SelectMany(e => e.Diagnostics.Select(d =>
            new ClassificationIssue(e.Snapshot.FormKey, e.Snapshot.RecordType, d.Code, d.Message)
            { Severity = d.Severity, Relevance = d.Relevance })).ToArray();
        var resolver = new TemplateResolver(equipment.Select(e => e.Snapshot), settings.MaximumTemplateDepth, missingTemplateTargetStatus);
        var templates = equipment.Where(e => e.Snapshot.Template is not null)
            .Select(e => EffectiveEquipmentInvestigator.Investigate(e, resolver, cancel)).ToArray();
        var roots = templates.Where(t => t.Chain.Status == "Resolved")
            .GroupBy(t => (t.Chain.RecordType, Root: t.Chain.RootParent!))
            .OrderBy(g => g.Key.RecordType, StringComparer.Ordinal).ThenBy(g => g.Key.Root, StringComparer.Ordinal)
            .Select(g =>
            {
                cancel.ThrowIfCancellationRequested();
                var children = g.Select(t => resolver.GetRecord(t.ChildFormKey)).ToArray();
                return new TemplateRootSummary(g.Key.RecordType, g.Key.Root, resolver.GetRecord(g.Key.Root).WinningPlugin,
                    children.Length, children.Count(c => c.Enchantment is not null),
                    children.Select(c => c.DefiningPlugin).Distinct().Order(StringComparer.Ordinal).ToArray(),
                    children.Select(c => c.WinningPlugin).Distinct().Order(StringComparer.Ordinal).ToArray(),
                    children.Select(c => c.FormKey).Order(StringComparer.Ordinal).ToArray(), "UnvalidatedDesignFamily_NoBaselineCreated");
            }).ToArray();
        var balance = BalanceAudit.Build(equipment, settings, cancel);
        var references = ReferenceComparison.Build(equipment, settings, balance.Summary.AnalyzedRecords, cancel);
        return new(SchemaVersion, PatcherVersion, ClassificationVersion, "0.54.4", "0.36.6", "English",
            System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(settings))!, inputs.ToArray(), summary, equipment, groups, issues)
        {
            KeywordSummary = AggregateKeywords(equipment),
            EligibilitySummary = AggregateEligibility(equipment),
            TemplateAnalysis = templates,
            TemplateRoots = roots,
            BalanceAnalysis = balance,
            ReferenceAnalysis = references,
            WeaponEvidenceAnalysis = WeaponEvidenceAudit.Build(equipment, references, cancel)
        };
    }

    private static KeywordSummary[] AggregateKeywords(AuditedEquipment[] equipment) => equipment
        .SelectMany(e => e.Snapshot.Keywords.DistinctBy(k => k.FormKey, StringComparer.OrdinalIgnoreCase)
            .Select(k => (Equipment: e.Snapshot, Keyword: k)))
        .GroupBy(x => x.Keyword.FormKey, StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g =>
        {
            var rows = g.ToArray();
            string[] Sorted(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var categories = Sorted(rows.Select(r => DiagnosticPolicy.KeywordCategory(r.Keyword)));
            var category = categories.Contains("Unresolved") ? "Unresolved"
                : categories.Contains("PotentialClassification") ? "PotentialClassification" : categories[0];
            return new KeywordSummary(Sorted(rows.Select(r => r.Keyword.FormKey))[0],
                Sorted(rows.Select(r => r.Keyword.EditorId).OfType<string>()), Sorted(rows.Select(r => r.Keyword.Status)),
                category, category is "PotentialClassification" or "RecognizedClassification" or "Unresolved",
                rows.Length, Sorted(rows.Select(r => r.Equipment.RecordType)), Sorted(rows.Select(r => r.Equipment.DefiningPlugin)),
                Sorted(rows.Select(r => r.Equipment.WinningPlugin)), rows.Select(r => r.Equipment)
                    .OrderBy(s => s.RecordType, StringComparer.Ordinal).ThenBy(s => s.FormKey, StringComparer.Ordinal)
                    .Take(5).Select(s => new KeywordExample(s.FormKey, s.EditorId, s.RecordType)).ToArray());
        }).ToArray();

    private static EligibilityReasonSummary[] AggregateEligibility(AuditedEquipment[] equipment) => equipment
        .SelectMany(e => new[] {
            (Stage: "Statistics", Reasons: e.Eligibility.InventoryStatisticsReasons),
            (Stage: "Baseline", Reasons: e.Eligibility.BaselineReasons),
            (Stage: "Analysis", Reasons: e.Eligibility.AnalysisReasons)
        }.SelectMany(stage => stage.Reasons.Distinct().Select(reason => (stage.Stage, e.Snapshot.RecordType, Reason: reason))))
        .GroupBy(x => (x.Stage, x.RecordType, x.Reason))
        .OrderBy(g => g.Key.Stage, StringComparer.Ordinal).ThenBy(g => g.Key.RecordType, StringComparer.Ordinal)
        .ThenBy(g => g.Key.Reason, StringComparer.Ordinal)
        .Select(g => new EligibilityReasonSummary(g.Key.Stage, g.Key.RecordType, g.Key.Reason,
            DiagnosticPolicy.ReasonCategory(g.Key.Reason), g.Count())).ToArray();

    public static MetricStatistics Describe(IEnumerable<double?> source)
    {
        var all = source.ToArray();
        var available = all.Where(value => value.HasValue && double.IsFinite(value.Value))
            .Select(value => value!.Value).Order().ToArray();
        if (available.Length == 0) return new(0, all.Length, null, null, null, null);
        var middle = available.Length / 2;
        var median = available.Length % 2 == 1 ? available[middle] : available[middle - 1] / 2 + available[middle] / 2;
        return new(available.Length, all.Length - available.Length, available[0], available[^1],
            available.Sum(value => value / available.Length), median);
    }

    public static string[] MetricNames(string recordType) => recordType == "WEAP"
        ? ["Damage", "Speed", "Reach", "Weight", "Value", "CriticalDamage", "Stagger"]
        : ["ArmorRating", "Weight", "Value"];

    public static double? Metric(EquipmentSnapshot snapshot, string name) => name switch
    {
        "Damage" => snapshot.Damage, "Speed" => snapshot.Speed, "Reach" => snapshot.Reach,
        "Weight" => snapshot.Weight, "Value" => snapshot.Value, "CriticalDamage" => snapshot.CriticalDamage,
        "Stagger" => snapshot.Stagger, "ArmorRating" => snapshot.ArmorRating,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown inventory metric.")
    };

    private static SortedDictionary<string, int> Counts(IEnumerable<string> source)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var group in source.GroupBy(value => value, StringComparer.Ordinal)) result.Add(group.Key, group.Count());
        return result;
    }
}
