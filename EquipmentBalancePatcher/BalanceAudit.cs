using System.Globalization;
using System.Text.Json;

namespace EquipmentBalancePatcher;

public sealed class DeviationThreshold
{
    public double MinimumAbsoluteDifference { get; set; }
    public double MinimumRelativeDifference { get; set; } = 0.25;
    public DeviationThreshold() { }
    public DeviationThreshold(double absolute) => MinimumAbsoluteDifference = absolute;
}
public sealed class BalanceThresholds
{
    public DeviationThreshold Damage { get; set; } = new(3);
    public DeviationThreshold ArmorRating { get; set; } = new(8);
    public DeviationThreshold Weight { get; set; } = new(2);
    public DeviationThreshold Speed { get; set; } = new(0.15);
    public DeviationThreshold Reach { get; set; } = new(0.2);
    public DeviationThreshold CriticalDamage { get; set; } = new(2);
    public DeviationThreshold Stagger { get; set; } = new(0.2);
    public DeviationThreshold For(string metric) => metric switch
    {
        "Damage" => Damage, "ArmorRating" => ArmorRating, "Weight" => Weight, "Speed" => Speed,
        "Reach" => Reach, "CriticalDamage" => CriticalDamage, "Stagger" => Stagger,
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };
}
public sealed record BalanceCohort(string RecordType, string Category, string Material, string? ArmorType,
    string? AnimationType, string? Skill, string? EquipmentType, uint? SlotMask, bool? ShieldFlag, string Flags);
public sealed record ReferenceDistribution(int Count, double? Minimum, double? Maximum, double? Mean,
    double? Median, double? Mad, double? Q1, double? Q3);
public sealed record PluginConcentration(SortedDictionary<string, int> DefiningPlugins,
    SortedDictionary<string, int> WinningPlugins, double MaximumDefiningShare, double MaximumWinningShare,
    bool Concentrated);
public sealed record BalanceGroup(string CohortId, BalanceCohort Cohort, string Metric,
    string[] MemberFormKeys, string[] ReferenceDesignIds, string[] ReferenceFormKeys,
    int CandidateCount, int InvalidReferenceMetrics, ReferenceDistribution Distribution,
    PluginConcentration Concentration, int PossibleDuplicateRecords, string Status, string[] Warnings);
public sealed record BalanceEvaluation(string FormKey, string DesignId, string CohortId, string Metric,
    double? Value, string Status, string[] PeerFormKeys, ReferenceDistribution Reference,
    PluginConcentration Concentration, double? SignedDifference, double? AbsoluteDifference,
    double? PercentageDifference, double? RobustDeviationScore);
public sealed record BalanceFinding(string FormKey, string? EditorId, string? Name, string DefiningPlugin,
    string WinningPlugin, string RecordType, string Category, string Material, string CohortId, string Metric,
    double Value, ReferenceDistribution Reference, string[] PeerFormKeys, double SignedDifference,
    double AbsoluteDifference, double? PercentageDifference, double? RobustDeviationScore,
    string FindingType, string Severity, string Confidence, string Explanation, string[] FalsePositiveReasons);
public sealed record BalanceRecordStatus(string FormKey, string RecordType, string DefiningPlugin,
    string WinningPlugin, string DesignId, string Status, string[] Reasons);
public sealed record MaterialProgression(string StructuralId, string RecordType, string Category,
    string? ArmorType, string Metric, string LowerTier, string HigherTier, string LowerCohortId,
    string HigherCohortId, double? LowerMedian, double? HigherMedian, string Status, string Explanation);
public sealed record BalanceSummary(int InventoryRecords, int EligibilityCandidates, int EnabledCandidates,
    int AnalyzedRecords, int ExcludedRecords, int InsufficientPeerRecords, int InsufficientEvidenceRecords,
    int InvalidMetricRecords, int StatisticalFindings, int PotentialConsistencyIssues, int InsufficientEvidenceFindings);
public sealed record BalanceAnalysis(string PolicyVersion, bool Enabled, string DesignIdentityStrategy,
    BalanceSummary Summary, BalanceRecordStatus[] Records, BalanceGroup[] Groups,
    BalanceEvaluation[] Evaluations, BalanceFinding[] Findings, MaterialProgression[] MaterialProgression);

