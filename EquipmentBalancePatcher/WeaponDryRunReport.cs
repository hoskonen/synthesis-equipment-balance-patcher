using System.Globalization;
using System.Text;

namespace EquipmentBalancePatcher;

public static class WeaponDryRunReport
{
    public const int DisplayLimit = 10;

    public static string Render(AuditReport report)
    {
        var text = new StringBuilder();
        void Line(string value = "") => text.Append(value).Append('\n');
        void Count(string label, int count) => Line($"{label.PadRight(39)}{count.ToString(CultureInfo.InvariantCulture)}");
        void Section(string title) { Line(); Line(title); Line(new string('-', title.Length)); }
        var records = (report.WeaponEvidenceAnalysis?.Records ?? []).DistinctBy(r => r.FormKey, StringComparer.OrdinalIgnoreCase).ToArray();
        var ordinary = records.Where(r => r.OrdinaryInspected).ToArray();
        var equipment = report.Equipment.ToDictionary(e => e.Snapshot.FormKey, StringComparer.OrdinalIgnoreCase);
        var references = (report.ReferenceAnalysis?.Catalog ?? []).ToDictionary(r => r.Identity.FormKey, StringComparer.OrdinalIgnoreCase);
        bool Resolved(WeaponEvidenceResult r) => r.ReferenceFormKey is not null
            && references.TryGetValue(r.ReferenceFormKey, out var reference) && reference.Status == "Resolved";
        bool Self(WeaponEvidenceResult r) => string.Equals(r.FormKey, r.ReferenceFormKey, StringComparison.OrdinalIgnoreCase);
        var eligible = ordinary.Where(r => equipment[r.FormKey].Eligibility.AnalysisEligible && Resolved(r) && !Self(r)).ToArray();
        var compared = eligible.Where(r => r.ReferenceMetrics.Any(m => m.Status is "WithinTolerance" or "ReferenceDeviation")).ToArray();
        var damage = compared.Select(r => (Record: r, Metric: r.ReferenceMetrics.FirstOrDefault(m => m.Metric == "Damage")))
            .Where(x => x.Metric is not null && BalanceAudit.Valid(x.Metric.CandidateValue) && BalanceAudit.Valid(x.Metric.ReferenceValue)
                && x.Metric.Status is "WithinTolerance" or "ReferenceDeviation").Select(x => (x.Record, Metric: x.Metric!)).ToArray();
        var deviations = damage.Where(x => x.Metric.Status == "ReferenceDeviation" && x.Metric.SignedDifference is { } d && double.IsFinite(d) && d != 0).ToArray();
        var higher = deviations.Where(x => x.Metric.SignedDifference > 0).ToArray();
        var lower = deviations.Where(x => x.Metric.SignedDifference < 0).ToArray();
        bool Ranked(ReferenceMetric m) => m.PercentageDifference is { } p && double.IsFinite(p);
        var unranked = deviations.Where(x => !Ranked(x.Metric)).OrderBy(x => x.Record.FormKey, StringComparer.Ordinal).ToArray();
        Line($"Equipment Balance Patcher v{report.PatcherVersion}");
        Line("Mode: DRY RUN — no plugin changes");
        Section("Ordinary Weapon Audit");
        Count("Total weapons in inventory:", report.Summary.Weapons);
        Count("Weapons inspected:", ordinary.Length);
        Count("Compared with references (non-self):", compared.Length);
        Count("Base damage comparisons:", damage.Length);
        Count("Canonical references themselves:", ordinary.Count(r => Resolved(r) && Self(r)));
        Count("Higher damage candidates:", higher.Length);
        Count("Lower damage candidates:", lower.Length);
        Count("Undefined percentage (not ranked):", unranked.Length);
        Count("Excluded from ordinary inspection:", records.Count(r => !r.OrdinaryInspected));
        Count("No reviewed canonical reference:", ordinary.Count(r => r.ReferenceStatus == "NoReference"));
        Count("No numerical comparison (unassessed):", ordinary.Count(r => !Self(r)) - compared.Length);
        Count("Compared but damage unavailable:", compared.Length - damage.Length);
        Line("Differences are review candidates, not confirmed balance problems. Unassessed does not mean balanced.");

        void Item(int index, WeaponEvidenceResult r, ReferenceMetric metric)
        {
            var e = equipment[r.FormKey]; var s = e.Snapshot; var reference = references[r.ReferenceFormKey!];
            Line($"{index.ToString(CultureInfo.InvariantCulture)}. {Clean(s.Name ?? s.EditorId ?? "(unnamed weapon)")} — {e.Classification.Category} / {e.Classification.Material}");
            var percent = Ranked(metric) ? Signed(metric.PercentageDifference!.Value) + "%" : "percentage undefined";
            Line($"   Damage: {Number(metric.CandidateValue!.Value)} vs {Number(metric.ReferenceValue!.Value)} (difference {Signed(metric.SignedDifference!.Value)}; {percent})");
            Line($"   Reference: {Clean(reference.Identity.Name)} [{reference.Identity.FormKey}] — winner {Clean(reference.WinningPlugin ?? "unavailable")}");
            Line($"   Source: {Clean(s.DefiningPlugin)}" + (string.Equals(s.DefiningPlugin, s.WinningPlugin, StringComparison.OrdinalIgnoreCase) ? "" : $"; winner {Clean(s.WinningPlugin)}"));
            Line($"   FormKey: {s.FormKey}");
            var notes = new List<string>();
            if (r.Reasons.Contains("StructuralDifferencesRequireReview")) notes.Add("structure differs from reference");
            if (r.Reasons.Contains("EnchantedCandidate_BaseStatsOnly")) notes.Add("enchanted weapon; base damage only");
            if (r.Status == "ConflictingEvidence") notes.Add("material evidence conflicts");
            if (notes.Count > 0) Line("   Review: " + string.Join("; ", notes) + ".");
        }
        void List(string title, IEnumerable<(WeaponEvidenceResult Record, ReferenceMetric Metric)> candidates)
        {
            Section(title);
            var ranked = candidates.ToArray();
            if (ranked.Length == 0) { Line("None with a defined percentage."); return; }
            var shown = ranked.Take(DisplayLimit).ToArray();
            for (var i = 0; i < shown.Length; i++) Item(i + 1, shown[i].Record, shown[i].Metric);
            if (ranked.Length > shown.Length) Line($"Showing {DisplayLimit} of {ranked.Length.ToString(CultureInfo.InvariantCulture)}; remaining candidates are in the detailed reports.");
        }
        List("Higher Damage — Review Candidates", higher.Where(x => Ranked(x.Metric)).OrderByDescending(x => x.Metric.PercentageDifference)
            .ThenByDescending(x => x.Metric.AbsoluteDifference).ThenBy(x => x.Record.FormKey, StringComparer.Ordinal));
        List("Lower Damage — Review Candidates", lower.Where(x => Ranked(x.Metric)).OrderBy(x => x.Metric.PercentageDifference)
            .ThenByDescending(x => x.Metric.AbsoluteDifference).ThenBy(x => x.Record.FormKey, StringComparer.Ordinal));
        if (unranked.Length > 0)
        {
            Section("Undefined Percentage — Unranked Review Candidates");
            Line("Kept outside percentage rankings; existing absolute tolerance results are retained. Ordered by FormKey.");
            for (var i = 0; i < Math.Min(DisplayLimit, unranked.Length); i++) Item(i + 1, unranked[i].Record, unranked[i].Metric);
            if (unranked.Length > DisplayLimit) Line("Additional unranked candidates remain in detailed reports.");
        }
        Section("Material Evidence");
        Count("Keyword/name agreements:", ordinary.Count(r => r.NameAgreement == "AgreesWithKeyword"));
        Count("Ordinary material conflicts:", ordinary.Count(r => r.Status == "ConflictingEvidence"));
        Count("Excluded special-weapon conflicts:", records.Count(r => !r.OrdinaryInspected && (r.KeywordMaterials.Length > 1 || r.NameAgreement == "ConflictsWithKeyword")));
        Count("Incomplete material evidence:", ordinary.Count(r => r.Status == "IncompleteEvidence"));
        Line("Names are weak evidence; keyword-only or incomplete evidence is not an error.");
        Section("Detailed findings");
        Line("  weapon-evidence.csv"); Line("  reference-comparisons.csv");
        return text.ToString();
    }

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Signed(double value) => (value > 0 ? "+" : "") + Number(value);
    private static string Clean(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
