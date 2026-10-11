namespace EquipmentBalancePatcher;

public sealed record CanonicalReference(string FormKey, string RecordType, string Category, string Material, string? ArmorType, string Name);
public sealed record ResolvedReference(CanonicalReference Identity, string Status, string? WinningPlugin, string? WinningName, string[] Reasons);
public sealed record ReferenceMetric(string Metric, double? CandidateValue, double? ReferenceValue, double? SignedDifference,
    double? AbsoluteDifference, double? PercentageDifference, string Status, string[] Reasons);
public sealed record ReferenceResult(string FormKey, string Status, string? ReferenceFormKey, string[] Reasons, ReferenceMetric[] Metrics);
public sealed record ReferenceCoverage(string RecordType, string? Category, string? Material, string? ArmorType,
    int Eligible, int Matched, int NoReference, int ReferenceUnavailable, int Deviations);
public sealed record ReferenceSummary(int CatalogEntries, int ResolvedReferences, int MissingReferences, int UnusableReferences,
    int EligibleRecords, int MatchedRecords, int NoReferenceRecords, int ReferenceUnavailableRecords, int ReferenceRecords,
    int DeviationRecords, int ComparableRecords, int StatisticalAnalyzedRecords);
public sealed record ReferenceAnalysis(string PolicyVersion, bool Enabled, ReferenceSummary Summary,
    ResolvedReference[] Catalog, ReferenceResult[] Records, ReferenceCoverage[] Coverage);

public static class ReferenceComparison
{
    // Explicit identities verified against FormKeys registry 3.4.0, commit 650e147f854086b47b91ea80a881751466135256.
    // No hardcoded stats; unreviewed category/tier combinations are omitted.
    public static IReadOnlyList<CanonicalReference> Catalog { get; } = Array.AsReadOnly(new CanonicalReference[]
    {
        new("012EB7:Skyrim.esm", "WEAP", "Sword", "Iron", null, "Iron Sword"),
        new("013989:Skyrim.esm", "WEAP", "Sword", "Steel", null, "Steel Sword"),
        new("0139A1:Skyrim.esm", "WEAP", "Sword", "Elven", null, "Elven Sword"),
        new("013999:Skyrim.esm", "WEAP", "Sword", "Dwarven", null, "Dwarven Sword"),
        new("013991:Skyrim.esm", "WEAP", "Sword", "Orcish", null, "Orcish Sword"),
        new("0139B1:Skyrim.esm", "WEAP", "Sword", "Ebony", null, "Ebony Sword"),
        new("01397E:Skyrim.esm", "WEAP", "Dagger", "Iron", null, "Iron Dagger"),
        new("013986:Skyrim.esm", "WEAP", "Dagger", "Steel", null, "Steel Dagger"),
        new("01399E:Skyrim.esm", "WEAP", "Dagger", "Elven", null, "Elven Dagger"),
        new("013996:Skyrim.esm", "WEAP", "Dagger", "Dwarven", null, "Dwarven Dagger"),
        new("01398E:Skyrim.esm", "WEAP", "Dagger", "Orcish", null, "Orcish Dagger"),
        new("0139AE:Skyrim.esm", "WEAP", "Dagger", "Ebony", null, "Ebony Dagger"),
        new("01399D:Skyrim.esm", "WEAP", "Bow", "Elven", null, "Elven Bow"),
        new("013995:Skyrim.esm", "WEAP", "Bow", "Dwarven", null, "Dwarven Bow"),
        new("01398D:Skyrim.esm", "WEAP", "Bow", "Orcish", null, "Orcish Bow"),
        new("0139AD:Skyrim.esm", "WEAP", "Bow", "Ebony", null, "Ebony Bow"),
        new("012E49:Skyrim.esm", "ARMO", "Cuirass", "Iron", "HeavyArmor", "Iron Cuirass"),
        new("013952:Skyrim.esm", "ARMO", "Cuirass", "Steel", "HeavyArmor", "Steel Cuirass (A)"),
        new("01394D:Skyrim.esm", "ARMO", "Cuirass", "Dwarven", "HeavyArmor", "Dwarven Cuirass"),
        new("013957:Skyrim.esm", "ARMO", "Cuirass", "Orcish", "HeavyArmor", "Orcish Cuirass"),
        new("013961:Skyrim.esm", "ARMO", "Cuirass", "Ebony", "HeavyArmor", "Ebony Cuirass"),
        new("012E4D:Skyrim.esm", "ARMO", "Helmet", "Iron", "HeavyArmor", "Iron Helmet"),
        new("013954:Skyrim.esm", "ARMO", "Helmet", "Steel", "HeavyArmor", "Steel Helmet (A)"),
        new("01394F:Skyrim.esm", "ARMO", "Helmet", "Dwarven", "HeavyArmor", "Dwarven Helmet"),
        new("013959:Skyrim.esm", "ARMO", "Helmet", "Orcish", "HeavyArmor", "Orcish Helmet"),
        new("013963:Skyrim.esm", "ARMO", "Helmet", "Ebony", "HeavyArmor", "Ebony Helmet"),
        new("0896A3:Skyrim.esm", "ARMO", "Cuirass", "Elven", "LightArmor", "Elven Cuirass"),
        new("01391D:Skyrim.esm", "ARMO", "Helmet", "Elven", "LightArmor", "Elven Helmet"),
    });
    private static (string Type, string? Category, string? Material, string? Armor) Key(AuditedEquipment e) =>
        (e.Snapshot.RecordType, e.Classification.Category, e.Classification.Material, e.Snapshot.ArmorType);
    private static (string Type, string? Category, string? Material, string? Armor) Key(CanonicalReference e) =>
        (e.RecordType, e.Category, e.Material, e.ArmorType);

