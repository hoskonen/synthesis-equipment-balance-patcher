using System.Text.RegularExpressions;

namespace EquipmentBalancePatcher;

public sealed record WeaponEvidenceResult(string FormKey, bool OrdinaryInspected, string Status, string Confidence,
    ReferenceSnapshot[] MaterialKeywords, string[] KeywordMaterials, string KeywordStatus, string[] NameMaterials,
    string NameAgreement, string CraftingEvidenceStatus, string? ReferenceFormKey, string ReferenceStatus,
    ReferenceMetric[] ReferenceMetrics, string[] Reasons);
public sealed record WeaponEvidenceSummary(int WeaponRecords, int OrdinaryWeaponsInspected, int MatchingKeywordName,
    int ConflictingEvidence, int KeywordOnly, int NameOnly, int NoMaterialEvidence, int AmbiguousNames,
    int UsableReferences, int ReferenceDeviations, int ExcludedWeapons, SortedDictionary<string, int> ExclusionReasons);
public sealed record WeaponEvidenceAnalysis(string PolicyVersion, string CraftingEvidenceStatus, WeaponEvidenceSummary Summary)
{
    // CSV carries the single compact per-weapon view; JSON adds only policy and summary, not duplicate metric rows.
    [System.Text.Json.Serialization.JsonIgnore]
    public WeaponEvidenceResult[] Records { get; init; } = [];
}

