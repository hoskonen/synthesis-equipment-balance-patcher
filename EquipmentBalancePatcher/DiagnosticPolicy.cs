namespace EquipmentBalancePatcher;

public static class DiagnosticPolicy
{
    public static string KeywordCategory(ReferenceSnapshot keyword)
    {
        if (keyword.Status != "Resolved") return "Unresolved";
        if (KeywordCatalog.Entries.TryGetValue(keyword.FormKey, out var entry))
            return entry.Kind == "Unrelated" ? "RecognizedUnrelated" : "RecognizedClassification";
        return PotentialKeyword(keyword.EditorId) ? "PotentialClassification" : "OtherUnknown";
    }

    // Relevance hints only. Never assign materials or categories from these names.
    public static bool PotentialKeyword(string? id) => id is not null && new[] {
        "Material", "WeapType", "WeaponType", "ArmorType", "ArmorHelmet", "ArmorCuirass",
        "ArmorBoots", "ArmorGauntlets", "ArmorShield", "Rapier", "Spear", "Pike", "Halberd",
        "Quarterstaff", "Trident", "Glaive", "Scythe", "Pole1H", "Pole2H"
    }.Any(token => id.Contains(token, StringComparison.OrdinalIgnoreCase));

    public static bool CustomSubtype(string? id) => id is not null && new[] {
        "Rapier", "Spear", "Pike", "Halberd", "Quarterstaff", "Trident", "Glaive", "Scythe", "Pole1H", "Pole2H"
    }.Any(token => id.Contains(token, StringComparison.OrdinalIgnoreCase));

    public static string ReasonCategory(string code) => code switch
    {
        "TemplateNotEvaluated" or "TemplateSelfReference" => "Template",
        "PotentialClassificationKeyword" => "KeywordReview",
        "NonPlayableOrUnknown" or "NonPlayableEquipment" or "ClothingOutsideOrdinaryBaseline"
            or "JewelryOutsideOrdinaryAnalysis" or "StaffAnimationOutsideOrdinaryAnalysis" or "ExpectedOutOfScope" => "ExpectedExclusion",
        "EnchantedEquipment" or "ScriptedEquipment" or "SpecialEquipment" or "BoundWeapon" => "SpecialEquipment",
        "ClassificationNotConfident" => "Classification",
        "DeletedRecord" => "ExpectedExclusion",
        "UnsupportedSlotCombination" or "CombinedPrimarySlots" or "UnsupportedWeaponAnimation"
            or "CustomWeaponSubtypeRequiresReview" => "UnsupportedEquipment",
        _ => "MissingOrConflictingData"
    };

    public static Diagnostic Decorate(Diagnostic diagnostic) => diagnostic with
    {
        Severity = diagnostic.Code is "TemplateNotEvaluated" or "ScriptedEquipment" or "DeletedRecord"
            or "NonPlayableOrUnknown" or "ArtifactEquipment" or "UnusualWeaponFlags" or "BoundWeapon"
            or "ExpectedOutOfScope" ? "Info" : "Warning",
        Relevance = ReasonCategory(diagnostic.Code)
    };
}