    public static ReferenceAnalysis Build(AuditedEquipment[] equipment, Settings settings, int statisticalAnalyzedRecords,
        CancellationToken cancel = default, IReadOnlyList<CanonicalReference>? catalog = null)
    {
        catalog ??= Catalog;
        var winners = equipment.ToDictionary(e => e.Snapshot.FormKey, StringComparer.OrdinalIgnoreCase);
        var identities = catalog.ToDictionary(Key);
        var resolved = new List<ResolvedReference>();
        foreach (var identity in catalog.OrderBy(e => e.FormKey, StringComparer.Ordinal))
        {
            cancel.ThrowIfCancellationRequested();
            winners.TryGetValue(identity.FormKey, out var winner);
            var reasons = new List<string>();
            if (winner is null) reasons.Add("MissingReference");
            else
            {
                if (Key(winner) != Key(identity) || winner.Classification.Status != "Confident") reasons.Add("ReferenceIdentityMismatch");
                if (!winner.Eligibility.BaselineEligible) reasons.AddRange(winner.Eligibility.BaselineReasons);
                if (!BalanceAudit.Metrics(identity.RecordType).Any(m => BalanceAudit.Valid(InventoryAudit.Metric(winner.Snapshot, m))))
                    reasons.Add("NoValidReferenceMetrics");
            }
            resolved.Add(new(identity, winner is null ? "Missing" : reasons.Count > 0 ? "Unusable" : "Resolved",
                winner?.Snapshot.WinningPlugin, winner?.Snapshot.Name, reasons.Distinct().Order(StringComparer.Ordinal).ToArray()));
        }
        var byIdentity = resolved.ToDictionary(r => r.Identity.FormKey, StringComparer.OrdinalIgnoreCase);
        var records = new List<ReferenceResult>();
        foreach (var e in equipment.OrderBy(e => e.Snapshot.FormKey, StringComparer.Ordinal))
        {
            cancel.ThrowIfCancellationRequested();
            var s = e.Snapshot;
            var enchantmentExcluded = s.Enchantment is not null && !settings.IncludeEnchantedAnalysisCandidates;
            if (!settings.EnableReferenceComparison || !e.Eligibility.AnalysisEligible || enchantmentExcluded)
            {
                var reasons = e.Eligibility.AnalysisReasons.AsEnumerable();
                if (!settings.EnableReferenceComparison) reasons = reasons.Append("ReferenceComparisonDisabled");
                if (enchantmentExcluded) reasons = reasons.Append("EnchantedAnalysisDisabled");
                records.Add(new(s.FormKey, "Excluded", null, reasons.Distinct().Order(StringComparer.Ordinal).ToArray(), []));
                continue;
            }
            if (!identities.TryGetValue(Key(e), out var identity)) { records.Add(new(s.FormKey, "NoReference", null, ["NoCatalogEntryForExactTierCategoryType"], [])); continue; }
            var reference = byIdentity[identity.FormKey];
            if (reference.Status != "Resolved") { records.Add(new(s.FormKey, "ReferenceUnavailable", identity.FormKey, reference.Reasons, [])); continue; }
            var root = winners[identity.FormKey].Snapshot;
            var self = string.Equals(s.FormKey, identity.FormKey, StringComparison.OrdinalIgnoreCase);
            var metrics = BalanceAudit.Metrics(s.RecordType).Select(metric =>
            {
                var value = InventoryAudit.Metric(s, metric); var referenceValue = InventoryAudit.Metric(root, metric);
                if (!BalanceAudit.Valid(value) || !BalanceAudit.Valid(referenceValue))
                    return new ReferenceMetric(metric, BalanceAudit.Valid(value) ? value : null, BalanceAudit.Valid(referenceValue) ? referenceValue : null,
                        null, null, null, "Unavailable", [!BalanceAudit.Valid(value) ? "InvalidCandidateMetric" : "InvalidReferenceMetric"]);
                var diff = value!.Value - referenceValue!.Value;
                double? percent = referenceValue == 0 ? null : diff / referenceValue * 100;
                if (percent.HasValue && !double.IsFinite(percent.Value)) percent = null;
                var threshold = settings.BalanceThresholds.For(metric);
                var deviates = diff != 0 && Math.Abs(diff) >= threshold.MinimumAbsoluteDifference
                    && (!percent.HasValue || Math.Abs(percent.Value) / 100 >= threshold.MinimumRelativeDifference);
                return new ReferenceMetric(metric, value, referenceValue, diff, Math.Abs(diff), percent,
                    self ? "Reference" : deviates ? "ReferenceDeviation" : "WithinTolerance", percent is null ? ["UndefinedOrNonFinitePercentage"] : []);
            }).ToArray();
            var status = self ? "Reference" : metrics.Any(m => m.Status == "ReferenceDeviation") ? "ReferenceDeviation"
                : metrics.Any(m => m.Status == "Unavailable") ? "ReferenceUnavailable" : "WithinTolerance";
            var uncertainties = metrics.Where(m => m.Status == "Unavailable").SelectMany(m => m.Reasons).ToList();
            uncertainties.Add("DescriptiveOnly_NotABalanceError");
            if (s.Enchantment is not null) uncertainties.Add("EnchantedCandidate_BaseStatsOnly");
            if (BalanceAudit.Cohort(e) != BalanceAudit.Cohort(winners[identity.FormKey])) uncertainties.Add("StructuralDifferencesRequireReview");
            records.Add(new(s.FormKey, status, identity.FormKey, uncertainties.Distinct().Order(StringComparer.Ordinal).ToArray(), metrics));
        }
        var eligible = equipment.Where(e => settings.EnableReferenceComparison && e.Eligibility.AnalysisEligible
            && (settings.IncludeEnchantedAnalysisCandidates || e.Snapshot.Enchantment is null)).ToArray();
        var results = records.ToDictionary(r => r.FormKey, StringComparer.OrdinalIgnoreCase);
        var coverage = eligible.GroupBy(Key).OrderBy(g => g.Key.Type, StringComparer.Ordinal).ThenBy(g => g.Key.Category, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Material, StringComparer.Ordinal).ThenBy(g => g.Key.Armor, StringComparer.Ordinal)
            .Select(g => new ReferenceCoverage(g.Key.Type, g.Key.Category, g.Key.Material, g.Key.Armor, g.Count(),
                g.Count(e => results[e.Snapshot.FormKey].ReferenceFormKey is not null), g.Count(e => results[e.Snapshot.FormKey].Status == "NoReference"),
                g.Count(e => results[e.Snapshot.FormKey].Status == "ReferenceUnavailable"), g.Count(e => results[e.Snapshot.FormKey].Status == "ReferenceDeviation"))).ToArray();
        var summary = new ReferenceSummary(catalog.Count, resolved.Count(r => r.Status == "Resolved"), resolved.Count(r => r.Status == "Missing"),
            resolved.Count(r => r.Status == "Unusable"), eligible.Length, coverage.Sum(c => c.Matched), coverage.Sum(c => c.NoReference),
            coverage.Sum(c => c.ReferenceUnavailable), records.Count(r => r.Status == "Reference"), records.Count(r => r.Status == "ReferenceDeviation"),
            records.Count(r => r.Metrics.Any(m => m.Status is "WithinTolerance" or "ReferenceDeviation")), statisticalAnalyzedRecords);
        return new("winning-canonical-1", settings.EnableReferenceComparison, summary, resolved.ToArray(), records.ToArray(), coverage);
    }
}