public static class BalanceAudit
{
    public const string PolicyVersion = "stored-independent-1";
    // Concentration is an evidence gate, not an excuse to broaden material or discard authored designs.
    public const double MaximumTrustedPluginShare = 0.6;
    private static readonly string[] AllMetrics = ["Damage", "ArmorRating", "Weight", "Speed", "Reach", "CriticalDamage", "Stagger"];
    public static string[] Metrics(string type) => type == "WEAP"
        ? ["Damage", "Speed", "Reach", "Weight", "CriticalDamage", "Stagger"] : ["ArmorRating", "Weight"];
    public static void Validate(Settings settings)
    {
        if (settings.MinimumIndependentPeers < 3) throw new ArgumentOutOfRangeException(nameof(settings.MinimumIndependentPeers), "At least three independent peers are required.");
        if (!double.IsFinite(settings.RobustDeviationThreshold) || settings.RobustDeviationThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(settings.RobustDeviationThreshold));
        if (settings.BalanceThresholds is null) throw new ArgumentException("Balance thresholds are required.");
        foreach (var metric in AllMetrics)
        {
            var threshold = settings.BalanceThresholds.For(metric);
            if (threshold is null || !double.IsFinite(threshold.MinimumAbsoluteDifference) || threshold.MinimumAbsoluteDifference < 0
                || !double.IsFinite(threshold.MinimumRelativeDifference) || threshold.MinimumRelativeDifference < 0)
                throw new ArgumentException("Finite nonnegative thresholds required for " + metric);
        }
    }
    public static BalanceCohort Cohort(AuditedEquipment e)
    {
        var s = e.Snapshot;
        return new(s.RecordType, e.Classification.Category!, e.Classification.Material!, s.ArmorType,
            s.AnimationType, s.Skill, s.EquipmentType?.FormKey.ToUpperInvariant(), s.SlotMask, s.ShieldFlag,
            string.Join(";", s.Flags.Order(StringComparer.Ordinal)));
    }
    public static string CohortId(BalanceCohort cohort) => JsonSerializer.Serialize(cohort);
    private static string DesignId(EquipmentSnapshot s) => s.RecordType + ":" + s.FormKey;
    public static bool Valid(double? value) => value.HasValue && double.IsFinite(value.Value) && value.Value >= 0;
    public static ReferenceDistribution Describe(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        if (values.Any(v => !double.IsFinite(v) || v < 0)) throw new ArgumentException("Invalid reference metric.");
        if (values.Length == 0) return new(0, null, null, null, null, null, null, null);
        double Quantile(double[] sorted, double p)
        {
            var position = (sorted.Length - 1) * p;
            var low = (int)Math.Floor(position); var high = (int)Math.Ceiling(position); var fraction = position - low;
            return sorted[low] * (1 - fraction) + sorted[high] * fraction;
        }
        var median = Quantile(values, 0.5);
        var deviations = values.Select(v => Math.Abs(v - median)).Order().ToArray();
        var maximum = values[^1];
        var mean = maximum == 0 ? 0 : maximum * Math.Min(1, values.Sum(v => v / maximum) / values.Length);
        return new(values.Length, values[0], maximum, mean, median,
            Quantile(deviations, 0.5), Quantile(values, 0.25), Quantile(values, 0.75));
    }
    public static PluginConcentration Concentration(IEnumerable<EquipmentSnapshot> source)
    {
        var records = source.ToArray();
        SortedDictionary<string, int> Counts(Func<EquipmentSnapshot, string> selector) => new(records.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Select(selector).Order(StringComparer.Ordinal).First(), g => g.Count()), StringComparer.Ordinal);
        var origins = Counts(s => s.DefiningPlugin); var winners = Counts(s => s.WinningPlugin);
        var originShare = records.Length == 0 ? 0 : origins.Values.Max() / (double)records.Length;
        var winnerShare = records.Length == 0 ? 0 : winners.Values.Max() / (double)records.Length;
        return new(origins, winners, originShare, winnerShare,
            originShare > MaximumTrustedPluginShare || winnerShare > MaximumTrustedPluginShare);
    }
    private static string Status(ReferenceDistribution d, PluginConcentration concentration, int minimum) =>
        d.Count == 0 ? "NoReference" : d.Count < minimum ? "InsufficientPeers"
        : concentration.Concentrated ? "ConcentratedReference" : d.Minimum == d.Maximum ? "ZeroVariance"
        : d.Mad == 0 ? "ZeroMad" : "SufficientPeers";
    private static string[] Warnings(PluginConcentration c, int duplicates) => new[]
        { c.Concentrated ? "PluginConcentration" : null, duplicates > 0 ? "PossibleDuplicateDesigns_NotDeduplicated" : null,
          "IndependentAuthorshipNotProven_FormKeySampling" }.OfType<string>().ToArray();

    public static BalanceAnalysis Build(AuditedEquipment[] equipment, Settings settings, CancellationToken cancel = default)
    {
        Validate(settings);
        var statuses = new List<BalanceRecordStatus>();
        var groups = new List<BalanceGroup>(); var evaluations = new List<BalanceEvaluation>(); var findings = new List<BalanceFinding>();
        bool Enabled(AuditedEquipment e) => settings.EnableBalanceAudit && e.Eligibility.AnalysisEligible
            && (settings.IncludeEnchantedAnalysisCandidates || e.Snapshot.Enchantment is null);
        foreach (var e in equipment)
        {
            cancel.ThrowIfCancellationRequested();
            var reasons = e.Eligibility.AnalysisReasons.ToList();
            if (!settings.EnableBalanceAudit) reasons.Add("BalanceAuditDisabled");
            if (!settings.IncludeEnchantedAnalysisCandidates && e.Snapshot.Enchantment is not null) reasons.Add("EnchantedAnalysisDisabled");
            statuses.Add(new(e.Snapshot.FormKey, e.Snapshot.RecordType, e.Snapshot.DefiningPlugin, e.Snapshot.WinningPlugin,
                DesignId(e.Snapshot), Enabled(e) ? "Candidate" : "Excluded", reasons.Distinct().Order(StringComparer.Ordinal).ToArray()));
        }
        // No fuzzy/numeric/name deduplication: explicit template families are all ineligible under current rules.
        foreach (var cohort in equipment.Where(Enabled).GroupBy(Cohort).OrderBy(g => CohortId(g.Key), StringComparer.Ordinal))
        {
            var members = cohort.OrderBy(e => e.Snapshot.FormKey, StringComparer.Ordinal).ToArray();
            var baseline = members.Where(e => e.Eligibility.BaselineEligible).Select(e => e.Snapshot).ToArray();
            var id = CohortId(cohort.Key);
            // Same origin, identity labels and complete raw metric vector are a warning only, never a merge key.
            var duplicates = baseline.GroupBy(s => JsonSerializer.Serialize(new { DefiningPlugin = s.DefiningPlugin.ToUpperInvariant(), s.EditorId, s.Name,
                Values = Metrics(s.RecordType).Select(m => Valid(InventoryAudit.Metric(s, m)) ? InventoryAudit.Metric(s, m) : null).ToArray() }))
                .Sum(g => Math.Max(0, g.Count() - 1));
            foreach (var metric in Metrics(cohort.Key.RecordType))
            {
                cancel.ThrowIfCancellationRequested();
                var references = baseline.Where(s => Valid(InventoryAudit.Metric(s, metric))).ToArray();
                var distribution = Describe(references.Select(s => InventoryAudit.Metric(s, metric)!.Value));
                var concentration = Concentration(references);
                groups.Add(new(id, cohort.Key, metric, members.Select(e => e.Snapshot.FormKey).ToArray(),
                    references.Select(DesignId).ToArray(), references.Select(s => s.FormKey).ToArray(), members.Length,
                    baseline.Length - references.Length, distribution, concentration, duplicates,
                    Status(distribution, concentration, settings.MinimumIndependentPeers), Warnings(concentration, duplicates)));
                foreach (var e in members)
                {
                    cancel.ThrowIfCancellationRequested();
                    var s = e.Snapshot; var value = InventoryAudit.Metric(s, metric);
                    var peers = references.Where(p => DesignId(p) != DesignId(s)).ToArray();
                    var d = Describe(peers.Select(p => InventoryAudit.Metric(p, metric)!.Value)); var pc = Concentration(peers);
                    var status = Valid(value) ? Status(d, pc, settings.MinimumIndependentPeers) : "InvalidMetric";
                    var difference = Valid(value) && d.Median.HasValue ? value - d.Median : null;
                    double? relative = d.Median > 1e-9 && difference.HasValue ? difference / d.Median : null;
                    if (relative.HasValue && !double.IsFinite(relative.Value)) relative = null;
                    double? percent = relative * 100;
                    if (percent.HasValue && !double.IsFinite(percent.Value)) percent = null;
                    double? robust = d.Mad > 0 && difference.HasValue ? 0.6745 * (difference / d.Mad) : null;
                    if (robust.HasValue && !double.IsFinite(robust.Value)) robust = null;
                    var peerKeys = peers.Select(p => p.FormKey).ToArray();
                    evaluations.Add(new(s.FormKey, DesignId(s), id, metric, Valid(value) ? value : null, status, peerKeys, d, pc,
                        difference, difference.HasValue ? Math.Abs(difference.Value) : null, percent, robust));
                    if (!difference.HasValue || difference == 0) continue;
                    var t = settings.BalanceThresholds.For(metric); var absolute = Math.Abs(difference.Value);
                    // Zero MAD has a stricter absolute/relative gate and never produces an invented robust score.
                    var factor = d.Mad == 0 ? 2 : 1;
                    var passesSize = absolute >= t.MinimumAbsoluteDifference * factor
                        && (!relative.HasValue || Math.Abs(relative.Value) >= t.MinimumRelativeDifference * factor);
                    var passesRobust = d.Mad == 0 || robust.HasValue && Math.Abs(robust.Value) >= settings.RobustDeviationThreshold;
                    if (!passesSize || !passesRobust) continue;
                    var trusted = d.Count >= settings.MinimumIndependentPeers && !pc.Concentrated && duplicates == 0;
                    var kind = !trusted ? "InsufficientEvidence" : d.Mad == 0 ? "StatisticalDeviation" : "PotentialConsistencyIssue";
                    var falsePositives = new List<string> { "IntentionalUniqueOrSpecializedVariant", "StoredMetricsDoNotMeasureCombatPower",
                        "UnprovenIndependentDesignIdentity", "ProvisionalThresholds" };
                    if (s.Enchantment is not null) falsePositives.Add("EnchantedVariantComparedOnBaseStatsOnly");
                    if (d.Mad == 0) falsePositives.Add("ZeroMad_NoRobustScore");
                    if (!relative.HasValue) falsePositives.Add("NearZeroMedian_NoPercentage");
                    falsePositives.AddRange(Warnings(pc, duplicates));
                    string F(double v) => v.ToString("G8", CultureInfo.InvariantCulture);
                    var explanation = $"Stored {metric} {F(value!.Value)} is {(difference > 0 ? "above" : "below")} leave-design-out peer median {F(d.Median!.Value)} by {F(absolute)}; {d.Count} unenchanted reference designs, range {F(d.Minimum!.Value)}–{F(d.Maximum!.Value)}. "
                        + (trusted ? "Provisional deviation gates passed; manual review is required." : "Deviation gates passed, but peer count, concentration or possible duplicates prevents a reliable consistency conclusion.");
                    findings.Add(new(s.FormKey, s.EditorId, s.Name, s.DefiningPlugin, s.WinningPlugin, s.RecordType,
                        e.Classification.Category!, e.Classification.Material!, id, metric, value.Value, d, peerKeys,
                        difference.Value, absolute, percent, robust, kind, trusted ? "Review" : "Info",
                        !trusted ? "Low" : d.Mad == 0 ? "Moderate" : "SupportedForReview", explanation,
                        falsePositives.Distinct().Order(StringComparer.Ordinal).ToArray()));
                }
            }
        }
        var orderedEvaluations = evaluations.OrderBy(e => e.FormKey, StringComparer.Ordinal).ThenBy(e => e.Metric, StringComparer.Ordinal).ToArray();
        var byRecord = orderedEvaluations.GroupBy(e => e.FormKey).ToDictionary(g => g.Key, g => g.ToArray());
        statuses = statuses.Select(s => s.Status != "Candidate" ? s : s with
        {
            Status = byRecord[s.FormKey].Any(e => e.Status is "SufficientPeers" or "ZeroVariance" or "ZeroMad") ? "Analyzed" : "InsufficientEvidence",
            Reasons = byRecord[s.FormKey].Select(e => e.Status).Where(s => s is not ("SufficientPeers" or "ZeroVariance" or "ZeroMad")).Distinct().Order(StringComparer.Ordinal).ToArray()
        }).ToList();
        var summary = new BalanceSummary(equipment.Length, equipment.Count(e => e.Eligibility.AnalysisEligible), equipment.Count(Enabled),
            statuses.Count(s => s.Status == "Analyzed"), statuses.Count(s => s.Status == "Excluded"),
            byRecord.Count(g => g.Value.All(e => e.Status is "InsufficientPeers" or "NoReference" or "InvalidMetric")),
            statuses.Count(s => s.Status == "InsufficientEvidence"), byRecord.Count(g => g.Value.Any(e => e.Status == "InvalidMetric")),
            findings.Count, findings.Count(f => f.FindingType == "PotentialConsistencyIssue"), findings.Count(f => f.FindingType == "InsufficientEvidence"));
        return new(PolicyVersion, settings.EnableBalanceAudit, "OneRecordFormKeyPerDesign_NoNumericDeduplication_TemplatesExcluded", summary,
            statuses.OrderBy(s => s.FormKey, StringComparer.Ordinal).ToArray(), groups.ToArray(), orderedEvaluations,
            findings.OrderBy(f => f.RecordType, StringComparer.Ordinal).ThenBy(f => f.Material, StringComparer.Ordinal)
                .ThenBy(f => f.Category, StringComparer.Ordinal).ThenBy(f => f.FormKey, StringComparer.Ordinal).ThenBy(f => f.Metric, StringComparer.Ordinal).ToArray(),
            settings.EnableMaterialProgressionReport ? Progression(groups, settings.MinimumIndependentPeers) : []);
    }
    private static MaterialProgression[] Progression(List<BalanceGroup> groups, int minimum)
    {
        // A small declared anchor only; no complete vanilla material ladder is assumed.
        // Other tiers remain available as descriptive cohort medians/ranges, without expected ordering.
        return groups.Where(g => g.Metric is "Damage" or "ArmorRating")
            .GroupBy(g => CohortId(g.Cohort with { Material = "" }))
            .OrderBy(g => g.Key, StringComparer.Ordinal).SelectMany(g =>
            {
                var iron = g.SingleOrDefault(x => x.Cohort.Material == "Iron");
                var steel = g.SingleOrDefault(x => x.Cohort.Material == "Steel");
                if (iron is null || steel is null) return Array.Empty<MaterialProgression>();
                var reliable = iron.Distribution.Count >= minimum && steel.Distribution.Count >= minimum
                    && !iron.Concentration.Concentrated && !steel.Concentration.Concentrated
                    && iron.PossibleDuplicateRecords == 0 && steel.PossibleDuplicateRecords == 0;
                var inverted = steel.Distribution.Median < iron.Distribution.Median;
                return new[] { new MaterialProgression(g.Key, iron.Cohort.RecordType, iron.Cohort.Category, iron.Cohort.ArmorType,
                    iron.Metric, "Iron", "Steel", iron.CohortId, steel.CohortId, iron.Distribution.Median, steel.Distribution.Median,
                    !reliable ? "InsufficientEvidence" : inverted ? "PossibleProgressionInversion" : "DescriptiveProgression",
                    "Provisional Iron→Steel anchor; compares independent cohort medians, not a universal hierarchy or a prescribed stat. Review authored progression and distribution overlap.") };
            }).ToArray();
    }
}
