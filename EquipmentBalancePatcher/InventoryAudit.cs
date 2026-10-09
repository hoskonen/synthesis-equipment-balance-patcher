namespace EquipmentBalancePatcher;

public static class InventoryAudit
{
    public const string PatcherVersion = "0.1a";
    public const string SchemaVersion = "1";
    public const string ClassificationVersion = "1";

    public static AuditReport Build(IEnumerable<EquipmentSnapshot> snapshots, IEnumerable<InputPlugin> inputs,
        Settings settings, CancellationToken cancel = default)
    {
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
            new ClassificationIssue(e.Snapshot.FormKey, e.Snapshot.RecordType, d.Code, d.Message))).ToArray();
        return new(SchemaVersion, PatcherVersion, ClassificationVersion, "0.54.4", "0.36.6", "English",
            new Settings { OutputDirectory = settings.OutputDirectory }, inputs.ToArray(), summary, equipment, groups, issues);
    }

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
