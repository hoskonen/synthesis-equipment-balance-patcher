using System.Text.Json;

namespace EquipmentBalancePatcher;

public sealed record EffectiveField(string Name, JsonElement RawChildValue, JsonElement ProposedEffectiveValue,
    string? SourceFormKey, string? SourceWinningPlugin, string RuleId, string ResolutionStatus,
    int? TemplateDepth, string ValidationStatus, bool? ValuesDiffer);
public sealed record HypotheticalClassification(string Scenario, Classification Classification, Eligibility Eligibility,
    bool ClassificationChanged, bool MaterialChanged, bool CategoryChanged, bool EligibilityChanged,
    string[] RequiredValidation);
public sealed record EffectiveEquipment(string ChildFormKey, string? ChildEditorId, string? ChildName,
    string ChildWinningPlugin, TemplateChain Chain, EffectiveField[] Fields, Classification CurrentClassification,
    Eligibility CurrentEligibility, HypotheticalClassification[] HypotheticalClassifications, Diagnostic[] Diagnostics);
public sealed record TemplateRootSummary(string RecordType, string RootFormKey, string RootWinningPlugin,
    int TemplatedVariants, int EnchantedVariants, string[] OriginPlugins, string[] WinningPlugins,
    string[] ChildFormKeys, string SamplingStatus);

