namespace EquipmentBalancePatcher;

public static class EquipmentClassifier
{
    private static readonly Dictionary<string, string> Animations = new(StringComparer.Ordinal)
    {
        ["OneHandSword"] = "Sword", ["OneHandDagger"] = "Dagger", ["OneHandAxe"] = "WarAxe",
        ["OneHandMace"] = "Mace", ["TwoHandSword"] = "Greatsword", ["Bow"] = "Bow", ["Crossbow"] = "Crossbow"
    };

    // Biped bits are slot number minus 30, not the xEdit slot number itself.
    private static readonly (string Category, uint Primary, uint Allowed)[] ArmorProfiles =
    [
        ("Helmet", 1u << 0, (1u << 0) | (1u << 1) | (1u << 11) | (1u << 12) | (1u << 13)),
        ("Cuirass", 1u << 2, 1u << 2),
        ("Gauntlets", 1u << 3, (1u << 3) | (1u << 4)),
        ("Boots", 1u << 7, (1u << 7) | (1u << 8)),
        ("Shield", 1u << 9, 1u << 9)
    ];

    public static AuditedEquipment Classify(EquipmentSnapshot snapshot)
    {
        if (snapshot.RecordType is not ("WEAP" or "ARMO")) throw new ArgumentException("Only WEAP and ARMO snapshots are supported.", nameof(snapshot));
        var diagnostics = new List<Diagnostic>(snapshot.ExtractionDiagnostics);
        var evidence = new List<string>();
        var reasons = new List<string>();
        var known = new List<KnownKeyword>();
        foreach (var keyword in snapshot.Keywords)
        {
            if (keyword.Status != "Resolved")
            {
                diagnostics.Add(new("UnresolvedKeyword", $"Keyword {keyword.FormKey}: {keyword.Status}."));
                reasons.Add("IncompleteKeywordEvidence");
                continue;
            }
            if (KeywordCatalog.Entries.TryGetValue(keyword.FormKey, out var entry))
            {
                if (entry.Kind != "Unrelated")
                {
                    known.Add(entry);
                    evidence.Add($"{keyword.FormKey} ({entry.EditorId}) => {entry.Kind}:{entry.Value}");
                }
            }
            else
            {
                if (DiagnosticPolicy.PotentialKeyword(keyword.EditorId))
                {
                    diagnostics.Add(new("PotentialClassificationKeyword", $"Unreviewed keyword {keyword.FormKey} ({keyword.EditorId}); naming suggests classification relevance, not an authoritative mapping."));
                    if (keyword.EditorId is { } id && new[] { "ArmorMaterial", "WeapMaterial", "WeaponMaterial" }
                        .Any(token => id.Contains(token, StringComparison.OrdinalIgnoreCase))) reasons.Add("UnreviewedMaterialEvidence");
                }
            }
        }
        foreach (var (label, link) in References(snapshot))
            if (link.Status != "Resolved")
                diagnostics.Add(new("UnresolvedReference", $"{label} {link.FormKey}: {link.Status}."));

        string? category = null;
        string? handedness = null;
        var categoryKind = snapshot.RecordType == "WEAP" ? "WeaponCategory" : "ArmorCategory";
        var categories = Values(known, categoryKind);
        if (snapshot.RecordType == "WEAP")
        {
            evidence.Add("AnimationType:" + (snapshot.AnimationType ?? "Unavailable"));
            handedness = snapshot.AnimationType switch
            {
                "OneHandSword" or "OneHandDagger" or "OneHandAxe" or "OneHandMace" => "OneHanded",
                "TwoHandSword" or "TwoHandAxe" => "TwoHanded",
                "Bow" or "Crossbow" => "Ranged",
                _ => null
            };
            if (snapshot.Skill is not null)
            {
                evidence.Add("Skill:" + snapshot.Skill);
                var expectedSkill = handedness switch { "OneHanded" => "OneHanded", "TwoHanded" => "TwoHanded", "Ranged" => "Archery", _ => null };
                if (expectedSkill is not null && snapshot.Skill != expectedSkill) reasons.Add("ConflictingWeaponSkill");
            }
            if (snapshot.AnimationType == "TwoHandAxe")
            {
                if (categories.Length == 1 && categories[0] is "Battleaxe" or "Warhammer") category = categories[0];
                else reasons.Add(categories.Length > 0 ? "ConflictingWeaponCategory" : "MissingTwoHandedSubtype");
            }
            else if (snapshot.AnimationType is not null && Animations.TryGetValue(snapshot.AnimationType, out var animationCategory))
            {
                // Vanilla crossbows can use WeapTypeBow; animation disambiguates them.
                if (categories.Length == 0 || (categories.Length == 1 &&
                    (categories[0] == animationCategory || (animationCategory == "Crossbow" && categories[0] == "Bow"))))
                    category = animationCategory;
                else reasons.Add("ConflictingWeaponCategory");
            }
            else reasons.Add("UnsupportedWeaponAnimation");

            // Diagnostic hints only: never assign a subtype from a mod-defined EditorID.
            if (snapshot.Keywords.Any(keyword => !KeywordCatalog.Entries.ContainsKey(keyword.FormKey)
                && DiagnosticPolicy.CustomSubtype(keyword.EditorId)))
            {
                category = null;
                reasons.Add("CustomWeaponSubtypeRequiresReview");
            }
        }
        else
        {
            evidence.Add("ArmorType:" + (snapshot.ArmorType ?? "Unavailable"));
            evidence.Add("SlotMask:" + (snapshot.SlotMask?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unavailable"));
            if (snapshot.ArmorType is not ("HeavyArmor" or "LightArmor" or "Clothing")) reasons.Add("MissingOrUnsupportedArmorType");
            var types = Values(known, "ArmorType");
            if (types.Any(type => type != snapshot.ArmorType)) reasons.Add("ConflictingArmorType");
            if (snapshot.SlotMask is { } mask)
            {
                var primary = ArmorProfiles.Where(profile => (mask & profile.Primary) != 0).ToArray();
                if (primary.Length > 1) reasons.Add("CombinedPrimarySlots");
                else if (primary.Length == 1 && (mask & ~primary[0].Allowed) == 0)
                {
                    category = primary[0].Category;
                    if (categories.Length > 1 || categories.Any(value => value != category)) reasons.Add("ConflictingArmorCategory");
                    if (snapshot.ShieldFlag == true && category != "Shield") reasons.Add("ConflictingShieldEvidence");
                }
                // Vanilla/CC open helmets can occupy hair + circlet without the head bit.
                // Require canonical helmet identity and an armor type; a bare circlet is insufficient.
                else if (primary.Length == 0 && mask == ((1u << 1) | (1u << 12))
                    && snapshot.ArmorType is "HeavyArmor" or "LightArmor"
                    && categories.Length == 1 && categories[0] == "Helmet")
                    category = "Helmet";
                else reasons.Add("UnsupportedSlotCombination");
            }
            else reasons.Add("MissingSlotMask");
            if (category is not null && snapshot.ShieldFlag == true && category != "Shield") reasons.Add("ConflictingShieldEvidence");
        }
        var materials = Values(known, snapshot.RecordType == "WEAP" ? "WeaponMaterial" : "ArmorMaterial");
        var specificMaterials = materials.Where(candidate => !materials.Any(other =>
            KeywordCatalog.VariantParents.TryGetValue(other, out var parent) && parent == candidate)).ToArray();
        var material = specificMaterials.Length == 1 ? specificMaterials[0] : null;
        if (material is null) reasons.Add(materials.Length > 1 ? "ConflictingMaterialKeywords" : "UnknownMaterial");
        if (reasons.Any(reason => reason is "ConflictingWeaponCategory" or "ConflictingWeaponSkill" or "ConflictingArmorCategory"
            or "ConflictingArmorType" or "ConflictingShieldEvidence")) category = null;
        var scopeReasons = new List<string>();
        if (snapshot.Playable == false) scopeReasons.Add("NonPlayableOrUnknown");
        if (snapshot.RecordType == "ARMO" && snapshot.ArmorType == "Clothing") scopeReasons.Add("ClothingOutsideOrdinaryBaseline");
        if (snapshot.RecordType == "ARMO" && known.Any(k => k.Kind == "Scope" && k.Value == "Jewelry")) scopeReasons.Add("JewelryOutsideOrdinaryAnalysis");
        if (snapshot.RecordType == "WEAP" && snapshot.AnimationType == "Staff") scopeReasons.Add("StaffAnimationOutsideOrdinaryAnalysis");
        var expected = scopeReasons.Count > 0;
        foreach (var reason in reasons)
            if (!expected || reason is not ("UnknownMaterial" or "UnsupportedSlotCombination" or "UnsupportedWeaponAnimation"))
                diagnostics.Add(new(reason, Explain(reason)));
        if (expected) diagnostics.Add(new("ExpectedOutOfScope", "Outside ordinary weapon/armor analysis: " + string.Join(", ", scopeReasons.Order(StringComparer.Ordinal)) + ". Stored classification reasons remain in inventory."));
        if (snapshot.Template is not null) diagnostics.Add(new("TemplateNotEvaluated", "Stored fields only; effective inherited stats are not evaluated."));
        if (snapshot.Template is not null && string.Equals(snapshot.Template.FormKey, snapshot.FormKey, StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new("TemplateSelfReference", "Direct template points to this record; inheritance is not followed."));
        if (snapshot.HasScripts) diagnostics.Add(new("ScriptedEquipment", "Scripts may affect behavior; excluded from ordinary baselines."));
        if (snapshot.Deleted) diagnostics.Add(new("DeletedRecord", "Deleted winning record; excluded from statistical distributions and future comparisons."));
        if (snapshot.Playable != true) diagnostics.Add(new("NonPlayableOrUnknown", "Playability is false or unavailable; excluded from ordinary baselines."));
        if (known.Any(keyword => keyword.Kind == "SpecialEquipment"))
            diagnostics.Add(new("ArtifactEquipment", "Reviewed artifact keyword; excluded from ordinary baselines and ordinary analysis."));
        if (snapshot.Flags.Any(flag => new[] { "NotUsedInNormalCombat", "NonHostile", "EmbeddedWeapon", "CantDrop" }
            .Any(value => flag.Contains(value, StringComparison.Ordinal))))
            diagnostics.Add(new("UnusualWeaponFlags", "Weapon flags indicate special combat or utility behavior; ordinary eligibility is withheld."));
        if (snapshot.Flags.Any(flag => flag.Contains("BoundWeapon", StringComparison.Ordinal)))
            diagnostics.Add(new("BoundWeapon", "Bound weapon behavior is outside ordinary equipment baselines."));
        var status = category is null ? "Unclassified" : material is null || reasons.Count > 0 ? "Partial" : "Confident";
        var classification = new Classification(category, material, handedness, status,
            evidence.Distinct().Order(StringComparer.Ordinal).ToArray(), reasons.Distinct().Order(StringComparer.Ordinal).ToArray())
        {
            MaterialFamily = material is null ? null : KeywordCatalog.Family(material),
            MaterialVariant = material is not null && KeywordCatalog.Family(material) != material ? material : null,
            Scope = expected ? "ExpectedOutOfScope" : category is null ? "UnsupportedEquipment" : "OrdinaryEquipment",
            ScopeReasons = scopeReasons.Distinct().Order(StringComparer.Ordinal).ToArray()
        };
        var eligibility = GetEligibility(snapshot, classification, diagnostics);
        return new(snapshot, classification, eligibility, diagnostics.Select(DiagnosticPolicy.Decorate).Distinct().OrderBy(d => d.Code, StringComparer.Ordinal)
            .ThenBy(d => d.Message, StringComparer.Ordinal).ToArray());
    }

    public static IEnumerable<(string Label, ReferenceSnapshot Link)> References(EquipmentSnapshot snapshot)
    {
        if (snapshot.Enchantment is not null) yield return ("Enchantment", snapshot.Enchantment);
        if (snapshot.Template is not null) yield return ("Template", snapshot.Template);
        if (snapshot.EquipmentType is not null) yield return ("EquipmentType", snapshot.EquipmentType);
    }

    private static Eligibility GetEligibility(EquipmentSnapshot snapshot, Classification classification, List<Diagnostic> diagnostics)
    {
        var stats = new List<string>();
        var analysis = new List<string>();
        if (classification.Scope == "ExpectedOutOfScope")
        {
            stats.AddRange(classification.ScopeReasons);
            analysis.AddRange(classification.ScopeReasons);
        }
        if (snapshot.Deleted) { stats.Add("DeletedRecord"); analysis.Add("DeletedRecord"); }
        if (classification.Status != "Confident") { stats.Add("ClassificationNotConfident"); analysis.Add("ClassificationNotConfident"); }
        if (classification.Status != "Confident" && classification.Scope != "ExpectedOutOfScope")
        {
            stats.AddRange(classification.ReasonCodes);
            analysis.AddRange(classification.ReasonCodes);
        }
        // Templated records have stored fields that may not describe their in-game equipment.
        if (snapshot.Template is not null) { stats.Add("TemplateNotEvaluated"); analysis.Add("TemplateNotEvaluated"); }
        if (snapshot.Playable != true) analysis.Add("NonPlayableOrUnknown");
        if (snapshot.HasScripts) analysis.Add("ScriptedEquipment");
        if (diagnostics.Any(d => d.Code is "ArtifactEquipment" or "UnusualWeaponFlags")) analysis.Add("SpecialEquipment");
        if (diagnostics.Any(d => d.Code is "UnresolvedKeyword" or "UnresolvedReference")) analysis.Add("UnresolvedReferences");
        if (snapshot.Flags.Any(flag => flag.Contains("BoundWeapon", StringComparison.Ordinal))) analysis.Add("BoundWeapon");
        if (diagnostics.Any(d => d.Code is "NonFiniteMetric" or "NegativeMetric" or "MissingBasicStats" or "MissingWeaponData"))
            analysis.Add("InvalidOrMissingStructure");
        if (snapshot.RecordType == "ARMO" && snapshot.ArmorType == "Clothing") analysis.Add("ClothingOutsideOrdinaryBaseline");
        var baseline = new List<string>(analysis);
        if (snapshot.Enchantment is not null) baseline.Add("EnchantedEquipment");
        return new(stats.Count == 0, baseline.Count == 0, analysis.Count == 0,
            stats.Distinct().Order(StringComparer.Ordinal).ToArray(), baseline.Distinct().Order(StringComparer.Ordinal).ToArray(),
            analysis.Distinct().Order(StringComparer.Ordinal).ToArray());
    }

    private static string[] Values(IEnumerable<KnownKeyword> keywords, string kind) =>
        keywords.Where(keyword => keyword.Kind == kind).Select(keyword => keyword.Value).Distinct().Order(StringComparer.Ordinal).ToArray();

    private static string Explain(string reason) => reason switch
    {
        "UnknownMaterial" => "No reviewed material keyword; material is not guessed.",
        "ConflictingMaterialKeywords" => "Material keywords disagree; material is unavailable even when subtype can be identified.",
        "IncompleteKeywordEvidence" => "Unresolved keyword links may hide category or material evidence; confidence is withheld.",
        "CombinedPrimarySlots" => "Equipment occupies multiple primary armor slots; not treated as a single piece.",
        "UnsupportedSlotCombination" => "Slot mask is outside the reviewed primary/ancillary profiles.",
        "MissingTwoHandedSubtype" => "TwoHandAxe animation requires a battleaxe or warhammer keyword to identify subtype.",
        "CustomWeaponSubtypeRequiresReview" => "Custom combat keyword suggests a subtype outside the ordinary catalog; no subtype inferred.",
        "UnreviewedMaterialEvidence" => "An unreviewed material keyword may describe a distinct tier; reviewed stored material remains visible, but confidence is withheld.",
        _ => reason.StartsWith("Conflicting", StringComparison.Ordinal)
            ? "Structural and/or keyword evidence conflicts; ordinary classification is withheld."
            : "Required structure is unavailable or outside the reviewed categories."
    };
}
