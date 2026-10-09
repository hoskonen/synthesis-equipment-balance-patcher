using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class TemplateTests
{
    private static EquipmentSnapshot Weapon(string key, string? parent = null) => ClassificationTests.Weapon() with
    {
        FormKey = key, Template = parent is null ? null : new(parent, "Parent", "Resolved")
    };
    private static EffectiveField Field(EffectiveEquipment equipment, string name) => equipment.Fields.Single(f => f.Name == name);

    [TestMethod]
    public void NoTemplateIsStoredAndNeverInventsAParent()
    {
        var snapshot = Weapon("000800:Test.esp");
        var resolver = new TemplateResolver([snapshot]);
        var chain = resolver.Resolve(snapshot.FormKey);
        Assert.AreEqual("NoTemplate", chain.Status); Assert.AreEqual(0, chain.Depth);
        Assert.IsNull(chain.RootParent); Assert.AreEqual(1, chain.Visited.Length);
        var effective = EffectiveEquipmentInvestigator.Investigate(EquipmentClassifier.Classify(snapshot), resolver);
        Assert.AreEqual("Stored", Field(effective, "Damage").ResolutionStatus);
        Assert.AreEqual("StoredOnly", Field(effective, "Damage").ValidationStatus);
        Assert.AreEqual(0, effective.HypotheticalClassifications.Length);
    }

    [TestMethod]
    public void OneAndMultipleParentsAreTraversedWithoutInterpretingFields()
    {
        var root = Weapon("000800:Test.esp") with { Damage = 7, WinningPlugin = "Winner.esp" };
        var middle = Weapon("000801:Test.esp", root.FormKey) with { Damage = 8 };
        var child = Weapon("000802:Test.esp", middle.FormKey) with { Damage = 9 };
        var resolver = new TemplateResolver([child, root, middle]);
        Assert.AreEqual(1, resolver.Resolve(middle.FormKey).Depth);
        var chain = resolver.Resolve(child.FormKey);
        Assert.AreEqual("Resolved", chain.Status); Assert.AreEqual(2, chain.Depth);
        CollectionAssert.AreEqual(new[] { child.FormKey, middle.FormKey, root.FormKey }, chain.Visited.Select(h => h.FormKey).ToArray());
        Assert.AreEqual(root.FormKey, chain.RootParent);
        Assert.AreEqual("Winner.esp", chain.Visited.Last().WinningPlugin);
        var effective = EffectiveEquipmentInvestigator.Investigate(EquipmentClassifier.Classify(child), resolver);
        var damage = Field(effective, "Damage");
        Assert.AreEqual(9, damage.RawChildValue.GetDouble()); Assert.AreEqual(7, damage.ProposedEffectiveValue.GetDouble());
        Assert.AreEqual(root.FormKey, damage.SourceFormKey); Assert.AreEqual("Winner.esp", damage.SourceWinningPlugin);
        Assert.AreEqual(2, damage.TemplateDepth); Assert.AreEqual(true, damage.ValuesDiffer);
        Assert.AreEqual("ProposedInherited", damage.ResolutionStatus); Assert.AreEqual("Unvalidated", damage.ValidationStatus);
        Assert.AreEqual(9d, child.Damage); Assert.AreEqual(8d, middle.Damage);
    }

    [TestMethod]
    public void SharedParentsHaveStableCompletedResultsAndRootSummaries()
    {
        var root = Weapon("000800:Test.esp");
        var first = Weapon("000801:Test.esp", root.FormKey);
        var second = Weapon("000802:Test.esp", root.FormKey) with { Enchantment = new("000900:Test.esp", null, "Resolved") };
        var resolver = new TemplateResolver([root, first, second]);
        Assert.AreSame(resolver.Resolve(first.FormKey), resolver.Resolve(first.FormKey));
        Assert.AreEqual(root.FormKey, resolver.Resolve(second.FormKey).RootParent);
        var report = InventoryAudit.Build([root, first, second], [], new());
        var family = report.TemplateRoots.Single();
        Assert.AreEqual(2, family.TemplatedVariants); Assert.AreEqual(1, family.EnchantedVariants);
        Assert.AreEqual(1, report.Summary.BaselineEligible); // Only the independent raw root.
    }

    [TestMethod]
    public void SelfReferenceAndLongerCyclesProduceNoEffectiveRoot()
    {
        var first = Weapon("000800:Test.esp", "000801:Test.esp");
        var second = Weapon("000801:Test.esp", first.FormKey);
        var cyclic = new TemplateResolver([first, second]).Resolve(first.FormKey);
        Assert.AreEqual("Cycle", cyclic.Status); Assert.IsNull(cyclic.RootParent);
        Assert.AreEqual(first.FormKey, cyclic.FailureTarget); Assert.AreEqual(2, cyclic.Visited.Length);
        var self = first with { Template = new(first.FormKey, null, "Resolved") };
        Assert.AreEqual("SelfReference", new TemplateResolver([self]).Resolve(self.FormKey).Status);
        var resolver = new TemplateResolver([first, second]);
        var effective = EffectiveEquipmentInvestigator.Investigate(EquipmentClassifier.Classify(first), resolver);
        Assert.AreEqual("Unresolved", Field(effective, "Damage").ResolutionStatus);
        Assert.IsNull(Field(effective, "Damage").SourceFormKey);
        Assert.AreEqual(JsonValueKind.Null, Field(effective, "Damage").ProposedEffectiveValue.ValueKind);
        Assert.IsNull(Field(effective, "Damage").ValuesDiffer); Assert.AreEqual(0, effective.HypotheticalClassifications.Length);
    }

    [TestMethod]
    [DataRow("MissingParent")]
    [DataRow("UnresolvedParent")]
    [DataRow("DeletedParent")]
    [DataRow("WrongTypeParent")]
    public void InvalidTargetsStayExplicit(string expected)
    {
        var parent = Weapon("000800:Test.esp");
        var child = Weapon("000801:Test.esp", parent.FormKey);
        EquipmentSnapshot[] records = expected switch
        {
            "DeletedParent" => [child, parent with { Deleted = true }],
            "WrongTypeParent" => [child, ClassificationTests.Armor(4) with { FormKey = parent.FormKey }],
            _ => [child]
        };
        if (expected == "UnresolvedParent") child = child with { Template = new(parent.FormKey, null, "Unresolved") };
        if (expected == "UnresolvedParent") records = [child];
        var resolver = new TemplateResolver(records);
        var chain = resolver.Resolve(child.FormKey);
        Assert.AreEqual(expected, chain.Status); Assert.IsNull(chain.RootParent);
        Assert.AreEqual(parent.FormKey, chain.FailureTarget); Assert.AreEqual(1, chain.Diagnostics.Length);
    }

    [TestMethod]
    public void AbsentNonEquipmentTargetCanBeIdentifiedThroughSourceProbe()
    {
        var child = Weapon("000800:Test.esp", "000900:Test.esp");
        var chain = new TemplateResolver([child], missingTargetStatus: _ => "WrongTypeParent").Resolve(child.FormKey);
        Assert.AreEqual("WrongTypeParent", chain.Status);
    }

    [TestMethod]
    public void DepthBudgetIsChildRelativeAndCancellationAppliesEvenToCachedResults()
    {
        var root = Weapon("000800:Test.esp");
        var middle = Weapon("000801:Test.esp", root.FormKey);
        var child = Weapon("000802:Test.esp", middle.FormKey);
        var resolver = new TemplateResolver([root, middle, child], 1);
        Assert.AreEqual("Resolved", resolver.Resolve(middle.FormKey).Status);
        Assert.AreEqual("MaximumDepthExceeded", resolver.Resolve(child.FormKey).Status);
        Assert.AreEqual(1, resolver.Resolve(child.FormKey).Depth);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TemplateResolver([root], 0));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => resolver.Resolve(middle.FormKey, token.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() => InventoryAudit.Build([child], [], new(), token.Token));
        using var during = new CancellationTokenSource();
        var fresh = new TemplateResolver([child], missingTargetStatus: _ => { during.Cancel(); return "MissingParent"; });
        Assert.ThrowsExactly<OperationCanceledException>(() => fresh.Resolve(child.FormKey, during.Token));
        Assert.AreEqual("MissingParent", fresh.Resolve(child.FormKey).Status); // Canceled result wasn't cached.
    }

    [TestMethod]
    public void ZeroAndNullLocalValuesNeverFallBackToParentEnchantmentOrScripts()
    {
        var parent = Weapon("000800:Test.esp") with { Value = 99, Enchantment = new("000900:Test.esp", null, "Resolved"), HasScripts = true };
        var child = Weapon("000801:Test.esp", parent.FormKey) with { Value = 0, Enchantment = null, EnchantmentAmount = 0, Name = null, HasScripts = false };
        var report = InventoryAudit.Build([parent, child], [], new());
        var effective = report.TemplateAnalysis.Single();
        Assert.AreEqual(0, Field(effective, "Value").ProposedEffectiveValue.GetDouble());
        Assert.AreEqual(JsonValueKind.Null, Field(effective, "Enchantment").ProposedEffectiveValue.ValueKind);
        Assert.AreEqual("Stored", Field(effective, "Enchantment").ResolutionStatus);
        Assert.AreEqual(child.FormKey, Field(effective, "Name").SourceFormKey);
        Assert.IsFalse(Field(effective, "HasScripts").ProposedEffectiveValue.GetBoolean());
        Assert.IsTrue(effective.HypotheticalClassifications.All(h => !h.Eligibility.AnalysisEligible && h.Eligibility.AnalysisReasons.Contains("ScriptedEquipment")));
        Assert.IsFalse(report.Equipment.Single(e => e.Snapshot.FormKey == child.FormKey).Eligibility.BaselineEligible);
    }

    [TestMethod]
    public void KeywordAlternativesPreserveUncertaintyAndCustomBoundaries()
    {
        var parent = Weapon("000800:Test.esp");
        var child = Weapon("000801:Test.esp", parent.FormKey) with { Keywords = parent.Keywords.Concat([
            ClassificationTests.Keyword("000900:Mod.esp", "WeapTypeSpear")]).ToArray() };
        var report = InventoryAudit.Build([parent, child], [], new());
        var effective = report.TemplateAnalysis.Single();
        Assert.AreEqual("Sword", effective.HypotheticalClassifications.Single(h => h.Scenario == "RootReplacement").Classification.Category);
        Assert.IsNull(effective.HypotheticalClassifications.Single(h => h.Scenario == "ChildKeywords").Classification.Category);
        Assert.IsNull(effective.HypotheticalClassifications.Single(h => h.Scenario == "ChainKeywordUnion").Classification.Category);
        Assert.IsTrue(effective.Diagnostics.Any(d => d.Code == "TemplateKeywordSemanticsUnvalidated"));
        Assert.IsTrue(effective.Fields.All(f => f.ValidationStatus != "ValidatedInherited"));
        Assert.IsNull(report.Equipment.Single(e => e.Snapshot.FormKey == child.FormKey).Classification.Category);
    }

    [TestMethod]
    public void ArmorAndStaffPrototypeDoNotPromoteProductionEligibility()
    {
        var parent = ClassificationTests.Armor(4) with { FormKey = "000800:Test.esp", ArmorRating = 28 };
        var child = parent with { FormKey = "000801:Test.esp", ArmorRating = 25, Template = new(parent.FormKey, null, "Resolved") };
        var report = InventoryAudit.Build([parent, child], [], new());
        Assert.AreEqual(28, Field(report.TemplateAnalysis.Single(), "ArmorRating").ProposedEffectiveValue.GetDouble());
        var audited = report.Equipment.Single(e => e.Snapshot.FormKey == child.FormKey);
        Assert.AreEqual(25d, audited.Snapshot.ArmorRating); Assert.IsFalse(audited.Eligibility.InventoryStatisticsEligible);
        Assert.AreEqual(1, report.Groups.Single().Count);
        var staff = Weapon("000802:Test.esp") with { AnimationType = "Staff" };
        var staffChild = Weapon("000803:Test.esp", staff.FormKey);
        var staffReport = InventoryAudit.Build([staff, staffChild], [], new());
        Assert.IsTrue(staffReport.TemplateAnalysis.Single().HypotheticalClassifications.All(h => h.Classification.Scope == "ExpectedOutOfScope" && !h.Eligibility.AnalysisEligible));
    }

    [TestMethod]
    public void TemplateReportsAreDeterministicAndLeaveAllProductionDecisionsUnchanged()
    {
        var parent = Weapon("000800:Test.esp");
        var child = Weapon("000801:Test.esp", parent.FormKey) with { Name = "Child, \"Ä\"\nname", Damage = 8 };
        var one = InventoryAudit.Build([child, parent], [], new());
        var two = InventoryAudit.Build([parent, child], [], new());
        Assert.AreEqual(ReportWriter.Json(one), ReportWriter.Json(two));
        Assert.AreEqual(ReportWriter.TemplatesCsv(one), ReportWriter.TemplatesCsv(two));
        Assert.IsTrue(ReportWriter.TemplatesCsv(one).Contains("Child, \"\"Ä\"\"\nname", StringComparison.Ordinal));
        foreach (var e in one.Equipment)
        {
            var independentlyClassified = EquipmentClassifier.Classify(e.Snapshot);
            Assert.AreEqual(JsonSerializer.Serialize(independentlyClassified), JsonSerializer.Serialize(e));
        }
        using var directory = new TemporaryDirectory();
        Assert.AreEqual(8, ReportWriter.Write(one, directory.Path).Length);
        Assert.IsFalse(Directory.GetFiles(directory.Path).Any(f => f.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)));
        using var json = JsonDocument.Parse(ReportWriter.Json(one));
        Assert.AreEqual("4", json.RootElement.GetProperty("schemaVersion").GetString());
        Assert.AreEqual(1, json.RootElement.GetProperty("templateAnalysis").GetArrayLength());
    }
}
