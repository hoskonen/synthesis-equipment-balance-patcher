using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class RefinementTests
{
    [TestMethod]
    [DataRow("21BD63", "ARMO", "GoldenSaint")]
    [DataRow("21BD64", "ARMO", "DarkSeducer")]
    [DataRow("21BD65", "ARMO", "Madness")]
    [DataRow("21BD66", "ARMO", "Amber")]
    [DataRow("21BD67", "WEAP", "Amber")]
    [DataRow("21BD68", "WEAP", "Madness")]
    [DataRow("21BD69", "WEAP", "GoldenSaint")]
    [DataRow("21BD6A", "WEAP", "DarkSeducer")]
    public void ReviewedCreationMaterialsUseExactIdentity(string id, string type, string material)
    {
        var snapshot = type == "ARMO" ? ClassificationTests.Armor(4) : ClassificationTests.Weapon();
        snapshot = snapshot with { Keywords = [ClassificationTests.Keyword(id + ":ccbgssse025-advdsgs.esm", "Renamed")] };
        Assert.AreEqual(material, EquipmentClassifier.Classify(snapshot).Classification.Material);
        snapshot = snapshot with { Keywords = [ClassificationTests.Keyword(id + ":Fake.esp", "ccBGSSSE025_ArmorMaterialGolden")] };
        Assert.IsNull(EquipmentClassifier.Classify(snapshot).Classification.Material);
    }

    [TestMethod]
    public void ReviewedBaseVariantPairRetainsSpecificGrouping()
    {
        var armor = ClassificationTests.Armor(4) with { ArmorType = "LightArmor", Keywords = [
            ClassificationTests.Keyword("003277:Update.esm"), ClassificationTests.Keyword("06BBE5:Skyrim.esm")] };
        var result = EquipmentClassifier.Classify(armor);
        Assert.AreEqual("Confident", result.Classification.Status);
        Assert.AreEqual("Orcish", result.Classification.MaterialFamily);
        Assert.AreEqual("OrcishScaled", result.Classification.MaterialVariant);
        Assert.AreEqual("OrcishScaled", result.Classification.Material);
        var report = InventoryAudit.Build([armor, armor with { FormKey = "000999:Test.esp", Keywords = [ClassificationTests.Keyword("06BBE5:Skyrim.esm")] }], [], new());
        Assert.AreEqual(2, report.Groups.Length);
        var conflict = armor with { Keywords = armor.Keywords.Concat([ClassificationTests.Keyword("06BBDD:Skyrim.esm")]).ToArray() };
        Assert.IsNull(EquipmentClassifier.Classify(conflict).Classification.Material);
    }

    [TestMethod]
    public void SameFamilySiblingsStillConflict()
    {
        var snapshot = ClassificationTests.Armor(4) with { Keywords = [
            ClassificationTests.Keyword("024106:Dragonborn.esm"), ClassificationTests.Keyword("024107:Dragonborn.esm")] };
        Assert.IsNull(EquipmentClassifier.Classify(snapshot).Classification.Material);
    }

    [TestMethod]
    public void UnreviewedMaterialVariantWithholdsConfidenceWithoutGuessing()
    {
        var result = EquipmentClassifier.Classify(ClassificationTests.Weapon("OneHandSword",
            ClassificationTests.Keyword("000824:PrvtI_ArgonianWeapons.esp", "PI_WeapMaterialArgonian")));
        Assert.AreEqual("Steel", result.Classification.Material);
        Assert.AreEqual("Partial", result.Classification.Status);
        Assert.IsFalse(result.Eligibility.BaselineEligible);
        Assert.IsTrue(result.Classification.ReasonCodes.Contains("UnreviewedMaterialEvidence"));
    }

    [TestMethod]
    public void UnrelatedAndUnknownKeywordsRemainRawWithoutWarnings()
    {
        var snapshot = ClassificationTests.Weapon("OneHandSword",
            ClassificationTests.Keyword("08F958:Skyrim.esm", "RenamedVendor"),
            ClassificationTests.Keyword("002ED9:Update.esm", "Survival_ArmorWarm"),
            ClassificationTests.Keyword("000900:Custom.esp", "DistributionOnly"));
        var result = EquipmentClassifier.Classify(snapshot);
        Assert.AreEqual("Confident", result.Classification.Status);
        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.AreEqual(4, result.Snapshot.Keywords.Length);
        Assert.AreEqual("RecognizedUnrelated", DiagnosticPolicy.KeywordCategory(snapshot.Keywords[1]));
        Assert.AreEqual("OtherUnknown", DiagnosticPolicy.KeywordCategory(snapshot.Keywords[3]));
    }

    [TestMethod]
    public void KeywordAggregationCountsRecordsNotDuplicateOccurrences()
    {
        var hint = ClassificationTests.Keyword("000900:Custom.esp", "NewArmorMaterial");
        var first = ClassificationTests.Weapon("OneHandSword", hint, hint);
        var second = ClassificationTests.Armor(4, hint);
        var report = InventoryAudit.Build([first, second], [], new());
        var keyword = report.KeywordSummary.Single(k => k.FormKey == hint.FormKey);
        Assert.AreEqual(2, keyword.AffectedRecords);
        Assert.AreEqual("PotentialClassification", keyword.DiagnosticCategory);
        CollectionAssert.AreEqual(new[] { "ARMO", "WEAP" }, keyword.RecordTypes);
        Assert.AreEqual(2, keyword.Examples.Length);
        Assert.AreEqual(ReportWriter.Json(report), ReportWriter.Json(InventoryAudit.Build([second, first], [], new())));
        Assert.AreEqual(ReportWriter.KeywordsCsv(report), ReportWriter.KeywordsCsv(InventoryAudit.Build([second, first], [], new())));
        using var json = JsonDocument.Parse(ReportWriter.Json(report));
        Assert.AreEqual("4", json.RootElement.GetProperty("schemaVersion").GetString());
        Assert.IsTrue(json.RootElement.TryGetProperty("eligibilitySummary", out _));
    }

    [TestMethod]
    public void ExpectedExclusionsSuppressMissingMaterialAndSlotNoiseButKeepReasons()
    {
        foreach (var snapshot in new[] {
            ClassificationTests.Armor(32) with { ArmorType = "Clothing", Keywords = [ClassificationTests.Keyword("06BBE9:Skyrim.esm")] },
            ClassificationTests.Armor(0) with { Playable = false, Keywords = [] },
            ClassificationTests.Weapon("Staff") with { Keywords = [] } })
        {
            var result = EquipmentClassifier.Classify(snapshot);
            Assert.AreEqual("ExpectedOutOfScope", result.Classification.Scope);
            Assert.IsFalse(result.Eligibility.InventoryStatisticsEligible);
            Assert.IsTrue(result.Classification.ReasonCodes.Contains("UnknownMaterial"));
            Assert.IsFalse(result.Diagnostics.Any(d => d.Code is "UnknownMaterial" or "UnsupportedSlotCombination" or "UnsupportedWeaponAnimation"));
            Assert.AreEqual("Info", result.Diagnostics.Single(d => d.Code == "ExpectedOutOfScope").Severity);
        }
    }

    [TestMethod]
    [DataRow("OCF_WeapTypePole2H_Thrust")]
    [DataRow("ShWeapTypeQuarterStaff")]
    [DataRow("WeapTypeTrident")]
    public void CustomWeaponsNeverBecomeOrdinaryAnimationCategories(string id)
    {
        var result = EquipmentClassifier.Classify(ClassificationTests.Weapon("TwoHandSword", ClassificationTests.Keyword("000900:Custom.esp", id)));
        Assert.IsNull(result.Classification.Category);
        Assert.IsFalse(result.Eligibility.AnalysisEligible);
    }

    [TestMethod]
    public void HeadlessHelmetRequiresCanonicalKeywordAndReviewedSlots()
    {
        Assert.IsNull(EquipmentClassifier.Classify(ClassificationTests.Armor(4098)).Classification.Category);
        Assert.AreEqual("Helmet", EquipmentClassifier.Classify(ClassificationTests.Armor(4098, ClassificationTests.Keyword("06C0EE:Skyrim.esm"))).Classification.Category);
        Assert.AreEqual("Helmet", EquipmentClassifier.Classify(ClassificationTests.Armor(12291)).Classification.Category);
        Assert.IsNull(EquipmentClassifier.Classify(ClassificationTests.Armor(4098, ClassificationTests.Keyword("06C0EE:Skyrim.esm")) with { ShieldFlag = true }).Classification.Category);
    }

    [TestMethod]
    public void EligibilitySummaryExplicitlyCountsOverlappingReasons()
    {
        var snapshot = ClassificationTests.Weapon() with { HasScripts = true, Template = new("000900:Test.esp", null, "Resolved"), Enchantment = new("000901:Test.esp", null, "Resolved") };
        var report = InventoryAudit.Build([snapshot], [], new());
        var reasons = report.EligibilitySummary.Where(s => s.Stage == "Baseline").ToArray();
        Assert.AreEqual(3, reasons.Length);
        Assert.IsTrue(reasons.All(s => s.AffectedRecords == 1));
        Assert.AreEqual("Template", reasons.Single(s => s.Reason == "TemplateNotEvaluated").ReasonCategory);
        Assert.AreEqual(1, report.Summary.BaselineExcluded);
    }
}
