namespace EquipmentBalancePatcher;

public sealed record KnownKeyword(string EditorId, string Kind, string Value);

public static class KeywordCatalog
{
    // Reviewed against Mutagen.Bethesda.FormKeys.SkyrimSE 3.4.0 (not a runtime dependency).
    // FormKeys establish identity; EditorIDs are explanatory labels only.
    public static IReadOnlyDictionary<string, KnownKeyword> Entries { get; } = Build();

    // Only these reviewed base/variant pairs can coexist. Same-family siblings still conflict.
    public static IReadOnlyDictionary<string, string> VariantParents { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["OrcishScaled"] = "Orcish", ["ElvenGilded"] = "Elven", ["IronBanded"] = "Iron",
        ["ImperialStudded"] = "ImperialLight"
    };

    public static string Family(string material) => material switch
    {
        "SteelPlate" => "Steel", "StalhrimHeavy" or "StalhrimLight" => "Stalhrim",
        "BonemoldHeavy" or "BonemoldLight" => "Bonemold", "ChitinHeavy" or "ChitinLight" => "Chitin",
        "NordicHeavy" or "NordicLight" => "Nordic", "ImperialHeavy" or "ImperialLight" or "ImperialStudded" => "Imperial",
        _ => VariantParents.TryGetValue(material, out var parent) ? parent : material
    };

