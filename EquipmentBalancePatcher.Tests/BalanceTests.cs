using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class BalanceTests
{
    private static EquipmentSnapshot Weapon(int id, double damage = 10) => ClassificationTests.Weapon() with
    {
        FormKey = $"{id:X6}:Source{id}.esp", DefiningPlugin = $"Source{id}.esp", WinningPlugin = $"Winner{id}.esp",
        EditorId = $"Weapon{id}", Name = $"Weapon {id}", Damage = damage, CriticalDamage = 3, Stagger = 0.5
    };
    private static EquipmentSnapshot Armor(int id, double rating = 30) => ClassificationTests.Armor(4) with
    {
        FormKey = $"{id:X6}:Source{id}.esp", DefiningPlugin = $"Source{id}.esp", WinningPlugin = $"Winner{id}.esp",
        EditorId = $"Armor{id}", Name = $"Armor {id}", ArmorRating = rating
    };
    private static EquipmentSnapshot[] Peers() => Enumerable.Range(1, 5).Select(i => Weapon(i, 7 + i)).ToArray();
    private static AuditReport Build(IEnumerable<EquipmentSnapshot> snapshots, Settings? settings = null) =>
        InventoryAudit.Build(snapshots, [], settings ?? new());
    private static BalanceEvaluation Evaluation(AuditReport report, EquipmentSnapshot item, string metric = "Damage") =>
        report.BalanceAnalysis!.Evaluations.Single(e => e.FormKey == item.FormKey && e.Metric == metric);

    [TestMethod]
    public void ExcessiveWeaponAndArmorAreExplainedAgainstIndependentPeers()
    {
        var high = Weapon(20, 80);
        var armor = Armor(40, 100);
        var report = Build(Peers().Append(high).Concat(Enumerable.Range(30, 5).Select(i => Armor(i, i))).Append(armor));
        var finding = report.BalanceAnalysis!.Findings.Single(f => f.FormKey == high.FormKey && f.Metric == "Damage");
        Assert.AreEqual(5, finding.Reference.Count); Assert.AreEqual(10d, finding.Reference.Median);
        Assert.AreEqual(70d, finding.AbsoluteDifference); Assert.AreEqual(700d, finding.PercentageDifference);
        Assert.AreEqual("PotentialConsistencyIssue", finding.FindingType);
        Assert.IsTrue(finding.Explanation.Contains("manual review"));
        Assert.IsTrue(report.BalanceAnalysis.Findings.Any(f => f.FormKey == armor.FormKey && f.Metric == "ArmorRating"));
        Assert.AreEqual(high.DefiningPlugin, finding.DefiningPlugin); Assert.AreEqual(high.WinningPlugin, finding.WinningPlugin);
    }

    [TestMethod]
    public void MaterialSubtypeStructureAndArmorTypeAreIsolated()
    {
        var steel = Weapon(10);
        var iron = Weapon(11) with { Keywords = [ClassificationTests.Keyword("01E718:Skyrim.esm")] };
        var dagger = Weapon(12) with { AnimationType = "OneHandDagger" };
        var skill = Weapon(13) with { Skill = "OneHanded" };
        var equip = Weapon(14) with { EquipmentType = new("000123:Skyrim.esm", null, "Resolved") };
        var heavy = Armor(15);
        var light = Armor(16) with { ArmorType = "LightArmor" };
        var boot = Armor(17) with { SlotMask = 128 };
        var report = Build([steel, iron, dagger, skill, equip, heavy, light, boot]);
        Assert.AreEqual(8, report.BalanceAnalysis!.Groups.Select(g => g.CohortId).Distinct().Count());
        Assert.IsTrue(report.BalanceAnalysis.Groups.All(g => g.Distribution.Count == 1));
        Assert.AreEqual("Iron", report.Equipment.Single(e => e.Snapshot.FormKey == iron.FormKey).Classification.Material);
    }

    [TestMethod]
    public void SelfAndEnchantedDesignsDoNotContributeToPeerBaselines()
    {
        var item = Weapon(20, 30);
        var enchanted = Weapon(21, 90) with { Enchantment = new("000900:Test.esp", null, "Resolved") };
        var templated = Weapon(22, 100) with { Template = new(item.FormKey, null, "Resolved") };
        var report = Build(Peers().Concat([item, enchanted, templated]));
        var own = Evaluation(report, item);
        Assert.AreEqual(5, own.Reference.Count); Assert.IsFalse(own.PeerFormKeys.Contains(item.FormKey));
        Assert.IsFalse(own.PeerFormKeys.Contains(enchanted.FormKey));
        Assert.AreEqual(6, Evaluation(report, enchanted).Reference.Count);
        Assert.IsFalse(report.BalanceAnalysis!.Evaluations.Any(e => e.FormKey == templated.FormKey));
        Assert.IsTrue(report.BalanceAnalysis.Records.Single(r => r.FormKey == templated.FormKey).Reasons.Contains("TemplateNotEvaluated"));
        var disabled = Build(Peers().Append(enchanted), new() { IncludeEnchantedAnalysisCandidates = false });
        Assert.IsFalse(disabled.BalanceAnalysis!.Evaluations.Any(e => e.FormKey == enchanted.FormKey));
        // Statistical analysis cannot mutate the prior template prototype or any classification/eligibility.
        var noBalance = Build(Peers().Concat([item, enchanted, templated]), new() { EnableBalanceAudit = false });
        Assert.AreEqual(ReportWriter.TemplatesCsv(noBalance), ReportWriter.TemplatesCsv(report));
        Assert.AreEqual(ReportWriter.InventoryCsv(noBalance), ReportWriter.InventoryCsv(report));
        Assert.AreEqual(ReportWriter.GroupsCsv(noBalance), ReportWriter.GroupsCsv(report));
    }

    [TestMethod]
    public void EqualNumbersDoNotMergeUnrelatedDesignsAndDuplicateHintsRemainWarnings()
    {
        var records = Enumerable.Range(1, 6).Select(i => Weapon(i)).ToArray();
        var report = Build(records);
        var group = report.BalanceAnalysis!.Groups.Single(g => g.Metric == "Damage");
        Assert.AreEqual(6, group.ReferenceDesignIds.Length); Assert.AreEqual(0, group.PossibleDuplicateRecords);
        Assert.AreEqual(5, Evaluation(report, records[0]).Reference.Count);
        var duplicates = records.Select(s => s with { DefiningPlugin = "Same.esp", EditorId = "Copy", Name = "Copy" });
        var duplicateGroup = Build(duplicates).BalanceAnalysis!.Groups.Single(g => g.Metric == "Damage");
        Assert.AreEqual(6, duplicateGroup.Distribution.Count); Assert.AreEqual(5, duplicateGroup.PossibleDuplicateRecords);
        Assert.IsTrue(duplicateGroup.Warnings.Contains("PossibleDuplicateDesigns_NotDeduplicated"));
    }

    [TestMethod]
    public void SmallGroupsAndPluginConcentrationPreventConsistencyClaims()
    {
        var item = Weapon(20, 80);
        var sparse = Build(Peers().Take(4).Append(item));
        Assert.AreEqual("InsufficientPeers", Evaluation(sparse, item).Status);
        Assert.AreEqual("InsufficientEvidence", sparse.BalanceAnalysis!.Findings.Single(f => f.FormKey == item.FormKey && f.Metric == "Damage").FindingType);
        var concentrated = Build(Peers().Select(s => s with { WinningPlugin = "BigPatch.esp" }).Append(item));
        Assert.AreEqual("ConcentratedReference", Evaluation(concentrated, item).Status);
        Assert.AreEqual(1d, Evaluation(concentrated, item).Concentration.MaximumWinningShare);
        Assert.AreEqual("InsufficientEvidence", concentrated.BalanceAnalysis!.Findings.Single(f => f.FormKey == item.FormKey && f.Metric == "Damage").FindingType);
        var empty = Build([item]);
        Assert.AreEqual("NoReference", Evaluation(empty, item).Status);
        Assert.AreEqual(1, empty.BalanceAnalysis!.Summary.InsufficientPeerRecords);
    }

    [TestMethod]
    public void ZeroMadAndIdenticalValuesNeverProduceInfiniteScores()
    {
        var item = Weapon(20, 16);
        var report = Build(Enumerable.Range(1, 5).Select(i => Weapon(i)).Append(item));
        Assert.AreEqual("ZeroVariance", Evaluation(report, item).Status);
        Assert.IsNull(Evaluation(report, item).RobustDeviationScore);
        var f = report.BalanceAnalysis!.Findings.Single(f => f.FormKey == item.FormKey && f.Metric == "Damage");
        Assert.AreEqual("StatisticalDeviation", f.FindingType);
        var trivial = Build(Enumerable.Range(1, 6).Select(i => Weapon(i, i == 6 ? 11 : 10)));
        Assert.AreEqual(0, trivial.BalanceAnalysis!.Findings.Length);
        var tied = Build(new[] { 8d, 10, 10, 10, 12 }.Select((v, i) => Weapon(i + 1, v)).Append(Weapon(20, 20)));
        Assert.AreEqual("ZeroMad", Evaluation(tied, Weapon(20)).Status);
        Assert.IsNull(Evaluation(tied, Weapon(20)).RobustDeviationScore);
        Assert.IsFalse(ReportWriter.Json(tied).Contains("Infinity"));
    }

    [TestMethod]
    public void InvalidAndMissingMetricsAreExplicitAndNearZeroMedianHasNoPercentage()
    {
        var item = Weapon(20, 10) with { Speed = null, Reach = -1, CriticalDamage = double.NaN };
        // Build analysis directly: no JSON serialization of intentionally nonfinite synthetic raw data.
        var analyzed = Peers().Append(item).Select(EquipmentClassifier.Classify).ToArray();
        var result = BalanceAudit.Build(analyzed, new());
        foreach (var metric in new[] { "Speed", "Reach", "CriticalDamage" })
            Assert.AreEqual("InvalidMetric", result.Evaluations.Single(e => e.FormKey == item.FormKey && e.Metric == metric).Status);
        var zero = Build(Enumerable.Range(1, 5).Select(i => Weapon(i, 1e-12)).Append(Weapon(20, 10)));
        Assert.IsNull(Evaluation(zero, Weapon(20)).PercentageDifference);
        Assert.IsTrue(zero.BalanceAnalysis!.Findings.Any(f => f.FormKey == Weapon(20).FormKey && f.PercentageDifference is null));
    }

    [TestMethod]
    public void SignedDeviationsAndThresholdBoundariesAreInterpretable()
    {
        var high = Weapon(20, 16); var low = Weapon(20, 4);
        var peers = Enumerable.Range(1, 5).Select(i => Weapon(i)).ToArray();
        Assert.AreEqual(6d, Build(peers.Append(high)).BalanceAnalysis!.Findings.Single().SignedDifference);
        Assert.AreEqual(-6d, Build(peers.Append(low)).BalanceAnalysis!.Findings.Single().SignedDifference);
        Assert.AreEqual(0, Build(peers.Append(Weapon(20, 15.999))).BalanceAnalysis!.Findings.Length);
        var relative = new Settings { BalanceThresholds = new() { Damage = new(0) { MinimumRelativeDifference = 0.3 } } };
        Assert.AreEqual(1, Build(peers.Append(high), relative).BalanceAnalysis!.Findings.Length);
        relative.BalanceThresholds.Damage.MinimumRelativeDifference = 0.300001;
        Assert.AreEqual(0, Build(peers.Append(high), relative).BalanceAnalysis!.Findings.Length);
    }

    [TestMethod]
    public void SettingsRejectInvalidThresholdsAndDisabledAuditWritesEmptyReports()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Build([], new() { MinimumIndependentPeers = 2 }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Build([], new() { RobustDeviationThreshold = double.PositiveInfinity }));
        Assert.ThrowsExactly<ArgumentException>(() => Build([], new() { BalanceThresholds = new() { Speed = new(-1) } }));
        var disabled = Build(Peers(), new() { EnableBalanceAudit = false });
        Assert.AreEqual(0, disabled.BalanceAnalysis!.Groups.Length);
        Assert.AreEqual(0, disabled.BalanceAnalysis.Findings.Length);
        Assert.AreEqual(1, ReportWriter.BalanceFindingsCsv(disabled).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [TestMethod]
    public void DistributionsAndReportsAreDeterministicAndCarryReproduciblePeers()
    {
        var stats = BalanceAudit.Describe([8, 9, 10, 11, 12]);
        Assert.AreEqual(10d, stats.Median); Assert.AreEqual(1d, stats.Mad);
        Assert.AreEqual(9d, stats.Q1); Assert.AreEqual(11d, stats.Q3);
        var records = Peers().Append(Weapon(20, 80)).ToArray();
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI"); var one = Build(records);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); var two = Build(records.Reverse());
            Assert.AreEqual(ReportWriter.Json(one), ReportWriter.Json(two));
            Assert.AreEqual(ReportWriter.BalanceFindingsCsv(one), ReportWriter.BalanceFindingsCsv(two));
            Assert.AreEqual(ReportWriter.BalanceGroupsCsv(one), ReportWriter.BalanceGroupsCsv(two));
            using var directory = new TemporaryDirectory();
            Assert.AreEqual(11, ReportWriter.Write(one, directory.Path).Length);
            Assert.IsFalse(Directory.GetFiles(directory.Path).Any(p => p.EndsWith(".esp") || p.EndsWith(".esl")));
            using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(ReportWriter.BalanceFindingsCsv(one)))
                { TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
            parser.SetDelimiters(","); var header = parser.ReadFields()!;
            while (!parser.EndOfData) Assert.AreEqual(header.Length, parser.ReadFields()!.Length);
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [TestMethod]
    public void MaterialProgressionIsSeparateAndInsufficientPairsStayUncertain()
    {
        var iron = Enumerable.Range(1, 6).Select(i => Weapon(i, 30) with { Keywords = [ClassificationTests.Keyword("01E718:Skyrim.esm")] });
        var steel = Enumerable.Range(20, 6).Select(i => Weapon(i, 10));
        var result = Build(iron.Concat(steel));
        Assert.AreEqual("PossibleProgressionInversion", result.BalanceAnalysis!.MaterialProgression.Single().Status);
        Assert.AreEqual(0, result.BalanceAnalysis.Findings.Length);
        var sparse = Build(iron.Take(2).Concat(steel));
        Assert.AreEqual("InsufficientEvidence", sparse.BalanceAnalysis!.MaterialProgression.Single().Status);
        Assert.AreEqual(0, Build(iron.Concat(steel), new() { EnableMaterialProgressionReport = false }).BalanceAnalysis!.MaterialProgression.Length);
    }

    [TestMethod]
    public void PluginCaseAndExtremeFiniteMetricsCannotHideConcentrationOrBreakSerialization()
    {
        var concentration = BalanceAudit.Concentration([Weapon(1) with { DefiningPlugin = "Same.esp" },
            Weapon(2) with { DefiningPlugin = "same.esp" }]);
        Assert.AreEqual(1, concentration.DefiningPlugins.Count);
        Assert.AreEqual(1d, concentration.MaximumDefiningShare);
        Assert.IsTrue(double.IsFinite(BalanceAudit.Describe(Enumerable.Repeat(double.MaxValue, 10)).Mean!.Value));
        var result = BalanceAudit.Build(Peers().Append(Weapon(20, double.MaxValue)).Select(EquipmentClassifier.Classify).ToArray(), new());
        var evaluation = result.Evaluations.Single(e => e.FormKey == Weapon(20).FormKey && e.Metric == "Damage");
        Assert.IsNull(evaluation.PercentageDifference);
        JsonSerializer.Serialize(result);
    }
}
