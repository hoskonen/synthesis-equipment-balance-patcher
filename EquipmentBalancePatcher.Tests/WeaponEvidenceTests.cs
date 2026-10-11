using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class WeaponEvidenceTests
{
    private static EquipmentSnapshot Weapon(string? name = "Steel Sword") => ClassificationTests.Weapon() with
        { Name = name, CriticalDamage = 3, Stagger = 0.5 };
    private static AuditReport Build(params EquipmentSnapshot[] snapshots) => InventoryAudit.Build(snapshots, [], new());
    private static WeaponEvidenceResult Evidence(EquipmentSnapshot item) => Build(item).WeaponEvidenceAnalysis!.Records.Single();

    [TestMethod]
    public void ExactKeywordIdentityAndWholeNameWordAgreeWithoutChangingEligibility()
    {
        var item = Weapon("STEEL Sword");
        var result = Evidence(item);
        Assert.AreEqual("ConsistentEvidence", result.Status); Assert.AreEqual("AgreesWithKeyword", result.NameAgreement);
        CollectionAssert.AreEqual(new[] { "Steel" }, result.NameMaterials);
        var renamedKeyword = item with { Keywords = [ClassificationTests.Keyword("01E719:Skyrim.esm", "DifferentName")] };
        Assert.AreEqual("ConsistentEvidence", Evidence(renamedKeyword).Status);
        var fakeKeyword = item with { Keywords = [ClassificationTests.Keyword("000999:Fake.esp", "WeapMaterialSteel")] };
        Assert.AreEqual("NameOnlyEvidence", Evidence(fakeKeyword).NameAgreement);
        Assert.IsFalse(Build(fakeKeyword).Equipment.Single().Eligibility.AnalysisEligible);
    }

    [TestMethod]
    public void KeywordNameConflictIsSeparateFromNumericalComparison()
    {
        var reference = Weapon() with { FormKey = "013989:Skyrim.esm", WinningPlugin = "Tweaks.esp" };
        var item = Weapon("Iron Sword");
        var report = Build(reference, item);
        var result = report.WeaponEvidenceAnalysis!.Records.Single(r => r.FormKey == item.FormKey);
        Assert.AreEqual("ConflictingEvidence", result.Status); Assert.AreEqual("ConflictsWithKeyword", result.NameAgreement);
        Assert.AreEqual("WithinTolerance", result.ReferenceStatus);
        Assert.AreEqual(reference.FormKey, result.ReferenceFormKey);
        Assert.AreEqual("Steel", report.Equipment.Single(e => e.Snapshot.FormKey == item.FormKey).Classification.Material);
        Assert.IsTrue(report.Equipment.Single(e => e.Snapshot.FormKey == item.FormKey).Eligibility.AnalysisEligible);
    }

    [TestMethod]
    public void NameOnlyAndNoEvidenceRemainIncompleteWithoutReferenceInference()
    {
        var item = Weapon("Ebony Sword") with { Keywords = [] };
        var result = Evidence(item);
        Assert.IsTrue(result.OrdinaryInspected); Assert.AreEqual("IncompleteEvidence", result.Status);
        Assert.AreEqual("NameOnlyEvidence", result.NameAgreement);
        Assert.AreEqual("NotEligibleForReference", result.ReferenceStatus); Assert.IsNull(result.ReferenceFormKey);
        Assert.AreEqual(0, result.ReferenceMetrics.Length);
        var none = Evidence(item with { Name = null });
        Assert.AreEqual("NoMaterialEvidence", none.NameAgreement);
        Assert.IsTrue(none.Reasons.Contains("DisplayNameUnavailable"));
    }

    [TestMethod]
    [DataRow("Ironwood Sword")]
    [DataRow("Steelheart")]
    [DataRow("Glasswing")]
    [DataRow("Ebony2")]
    [DataRow("Steel_Sword")]
    [DataRow("éSteel Sword")]
    public void ArbitrarySubstringsAreNotMaterialWords(string name)
    {
        var result = Evidence(Weapon(name));
        Assert.AreEqual(0, result.NameMaterials.Length); Assert.AreEqual("KeywordOnlyEvidence", result.NameAgreement);
        Assert.AreEqual("IncompleteEvidence", result.Status);
    }

    [TestMethod]
    public void AmbiguousAndPossessiveMaterialNamesRemainWeak()
    {
        var result = Evidence(Weapon("Iron-and-Steel Sword"));
        Assert.AreEqual("AmbiguousNameEvidence", result.NameAgreement); Assert.AreEqual("IncompleteEvidence", result.Status);
        Assert.IsTrue(result.Reasons.Contains("AmbiguousMaterialWords"));
        var possessive = Evidence(Weapon("Steel's Legacy"));
        Assert.AreEqual("AgreesWithKeyword", possessive.NameAgreement);
        Assert.IsTrue(possessive.Reasons.Contains("NameIsWeakEvidence_NotMaterialProof"));
    }

    [TestMethod]
    public void MultipleMaterialKeywordsAreVisibleButNeverPromoted()
    {
        var item = Weapon() with { Keywords = Weapon().Keywords.Append(ClassificationTests.Keyword("01E718:Skyrim.esm")).ToArray() };
        var result = Evidence(item);
        Assert.IsTrue(result.OrdinaryInspected); Assert.AreEqual("ConflictingEvidence", result.Status);
        Assert.AreEqual("MultipleMaterials", result.KeywordStatus); Assert.AreEqual(2, result.MaterialKeywords.Length);
        Assert.IsNull(result.ReferenceFormKey); Assert.IsFalse(Build(item).Equipment.Single().Eligibility.AnalysisEligible);
    }

    [TestMethod]
    public void CustomStaffScriptSpecialTemplateAndStructuralConflictsRemainOutOfScope()
    {
        var items = new[] { Weapon("Steel Spear"), Weapon() with { AnimationType = "Staff" },
            Weapon() with { Keywords = Weapon().Keywords.Append(ClassificationTests.Keyword("000900:Custom.esp", "WeapTypeRapier")).ToArray() },
            Weapon() with { Flags = ["CantDrop"] }, Weapon() with { HasScripts = true },
            Weapon() with { Template = new("013989:Skyrim.esm", null, "Resolved") }, Weapon() with { Playable = false },
            Weapon() with { Keywords = Weapon().Keywords.Append(ClassificationTests.Keyword("01E713:Skyrim.esm")).ToArray() },
            Weapon() with { AnimationType = "Crossbow" }, Weapon("Blade of Woe"), Weapon("Nightingale Blade"), Weapon("Dwarven Fishing Rod"), Weapon("Zephyr") };
        foreach (var item in items)
        {
            var result = Evidence(item);
            Assert.IsFalse(result.OrdinaryInspected, item.Name); Assert.AreEqual("UnsupportedSpecialWeapon", result.Status);
            Assert.AreEqual(0, result.ReferenceMetrics.Length);
        }
        // The local name hint cannot alter the existing classifier or reference audit.
        Assert.IsTrue(Build(Weapon("Blade of Woe")).Equipment.Single().Eligibility.AnalysisEligible);
    }

    [TestMethod]
    public void AllRequestedVanillaSubtypesCanBeInspectedButArmorIsOmitted()
    {
        foreach (var animation in new[] { "OneHandSword", "OneHandDagger", "OneHandAxe", "OneHandMace", "TwoHandSword", "Bow" })
            Assert.IsTrue(Evidence(Weapon() with { AnimationType = animation }).OrdinaryInspected);
        foreach (var keyword in new[] { "06D930:Skyrim.esm", "06D932:Skyrim.esm" })
            Assert.IsTrue(Evidence(Weapon() with { AnimationType = "TwoHandAxe", Keywords = Weapon().Keywords.Append(ClassificationTests.Keyword(keyword)).ToArray() }).OrdinaryInspected);
        Assert.AreEqual(0, Build(ClassificationTests.Armor(4)).WeaponEvidenceAnalysis!.Records.Length);
    }

    [TestMethod]
    public void SummaryCountsSourcesExclusionsAndCompactJsonCorrectly()
    {
        var records = new[] { Weapon(), Weapon("Iron Sword"), Weapon("Legacy"), Weapon("Ebony Sword") with { Keywords = [] },
            Weapon(null) with { Keywords = [] }, Weapon("Steel Rapier") };
        var report = Build(records.Select((s, i) => s with { FormKey = $"{i + 2048:X6}:Test.esp" }).ToArray());
        var summary = report.WeaponEvidenceAnalysis!.Summary;
        Assert.AreEqual(5, summary.OrdinaryWeaponsInspected); Assert.AreEqual(1, summary.MatchingKeywordName);
        Assert.AreEqual(1, summary.ConflictingEvidence); Assert.AreEqual(1, summary.KeywordOnly);
        Assert.AreEqual(1, summary.NameOnly); Assert.AreEqual(1, summary.NoMaterialEvidence); Assert.AreEqual(1, summary.ExcludedWeapons);
        Assert.AreEqual("Deferred_NotExtracted", report.WeaponEvidenceAnalysis.CraftingEvidenceStatus);
        using var json = JsonDocument.Parse(ReportWriter.Json(report));
        Assert.IsFalse(json.RootElement.GetProperty("weaponEvidenceAnalysis").TryGetProperty("records", out _));
    }

    [TestMethod]
    public void EvidenceIsDeterministicAndEveryPreviousComponentRemainsUnchanged()
    {
        var records = new[] { Weapon("Steel Sword"), Weapon("Iron Sword") with { FormKey = "000801:Test.esp" } };
        var first = Build(records);
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var second = Build(records.Reverse().ToArray());
            Assert.AreEqual(ReportWriter.WeaponEvidenceCsv(first), ReportWriter.WeaponEvidenceCsv(second));
            Assert.AreEqual(ReportWriter.Json(first), ReportWriter.Json(second));
        }
        finally { CultureInfo.CurrentCulture = prior; }
        var before = first.Equipment.Select(e => JsonSerializer.Serialize(e)).ToArray();
        var balance = JsonSerializer.Serialize(first.BalanceAnalysis); var references = JsonSerializer.Serialize(first.ReferenceAnalysis);
        var inventory = ReportWriter.InventoryCsv(first); var findings = ReportWriter.BalanceFindingsCsv(first);
        var comparisons = ReportWriter.ReferenceComparisonsCsv(first); var templates = ReportWriter.TemplatesCsv(first);
        WeaponEvidenceAudit.Build(first.Equipment, first.ReferenceAnalysis!);
        CollectionAssert.AreEqual(before, first.Equipment.Select(e => JsonSerializer.Serialize(e)).ToArray());
        Assert.AreEqual(balance, JsonSerializer.Serialize(first.BalanceAnalysis)); Assert.AreEqual(references, JsonSerializer.Serialize(first.ReferenceAnalysis));
        Assert.AreEqual(inventory, ReportWriter.InventoryCsv(first)); Assert.AreEqual(findings, ReportWriter.BalanceFindingsCsv(first));
        Assert.AreEqual(comparisons, ReportWriter.ReferenceComparisonsCsv(first)); Assert.AreEqual(templates, ReportWriter.TemplatesCsv(first));
    }
}