    private static Dictionary<string, KnownKeyword> Build()
    {
        var result = new Dictionary<string, KnownKeyword>(StringComparer.OrdinalIgnoreCase);
        void Add(string plugin, string id, string editorId, string kind, string value) =>
            result.Add($"{id}:{plugin}", new(editorId, kind, value));
        void Skyrim(string id, string editorId, string kind, string value) =>
            Add("Skyrim.esm", id, editorId, kind, value);
        foreach (var (id, category) in new[] {
            ("01E711", "Sword"), ("01E712", "WarAxe"), ("01E713", "Dagger"),
            ("01E714", "Mace"), ("01E715", "Bow"), ("01E716", "Staff"),
            ("06D930", "Warhammer"), ("06D931", "Greatsword"), ("06D932", "Battleaxe") })
            Skyrim(id, "WeapType" + category, "WeaponCategory", category);
        foreach (var (id, category) in new[] {
            ("06C0EC", "Cuirass"), ("06C0ED", "Boots"), ("06C0EE", "Helmet"),
            ("06C0EF", "Gauntlets"), ("0965B2", "Shield") })
            Skyrim(id, "Armor" + category, "ArmorCategory", category);
        Skyrim("06BBD2", "ArmorHeavy", "ArmorType", "HeavyArmor");
        Skyrim("06BBD3", "ArmorLight", "ArmorType", "LightArmor");
        Skyrim("06BBE8", "ArmorClothing", "ArmorType", "Clothing");
        Skyrim("0A8668", "DaedricArtifact", "SpecialEquipment", "Artifact");
        Skyrim("0917E8", "VendorItemDaedricArtifact", "SpecialEquipment", "Artifact");
        Skyrim("06BBE9", "ArmorJewelry", "Scope", "Jewelry");
        // Reviewed exact identities from resolved keywords in the supplied v0.1a audit.
        foreach (var (id, editorId) in new[] {
            ("08F958", "VendorItemWeapon"), ("08F959", "VendorItemArmor"),
            ("08F95A", "VendorItemJewelry"), ("08F95B", "VendorItemClothing"),
            ("0937A4", "VendorItemStaff"), ("0C27BD", "MagicDisallowEnchanting"),
            ("0A8657", "ClothingBody"), ("10CD09", "ClothingRing"), ("10CD0A", "ClothingNecklace"),
            ("0A8664", "JewelryExpensive"), ("02C177", "PerkFistsOrcish") })
            Skyrim(id, editorId, "Unrelated", "Gameplay");
        Add("Update.esm", "002ED8", "Survival_ArmorCold", "Unrelated", "Survival");
        Add("Update.esm", "002ED9", "Survival_ArmorWarm", "Unrelated", "Survival");
        Add("Update.esm", "002EDB", "Survival_BodyAndHead", "Unrelated", "Survival");
        Add("Update.esm", "003278", "MatchingSetHeavyExclude", "Unrelated", "Perk");
        Add("Update.esm", "003277", "ccBGS_ArmorMaterialOrcishLight", "ArmorMaterial", "OrcishScaled");
        foreach (var (id, label, material) in new[] {
            ("21BD63", "Golden", "GoldenSaint"), ("21BD64", "Dark", "DarkSeducer"),
            ("21BD65", "Madness", "Madness"), ("21BD66", "Amber", "Amber") })
            Add("ccBGSSSE025-AdvDSGS.esm", id, "ccBGSSSE025_ArmorMaterial" + label, "ArmorMaterial", material);
        foreach (var (id, label, material) in new[] {
            ("21BD67", "Amber", "Amber"), ("21BD68", "Madness", "Madness"),
            ("21BD69", "Golden", "GoldenSaint"), ("21BD6A", "Dark", "DarkSeducer") })
            Add("ccBGSSSE025-AdvDSGS.esm", id, "ccBGSSSE025_WeapMaterial" + label, "WeaponMaterial", material);
        foreach (var (id, material) in new[] {
            ("01E717", "Wood"), ("01E718", "Iron"), ("01E719", "Steel"),
            ("01E71A", "Dwarven"), ("01E71B", "Elven"), ("01E71C", "Orcish"),
            ("01E71D", "Glass"), ("01E71E", "Ebony"), ("01E71F", "Daedric"),
            ("0C5C00", "Imperial"), ("0C5C01", "Draugr"), ("0C5C02", "DraugrHoned"),
            ("0C5C03", "Falmer"), ("0C5C04", "FalmerHoned"), ("10AA1A", "Silver") })
            Skyrim(id, "WeapMaterial" + material, "WeaponMaterial", material);
        foreach (var (id, material) in new[] {
            ("06BBD4", "Daedric"), ("06BBD5", "Dragonplate"), ("06BBD6", "Dragonscale"),
            ("06BBD7", "Dwarven"), ("06BBD8", "Ebony"), ("06BBD9", "Elven"),
            ("06BBDA", "ElvenGilded"), ("06BBDB", "Leather"), ("06BBDC", "Glass"),
            ("06BBDD", "Hide"), ("06BBDE", "Scaled"), ("06BBDF", "Studded"),
            ("06BBE0", "ImperialLight"), ("06BBE1", "ImperialStudded"),
            ("06BBE2", "ImperialHeavy"), ("06BBE3", "Iron"), ("06BBE4", "IronBanded"),
            ("06BBE5", "Orcish"), ("06BBE6", "Steel"), ("06BBE7", "SteelPlate"),
            ("0AC13A", "Stormcloak") })
            Skyrim(id, "ArmorMaterial" + material, "ArmorMaterial", material);
        Add("Dawnguard.esm", "019822", "DLC1WeapMaterialDragonbone", "WeaponMaterial", "Dragonbone");
        foreach (var (id, material) in new[] {
            ("01463E", "Vampire"), ("012CCE", "FalmerHardened"),
            ("012CCD", "Dawnguard"), ("0050C4", "Hunter") })
            Add("Dawnguard.esm", id, "DLC1ArmorMaterial" + material, "ArmorMaterial", material);
        foreach (var (id, material) in new[] { ("026230", "Nordic"), ("02622F", "Stalhrim") })
            Add("Dragonborn.esm", id, "DLC2WeaponMaterial" + material, "WeaponMaterial", material);
        foreach (var (id, material) in new[] {
            ("024100", "BonemoldLight"), ("024101", "BonemoldHeavy"),
            ("024102", "ChitinLight"), ("024103", "ChitinHeavy"),
            ("024104", "NordicLight"), ("024105", "NordicHeavy"),
            ("024106", "StalhrimHeavy"), ("024107", "StalhrimLight"), ("03A328", "MoragTong") })
            Add("Dragonborn.esm", id, "DLC2ArmorMaterial" + material, "ArmorMaterial", material);
        foreach (var (id, material) in new[] {
            ("0009B9", "Forsworn"), ("0009BA", "MS02Forsworn"), ("0009BB", "Penitus"),
            ("0009BC", "ThievesGuild"), ("0009BD", "Falmer"), ("0009BE", "BearStormcloak"),
            ("0009BF", "ThievesGuildLeader"), ("0009C0", "Blades") })
            Add("Update.esm", id, "ArmorMaterial" + material, "ArmorMaterial", material);
        return result;
    }
}
