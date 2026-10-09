using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class ClassificationTests
{
    internal static ReferenceSnapshot Keyword(string key, string? name = null, string status = "Resolved") => new(key, name, status);
    internal static EquipmentSnapshot Weapon(string animation = "OneHandSword", params ReferenceSnapshot[] extra) => new()
    {
        RecordType = "WEAP", FormKey = "000800:Test.esp", DefiningPlugin = "Test.esp", WinningPlugin = "Patch.esp",
        Playable = true, AnimationType = animation, Damage = 10, Speed = 1, Reach = 1, Weight = 10, Value = 100,
        Keywords = new[] { Keyword("01E719:Skyrim.esm", "WeapMaterialSteel") }.Concat(extra).ToArray()
    };
    internal static EquipmentSnapshot Armor(uint mask, params ReferenceSnapshot[] extra) => new()
    {
        RecordType = "ARMO", FormKey = "000801:Test.esp", DefiningPlugin = "Test.esp", WinningPlugin = "Patch.esp",
        Playable = true, ArmorType = "HeavyArmor", SlotMask = mask, ArmorRating = 20, Weight = 10, Value = 100,
        Keywords = new[] { Keyword("06BBE6:Skyrim.esm", "ArmorMaterialSteel") }.Concat(extra).ToArray()
    };

    [TestMethod]
    [DataRow("OneHandSword", "Sword")]
    [DataRow("OneHandDagger", "Dagger")]
    [DataRow("OneHandAxe", "WarAxe")]
    [DataRow("OneHandMace", "Mace")]
    [DataRow("TwoHandSword", "Greatsword")]
    [DataRow("Bow", "Bow")]
    [DataRow("Crossbow", "Crossbow")]
    public void AnimationIdentifiesOrdinarySubtype(string animation, string category)
    {
        var result = EquipmentClassifier.Classify(Weapon(animation));
        Assert.AreEqual(category, result.Classification.Category);
        Assert.AreEqual("Confident", result.Classification.Status);
        Assert.IsTrue(result.Eligibility.BaselineEligible);
    }

    [TestMethod]
    [DataRow("06D932:Skyrim.esm", "Battleaxe")]
    [DataRow("06D930:Skyrim.esm", "Warhammer")]
    public void TwoHandAxeRequiresExactSubtypeKeyword(string key, string category) =>
        Assert.AreEqual(category, EquipmentClassifier.Classify(Weapon("TwoHandAxe", Keyword(key))).Classification.Category);

    [TestMethod]
    public void MissingOrConflictingTypeIsNotGuessed()
    {
        Assert.IsNull(EquipmentClassifier.Classify(Weapon("TwoHandAxe")).Classification.Category);
        Assert.IsNull(EquipmentClassifier.Classify(Weapon("OneHandSword", Keyword("01E713:Skyrim.esm"))).Classification.Category);
        Assert.IsNull(EquipmentClassifier.Classify(Weapon("TwoHandAxe", Keyword("06D930:Skyrim.esm"), Keyword("06D932:Skyrim.esm"))).Classification.Category);
    }

    [TestMethod]
    public void KeywordIdentityDoesNotDependOnEditorId()
    {
        var snapshot = Weapon() with { Keywords = [Keyword("01E719:Skyrim.esm", "RenamedMaterial")] };
        Assert.AreEqual("Steel", EquipmentClassifier.Classify(snapshot).Classification.Material);
        var fake = snapshot with { Keywords = [Keyword("000900:Fake.esp", "WeapMaterialSteel")] };
        Assert.IsNull(EquipmentClassifier.Classify(fake).Classification.Material);
    }

    [TestMethod]
    public void UnknownMaterialRemainsPartialAndVisible()
    {
        var result = EquipmentClassifier.Classify(Weapon() with { Keywords = [] });
        Assert.AreEqual("Sword", result.Classification.Category);
        Assert.AreEqual("Partial", result.Classification.Status);
        Assert.IsFalse(result.Eligibility.InventoryStatisticsEligible);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "UnknownMaterial"));
    }

    [TestMethod]
    public void ConflictingMaterialsAreReported()
    {
        var result = EquipmentClassifier.Classify(Weapon("OneHandSword", Keyword("01E718:Skyrim.esm")));
        Assert.IsNull(result.Classification.Material);
        Assert.IsFalse(result.Eligibility.BaselineEligible);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "ConflictingMaterialKeywords"));
        Assert.AreEqual("Sword", result.Classification.Category);
        Assert.AreEqual("Partial", result.Classification.Status);
    }

    [TestMethod]
    public void ConflictingSkillWithholdsOrdinaryClassification()
    {
        var result = EquipmentClassifier.Classify(Weapon() with { Skill = "TwoHanded" });
        Assert.IsNull(result.Classification.Category);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "ConflictingWeaponSkill"));
    }

    [TestMethod]
    public void KnownArtifactsAndSpecialFlagsDoNotEnterOrdinaryBaselines()
    {
        foreach (var snapshot in new[] {
            Weapon("OneHandSword", Keyword("0A8668:Skyrim.esm")),
            Weapon() with { Flags = ["Data:BoundWeapon"] },
            Weapon() with { Flags = ["Data:CantDrop"] } })
        {
            var result = EquipmentClassifier.Classify(snapshot);
            Assert.AreEqual("Confident", result.Classification.Status);
            Assert.IsTrue(result.Eligibility.InventoryStatisticsEligible);
            Assert.IsFalse(result.Eligibility.BaselineEligible);
            Assert.IsFalse(result.Eligibility.AnalysisEligible);
        }
    }

    [TestMethod]
    public void CustomSubtypeHintsWithholdOrdinaryClassification()
    {
        var result = EquipmentClassifier.Classify(Weapon("OneHandSword", Keyword("000900:Custom.esp", "WeapTypeRapier")));
        Assert.IsNull(result.Classification.Category);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "CustomWeaponSubtypeRequiresReview"));
    }

    [TestMethod]
    public void CrossbowAcceptsCanonicalBowKeyword() => Assert.AreEqual("Crossbow",
        EquipmentClassifier.Classify(Weapon("Crossbow", Keyword("01E715:Skyrim.esm"))).Classification.Category);

    [TestMethod]
    [DataRow(1u, "Helmet")]
    [DataRow(4099u, "Helmet")]
    [DataRow(4u, "Cuirass")]
    [DataRow(24u, "Gauntlets")]
    [DataRow(384u, "Boots")]
    [DataRow(512u, "Shield")]
    public void StandardAndAncillarySlotsAreRecognized(uint mask, string category)
    {
        var result = EquipmentClassifier.Classify(Armor(mask));
        Assert.AreEqual(category, result.Classification.Category);
        Assert.AreEqual("Confident", result.Classification.Status);
    }

    [TestMethod]
    public void CombinedAndUnsupportedSlotsRemainSeparate()
    {
        var combined = EquipmentClassifier.Classify(Armor(4u | 8u));
        Assert.IsTrue(combined.Diagnostics.Any(d => d.Code == "CombinedPrimarySlots"));
        Assert.IsNull(combined.Classification.Category);
        Assert.IsTrue(EquipmentClassifier.Classify(Armor(4u | (1u << 10))).Diagnostics.Any(d => d.Code == "UnsupportedSlotCombination"));
    }

    [TestMethod]
    public void ArmorKeywordAndStructuralConflictsAreRejected()
    {
        Assert.IsNull(EquipmentClassifier.Classify(Armor(4, Keyword("06C0EE:Skyrim.esm"))).Classification.Category);
        Assert.IsNull(EquipmentClassifier.Classify(Armor(4, Keyword("06BBD3:Skyrim.esm"))).Classification.Category);
        Assert.IsNull(EquipmentClassifier.Classify(Armor(4) with { ShieldFlag = true }).Classification.Category);
    }

    [TestMethod]
    public void EnchantedClassificationAndEligibilityAreIndependent()
    {
        var result = EquipmentClassifier.Classify(Weapon() with { Enchantment = new("000123:Test.esp", "Enchant", "Resolved") });
        Assert.AreEqual("Confident", result.Classification.Status);
        Assert.IsTrue(result.Eligibility.InventoryStatisticsEligible);
        Assert.IsTrue(result.Eligibility.AnalysisEligible);
        Assert.IsFalse(result.Eligibility.BaselineEligible);
    }

    [TestMethod]
    public void TemplatesAndDeletedRecordsAreDiagnosticOnly()
    {
        foreach (var snapshot in new[] { Weapon() with { Template = new("000100:Test.esp", null, "Resolved") }, Weapon() with { Deleted = true } })
        {
            var result = EquipmentClassifier.Classify(snapshot);
            Assert.IsFalse(result.Eligibility.InventoryStatisticsEligible);
            Assert.IsFalse(result.Eligibility.AnalysisEligible);
        }
    }

    [TestMethod]
    public void UnresolvedLinksAndScriptsRemainVisible()
    {
        var unresolved = EquipmentClassifier.Classify(Weapon("OneHandSword", Keyword("000999:Missing.esp", status: "Unresolved")));
        Assert.AreEqual("Partial", unresolved.Classification.Status);
        Assert.IsFalse(unresolved.Eligibility.AnalysisEligible);
        var scripted = EquipmentClassifier.Classify(Weapon() with { HasScripts = true });
        Assert.IsTrue(scripted.Eligibility.InventoryStatisticsEligible);
        Assert.IsFalse(scripted.Eligibility.BaselineEligible);
    }
}