public static class EffectiveEquipmentInvestigator
{
    public const string PolicyVersion = "prototype-1";
    private static readonly JsonSerializerOptions ValueOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static EffectiveEquipment Investigate(AuditedEquipment equipment, TemplateResolver resolver,
        CancellationToken cancel = default)
    {
        var child = equipment.Snapshot;
        var chain = resolver.Resolve(child.FormKey, cancel);
        var success = chain.Status == "Resolved";
        var root = success ? resolver.GetRecord(chain.RootParent!) : child;
        var fields = new List<EffectiveField>();
        void Add(string name, object? raw, object? proposed, bool inherited, bool uncertainLocal = false)
        {
            cancel.ThrowIfCancellationRequested();
            var unresolved = inherited && chain.Status is not ("Resolved" or "NoTemplate");
            var source = inherited && success ? root : child;
            var rawJson = JsonSerializer.SerializeToElement(raw, ValueOptions);
            var proposedJson = JsonSerializer.SerializeToElement(unresolved ? null : proposed, ValueOptions);
            var missing = inherited && (proposedJson.ValueKind == JsonValueKind.Null && name is not ("EquipmentType" or "Skill")
                || proposed is ReferenceSnapshot reference && reference.Status != "Resolved"
                || proposed is ReferenceSnapshot[] keywords && keywords.Any(k => k.Status != "Resolved"));
            fields.Add(new(name, rawJson, proposedJson, unresolved ? null : source.FormKey,
                unresolved ? null : source.WinningPlugin,
                PolicyVersion + (inherited ? "/root-replacement/" : "/child-local/") + name,
                unresolved || missing ? "Unresolved" : inherited && success ? "ProposedInherited" : "Stored",
                unresolved ? null : inherited && success ? chain.Depth : 0,
                child.Template is not null && (inherited || uncertainLocal) ? "Unvalidated" : "StoredOnly",
                unresolved || missing ? null : rawJson.GetRawText() != proposedJson.GetRawText()));
        }
        Add("Weight", child.Weight, root.Weight, true);
        Add("EquipmentType", child.EquipmentType, root.EquipmentType, true);
        Add("Keywords", child.Keywords, root.Keywords, true);
        if (child.RecordType == "WEAP")
        {
            Add("Damage", child.Damage, root.Damage, true);
            Add("Speed", child.Speed, root.Speed, true);
            Add("Reach", child.Reach, root.Reach, true);
            Add("CriticalDamage", child.CriticalDamage, root.CriticalDamage, true);
            Add("Stagger", child.Stagger, root.Stagger, true);
            Add("AnimationType", child.AnimationType, root.AnimationType, true);
            Add("Skill", child.Skill, root.Skill, true);
        }
        else
        {
            Add("ArmorRating", child.ArmorRating, root.ArmorRating, true);
            Add("ArmorType", child.ArmorType, root.ArmorType, true);
            Add("SlotMask", child.SlotMask, root.SlotMask, true);
        }
        Add("Name", child.Name, child.Name, false);
        Add("Value", child.Value, child.Value, false, true);
        Add("Enchantment", child.Enchantment, child.Enchantment, false, true);
        Add("EnchantmentAmount", child.EnchantmentAmount, child.EnchantmentAmount, false, true);
        Add("HasScripts", child.HasScripts, child.HasScripts, false, true);
        Add("HasVirtualMachineAdapter", child.HasVirtualMachineAdapter, child.HasVirtualMachineAdapter, false, true);
        Add("Playable", child.Playable, child.Playable, false, true);
        Add("Flags", child.Flags, child.Flags, false, true);
        Add("ShieldFlag", child.ShieldFlag, child.ShieldFlag, false, true);

        var diagnostics = new List<Diagnostic>(chain.Diagnostics);
        var hypotheses = new List<HypotheticalClassification>();
        if (success)
        {
            var members = chain.Visited.Select(h => resolver.GetRecord(h.FormKey)).ToArray();
            if (!child.Keywords.Select(k => (k.FormKey, k.Status)).SequenceEqual(root.Keywords.Select(k => (k.FormKey, k.Status))))
                diagnostics.Add(new("TemplateKeywordSemanticsUnvalidated", "Child/root keyword sets differ; replacement, child preservation and chain union are separate hypotheses.")
                { Severity = "Info", Relevance = "Template" });
            var union = members.SelectMany(s => s.Keywords).DistinctBy(k => (k.FormKey.ToUpperInvariant(), k.Status))
                .OrderBy(k => k.FormKey, StringComparer.Ordinal).ThenBy(k => k.Status, StringComparer.Ordinal).ToArray();
            foreach (var (scenario, keywords) in new[] { ("RootReplacement", root.Keywords), ("ChildKeywords", child.Keywords), ("ChainKeywordUnion", union) })
            {
                cancel.ThrowIfCancellationRequested();
                // This detached view is used only for hypothetical reporting. Production snapshots stay untouched.
                // Remove the template gate only here to describe possible FUTURE eligibility; retain every other gate.
                var proposed = child with
                {
                    Template = null, StatSemantics = "ProposedInherited", Damage = root.Damage, ArmorRating = root.ArmorRating,
                    Weight = root.Weight, Speed = root.Speed, Reach = root.Reach, CriticalDamage = root.CriticalDamage,
                    Stagger = root.Stagger, AnimationType = root.AnimationType, Skill = root.Skill, ArmorType = root.ArmorType,
                    SlotMask = root.SlotMask, EquipmentType = root.EquipmentType, Keywords = keywords,
                    HasScripts = members.Any(s => s.HasScripts),
                    Playable = members.All(s => s.Playable == true) ? true : members.Any(s => s.Playable == false) ? false : null,
                    Flags = members.SelectMany(s => s.Flags).Distinct().Order(StringComparer.Ordinal).ToArray(),
                    ShieldFlag = members.Any(s => s.ShieldFlag == true),
                    ExtractionDiagnostics = members.SelectMany(s => s.ExtractionDiagnostics).Distinct().ToArray()
                };
                var classified = EquipmentClassifier.Classify(proposed);
                // Special artifact evidence must not be erased by a hypothetical keyword replacement.
                var special = members.Any(s => s.Keywords.Any(k => k.Status == "Resolved"
                    && KeywordCatalog.Entries.TryGetValue(k.FormKey, out var entry) && entry.Kind == "SpecialEquipment"));
                var eligibility = classified.Eligibility;
                if (special)
                    eligibility = eligibility with { BaselineEligible = false, AnalysisEligible = false,
                        BaselineReasons = eligibility.BaselineReasons.Append("SpecialEquipment").Distinct().Order(StringComparer.Ordinal).ToArray(),
                        AnalysisReasons = eligibility.AnalysisReasons.Append("SpecialEquipment").Distinct().Order(StringComparer.Ordinal).ToArray() };
                hypotheses.Add(new(scenario, classified.Classification, eligibility,
                    JsonSerializer.Serialize(equipment.Classification) != JsonSerializer.Serialize(classified.Classification),
                    equipment.Classification.Material != classified.Classification.Material,
                    equipment.Classification.Category != classified.Classification.Category,
                    equipment.Eligibility.InventoryStatisticsEligible != eligibility.InventoryStatisticsEligible
                        || equipment.Eligibility.BaselineEligible != eligibility.BaselineEligible
                        || equipment.Eligibility.AnalysisEligible != eligibility.AnalysisEligible,
                    ["InheritanceFieldsUnvalidated", "KeywordSemanticsUnvalidated", "ScriptsAndFlagsUnvalidated", "RootSamplingUnvalidated"]));
            }
            diagnostics.Add(new("ProposedInheritanceUnvalidated", "Traversal succeeded; inheritance and hypothetical eligibility are not engine-validated and do not affect production results.")
            { Severity = "Info", Relevance = "Template" });
        }
        return new(child.FormKey, child.EditorId, child.Name, child.WinningPlugin, chain,
            fields.OrderBy(f => f.Name, StringComparer.Ordinal).ToArray(), equipment.Classification, equipment.Eligibility,
            hypotheses.ToArray(), diagnostics.OrderBy(d => d.Code, StringComparer.Ordinal).ToArray());
    }
}