public static class WeaponEvidenceAudit
{
    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
        { "Sword", "Dagger", "WarAxe", "Mace", "Greatsword", "Battleaxe", "Warhammer", "Bow" };
    // Inspection can describe missing/contradictory materials without making these weapons AnalysisEligible.
    private static readonly HashSet<string> MaterialOnlyReasons = new(StringComparer.Ordinal)
        { "ClassificationNotConfident", "UnknownMaterial", "ConflictingMaterialKeywords", "UnreviewedMaterialEvidence" };
    private static readonly Regex MaterialWords = new(
        @"(?<![\p{L}\p{N}\p{M}_])(Iron|Steel|Elven|Dwarven|Orcish|Glass|Ebony|Daedric|Dragonbone)(?![\p{L}\p{N}\p{M}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex CustomWords = new(
        @"(?<![\p{L}\p{N}\p{M}_])(spear|halberd|rapier|pike|quarterstaff|polearm|trident|glaive|scythe)(?![\p{L}\p{N}\p{M}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    // Reviewed English name hints from the supplied inventory, local to this report. No production eligibility changes.
    private static readonly HashSet<string> NamedDesignHints = new(StringComparer.OrdinalIgnoreCase)
        { "Blade of Woe", "Dragonbane", "Nightingale Bow", "Harkon's Sword", "Bolar's Oathblade", "Bow of the Hunt",
          "Nightingale Blade", "Dwarven Black Bow of Fate", "Miraak's Sword", "Alessandra's Dagger", "Hjalti's Sword",
          "Valdr's Lucky Dagger", "Blade of Sacrifice", "Ghostblade", "Headman's Cleaver", "Tyrant's Bane", "Namira's Itch",
          "Fang of Haynekhtnamet", "Champion's Cudgel", "The Woodsman's Friend", "Red Eagle's Bane", "Poacher's Axe", "Headsman's Axe",
          "Boneshaver", "Scourge", "Torment", "Goldbrand", "Zephyr", "Hoarfrost", "Zahkrii Do Dovahkiin", "Nettlebane",
          "Trollsbane", "Windshear", "Bloodthorn", "The Longhammer", "Briarheart Geis", "The Pale Blade", "Chillrend" };
    private static readonly Regex UtilityNames = new(@"(?<![\p{L}\p{N}\p{M}_])(fishing rod|fishing knife|pickaxe|woodcutter['’]s axe)(?![\p{L}\p{N}\p{M}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private const string CraftingStatus = "Deferred_NotExtracted";

    public static WeaponEvidenceAnalysis Build(AuditedEquipment[] equipment, ReferenceAnalysis references, CancellationToken cancel = default)
    {
        var comparisons = references.Records.ToDictionary(r => r.FormKey, StringComparer.OrdinalIgnoreCase);
        var results = new List<WeaponEvidenceResult>();
        foreach (var e in equipment.Where(e => e.Snapshot.RecordType == "WEAP").OrderBy(e => e.Snapshot.FormKey, StringComparer.Ordinal))
        {
            cancel.ThrowIfCancellationRequested();
            var s = e.Snapshot;
            var keywords = s.Keywords.Where(k => KeywordCatalog.Entries.TryGetValue(k.FormKey, out var entry) && entry.Kind == "WeaponMaterial")
                .DistinctBy(k => k.FormKey, StringComparer.OrdinalIgnoreCase).OrderBy(k => k.FormKey, StringComparer.Ordinal).ToArray();
            var materials = keywords.Where(k => k.Status == "Resolved").Select(k => KeywordCatalog.Entries[k.FormKey].Value)
                .Distinct().Order(StringComparer.Ordinal).ToArray();
            var names = MaterialWords.Matches(s.Name ?? "").Select(m => m.Value.ToLowerInvariant())
                .Select(n => char.ToUpperInvariant(n[0]) + n[1..]).Distinct().Order(StringComparer.Ordinal).ToArray();
            var keywordStatus = materials.Length switch { 0 => "Missing", 1 => "SingleMaterial", _ => "MultipleMaterials" };
            var agreement = names.Length > 1 ? "AmbiguousNameEvidence"
                : names.Length == 0 ? materials.Length == 0 ? "NoMaterialEvidence" : "KeywordOnlyEvidence"
                : materials.Length == 0 ? "NameOnlyEvidence"
                : names.Length == 1 && materials.Length == 1 && names[0] == materials[0] ? "AgreesWithKeyword" : "ConflictsWithKeyword";
            var exclusions = e.Eligibility.AnalysisReasons.Where(r => !MaterialOnlyReasons.Contains(r)).ToList();
            if (!Categories.Contains(e.Classification.Category ?? "")) exclusions.Add(e.Classification.Category == "Crossbow" ? "CrossbowsDeferred" : "UnsupportedOrdinarySubtype");
            if (CustomWords.IsMatch(s.Name ?? "")) exclusions.Add("CustomWeaponNameHintRequiresReview");
            if (NamedDesignHints.Contains((s.Name ?? "").Trim())) exclusions.Add("NamedSpecialDesignHintRequiresReview");
            if (UtilityNames.IsMatch(s.Name ?? "") || new[] { "Knife", "Fork", "Test Vorpal Sword" }.Contains((s.Name ?? "").Trim(), StringComparer.OrdinalIgnoreCase))
                exclusions.Add("UtilityOrTestWeaponNameHintRequiresReview");
            var inspected = exclusions.Count == 0;
            var reasons = new List<string>(exclusions);
            reasons.Add("NameIsWeakEvidence_NotMaterialProof");
            reasons.Add("CraftingEvidenceNotExtracted_NotNoRecipe");
            if (s.Name is null) reasons.Add("DisplayNameUnavailable");
            if (keywords.Any(k => k.Status != "Resolved")) reasons.Add("UnresolvedMaterialKeyword");
            if (materials.Length == 0) reasons.Add("MissingReviewedMaterialKeyword");
            if (materials.Length > 1) reasons.Add("MultipleReviewedMaterialTiers");
            if (keywords.Length > 1 && materials.Length == 1) reasons.Add("MultipleKeywordIdentitiesSameTier");
            if (names.Length == 0) reasons.Add("NoRecognizedMaterialWord");
            if (names.Length > 1) reasons.Add("AmbiguousMaterialWords");
            if (e.Classification.ReasonCodes.Contains("UnreviewedMaterialEvidence")) reasons.Add("UnreviewedMaterialEvidence");
            if (s.Enchantment is not null) reasons.Add("EnchantedWeapon_BaseStatsOnly");
            var status = !inspected ? "UnsupportedSpecialWeapon" : materials.Length > 1 ? "ConflictingEvidence"
                : names.Length > 1 ? "IncompleteEvidence" : agreement == "AgreesWithKeyword" ? "ConsistentEvidence"
                : agreement == "ConflictsWithKeyword" ? "ConflictingEvidence" : "IncompleteEvidence";
            var confidence = !inspected ? "Unsupported" : status == "ConflictingEvidence" ? "ReviewRequired"
                : e.Classification.Status == "Confident" && materials.Length == 1 ? "KeywordSupported_NameWeak" : "Incomplete";
            var comparison = comparisons[s.FormKey];
            // Reuse v0.1f values and thresholds verbatim, never assign a reference from name-only material.
            var useComparison = inspected && e.Eligibility.AnalysisEligible && e.Classification.Status == "Confident" && materials.Length == 1;
            var referenceStatus = useComparison ? comparison.Status : inspected ? "NotEligibleForReference" : "NotCompared_ExcludedFromEvidenceScope";
            if (!useComparison && !e.Eligibility.AnalysisEligible) reasons.AddRange(e.Eligibility.AnalysisReasons);
            if (useComparison) reasons.AddRange(comparison.Reasons);
            results.Add(new(s.FormKey, inspected, status, confidence, keywords, materials, keywordStatus, names, agreement,
                CraftingStatus, useComparison ? comparison.ReferenceFormKey : null, referenceStatus,
                useComparison ? comparison.Metrics : [], reasons.Distinct().Order(StringComparer.Ordinal).ToArray()));
        }
        var ordinary = results.Where(r => r.OrdinaryInspected).ToArray();
        var byKey = results.ToDictionary(r => r.FormKey, StringComparer.OrdinalIgnoreCase);
        var excludedKeys = results.Where(r => !r.OrdinaryInspected).Select(r => r.FormKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exclusionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in equipment.Where(e => excludedKeys.Contains(e.Snapshot.FormKey)))
        {
            var r = byKey[e.Snapshot.FormKey];
            var exclusions = e.Eligibility.AnalysisReasons.Where(reason => !MaterialOnlyReasons.Contains(reason))
                .Concat(r.Reasons.Where(reason => reason is "CrossbowsDeferred" or "UnsupportedOrdinarySubtype" or "CustomWeaponNameHintRequiresReview"
                    or "NamedSpecialDesignHintRequiresReview" or "UtilityOrTestWeaponNameHintRequiresReview"));
            foreach (var reason in exclusions.Distinct()) exclusionCounts[reason] = exclusionCounts.GetValueOrDefault(reason) + 1;
        }
        var summary = new WeaponEvidenceSummary(results.Count, ordinary.Length,
            ordinary.Count(r => r.NameAgreement == "AgreesWithKeyword"), ordinary.Count(r => r.Status == "ConflictingEvidence"),
            ordinary.Count(r => r.KeywordMaterials.Length > 0 && r.NameMaterials.Length == 0),
            ordinary.Count(r => r.KeywordMaterials.Length == 0 && r.NameMaterials.Length > 0),
            ordinary.Count(r => r.KeywordMaterials.Length == 0 && r.NameMaterials.Length == 0), ordinary.Count(r => r.NameMaterials.Length > 1),
            ordinary.Count(r => r.ReferenceMetrics.Any(m => m.Status is "WithinTolerance" or "ReferenceDeviation" or "Reference")),
            ordinary.Count(r => r.ReferenceStatus == "ReferenceDeviation"), excludedKeys.Count, exclusionCounts);
        return new("ordinary-keyword-name-1", CraftingStatus, summary) { Records = results.ToArray() };
    }
}
