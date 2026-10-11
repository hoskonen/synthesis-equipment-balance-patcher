using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class ReferenceTests
{
    private static EquipmentSnapshot Sword(double damage = 10) => ClassificationTests.Weapon() with { Damage = damage, CriticalDamage = 3, Stagger = 0.5 };
    private static EquipmentSnapshot Reference(double damage = 10) => Sword(damage) with { FormKey = "013989:Skyrim.esm", DefiningPlugin = "Skyrim.esm", WinningPlugin = "Tweaks.esp" };
    private static AuditReport Build(params EquipmentSnapshot[] records) => InventoryAudit.Build(records, [], new());
    private static ReferenceResult Result(AuditReport report, string key = "000800:Test.esp") => report.ReferenceAnalysis!.Records.Single(r => r.FormKey == key);

    [TestMethod]
    public void ReviewedCatalogHasUniqueExplicitIdentitiesAndSupportedCombinations()
    {
        Assert.AreEqual(28, ReferenceComparison.Catalog.Count);
        Assert.AreEqual(28, ReferenceComparison.Catalog.Select(r => r.FormKey).Distinct().Count());
        Assert.AreEqual("013989:Skyrim.esm", ReferenceComparison.Catalog.Single(r => r.Category == "Sword" && r.Material == "Steel").FormKey);
        Assert.AreEqual("0896A3:Skyrim.esm", ReferenceComparison.Catalog.Single(r => r.Category == "Cuirass" && r.ArmorType == "LightArmor").FormKey);
        Assert.IsFalse(ReferenceComparison.Catalog.Any(r => r.Category == "Bow" && r.Material == "Steel"));
    }

    [TestMethod]
    public void WinningCanonicalOverrideSuppliesValuesWithoutMutatingEitherRecord()
    {
        var original = new SkyrimMod(ModKey.FromNameAndExtension("Skyrim.esm"), SkyrimRelease.SkyrimSE);
        var keyword = new Keyword(FormKey.Factory("01E719:Skyrim.esm"), SkyrimRelease.SkyrimSE);
        original.Keywords.Add(keyword);
        var sword = new Weapon(FormKey.Factory("013989:Skyrim.esm"), SkyrimRelease.SkyrimSE)
        {
            BasicStats = new() { Damage = 8, Weight = 10 },
            Data = new() { AnimationType = WeaponAnimationType.OneHandSword, Speed = 1, Reach = 1 },
            Keywords = [keyword.ToLink<IKeywordGetter>()]
        };
        original.Weapons.Add(sword);
        var patch = new SkyrimMod(ModKey.FromNameAndExtension("Tweaks.esp"), SkyrimRelease.SkyrimSE);
        var winner = patch.Weapons.GetOrAddAsOverride(sword); winner.BasicStats!.Damage = 10;
        ISkyrimModGetter[] listed = [original, patch];
        using var cache = listed.ToImmutableLinkCache();
        var snapshots = new EquipmentReader(cache).Read(
            listed.Reverse().Weapon().WinningContextOverrides().Select(c => ((IWeaponGetter)c.Record, c.ModKey)), []);
        var report = Build(snapshots.Single(), Sword(13));
        var metric = Result(report).Metrics.Single(m => m.Metric == "Damage");
        Assert.AreEqual(10d, metric.ReferenceValue); Assert.AreEqual(3d, metric.SignedDifference);
        Assert.AreEqual("Tweaks.esp", report.ReferenceAnalysis!.Catalog.Single(r => r.Identity.FormKey == sword.FormKey.ToString()).WinningPlugin);
        Assert.AreEqual((ushort)8, sword.BasicStats!.Damage); Assert.AreEqual((ushort)10, winner.BasicStats.Damage);
        Assert.AreEqual(1, patch.Weapons.Count);
    }

    [TestMethod]
    public void ComparisonUsesBothToleranceGatesWithoutPeerRequirements()
    {
        var settings = new Settings { MinimumIndependentPeers = 1000 };
        var report = InventoryAudit.Build([Reference(), Sword(13)], [], settings);
        var metric = Result(report).Metrics.Single(m => m.Metric == "Damage");
        Assert.AreEqual("ReferenceDeviation", metric.Status);
        Assert.AreEqual(3d, metric.AbsoluteDifference); Assert.AreEqual(30d, metric.PercentageDifference);
        Assert.AreEqual(0, report.BalanceAnalysis!.Summary.AnalyzedRecords);
        Assert.AreEqual("WithinTolerance", Result(Build(Reference(), Sword(12))).Status);
        settings.BalanceThresholds.Damage.MinimumRelativeDifference = 0.300001;
        Assert.AreEqual("WithinTolerance", Result(InventoryAudit.Build([Reference(), Sword(13)], [], settings)).Status);
        var low = Result(Build(Reference(), Sword(7))).Metrics.Single(m => m.Metric == "Damage");
        Assert.AreEqual(-3d, low.SignedDifference); Assert.AreEqual(-30d, low.PercentageDifference);
    }

    [TestMethod]
    public void MissingWrongTierWrongTypeAndTemplatedReferencesStayUnavailable()
    {
        Assert.AreEqual("ReferenceUnavailable", Result(Build(Sword())).Status);
        var wrongMaterial = Reference() with { Keywords = [ClassificationTests.Keyword("01E718:Skyrim.esm")] };
        var wrongType = ClassificationTests.Armor(4) with { FormKey = Reference().FormKey };
        var templated = Reference() with { Template = new("000999:Test.esp", null, "Unresolved") };
        foreach (var invalid in new[] { wrongMaterial, wrongType, templated })
        {
            var report = Build(invalid, Sword());
            Assert.AreEqual("ReferenceUnavailable", Result(report).Status);
            Assert.AreEqual("Unusable", report.ReferenceAnalysis!.Catalog.Single(r => r.Identity.FormKey == Reference().FormKey).Status);
        }
    }

    [TestMethod]
    public void TiersCategoriesAndArmorTypesNeverFallback()
    {
        var iron = Sword() with { Keywords = [ClassificationTests.Keyword("01E718:Skyrim.esm")] };
        Assert.AreEqual("ReferenceUnavailable", Result(Build(Reference(), iron)).Status);
        Assert.AreEqual("012EB7:Skyrim.esm", Result(Build(Reference(), iron)).ReferenceFormKey);
        var bow = Sword() with { AnimationType = "Bow" };
        Assert.AreEqual("NoReference", Result(Build(Reference(), bow)).Status);
        var heavy = ClassificationTests.Armor(4);
        var reference = heavy with { FormKey = "013952:Skyrim.esm" };
        Assert.AreEqual("WithinTolerance", Result(Build(reference, heavy), heavy.FormKey).Status);
        Assert.AreEqual("NoReference", Result(Build(reference, heavy with { ArmorType = "LightArmor" }), heavy.FormKey).Status);
    }

    [TestMethod]
    public void ZeroMissingAndSelfValuesAreExplicit()
    {
        var zero = Result(Build(Reference(0), Sword(3))).Metrics.Single(m => m.Metric == "Damage");
        Assert.IsNull(zero.PercentageDifference); Assert.AreEqual(3d, zero.AbsoluteDifference);
        Assert.AreEqual("ReferenceDeviation", zero.Status);
        var missing = Result(Build(Reference() with { Speed = null }, Sword()));
        Assert.AreEqual("ReferenceUnavailable", missing.Status);
        Assert.AreEqual("Unavailable", missing.Metrics.Single(m => m.Metric == "Speed").Status);
        var self = Result(Build(Reference(100)), Reference().FormKey);
        Assert.AreEqual("Reference", self.Status);
        Assert.IsFalse(self.Metrics.Any(m => m.Status == "ReferenceDeviation"));
    }

    [TestMethod]
    public void TemplatesCustomWeaponsAndSpecialRecordsRemainExcludedAndConcise()
    {
        var excluded = new[] { Sword() with { Template = new(Reference().FormKey, null, "Resolved") },
            Sword() with { AnimationType = "Staff" }, Sword() with { Playable = false },
            Sword() with { Keywords = Sword().Keywords.Append(ClassificationTests.Keyword("000900:Mod.esp", "WeapTypeSpear")).ToArray() } };
        foreach (var item in excluded)
        {
            var report = Build(Reference(), item);
            Assert.AreEqual("Excluded", Result(report).Status);
            Assert.IsFalse(ReportWriter.ReferenceComparisonsCsv(report).Contains(item.FormKey));
        }
    }

    [TestMethod]
    public void EnchantmentSettingIsSharedWithoutChangingStoredEligibility()
    {
        var item = Sword(13) with { Enchantment = new("000999:Test.esp", null, "Resolved") };
        Assert.AreEqual("ReferenceDeviation", Result(Build(Reference(), item)).Status);
        var disabled = InventoryAudit.Build([Reference(), item], [], new() { IncludeEnchantedAnalysisCandidates = false });
        Assert.AreEqual("Excluded", Result(disabled).Status);
        Assert.IsTrue(disabled.Equipment.Single(e => e.Snapshot.FormKey == item.FormKey).Eligibility.AnalysisEligible);
        Assert.AreEqual(1, disabled.ReferenceAnalysis!.Summary.EligibleRecords);
    }

    [TestMethod]
    public void NewReportIsDeterministicAndLeavesStatisticalAndEarlierReportsUnchanged()
    {
        var records = new[] { Reference(), Sword(13) };
        var first = Build(records);
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");
            var reversed = Build(records.Reverse().ToArray());
            Assert.AreEqual(ReportWriter.Json(first), ReportWriter.Json(reversed));
            Assert.AreEqual(ReportWriter.ReferenceComparisonsCsv(first), ReportWriter.ReferenceComparisonsCsv(reversed));
        }
        finally { CultureInfo.CurrentCulture = prior; }
        var disabled = InventoryAudit.Build(records, [], new() { EnableReferenceComparison = false });
        Assert.AreEqual(JsonSerializer.Serialize(first.BalanceAnalysis), JsonSerializer.Serialize(disabled.BalanceAnalysis));
        Assert.AreEqual(ReportWriter.InventoryCsv(first), ReportWriter.InventoryCsv(disabled));
        Assert.AreEqual(ReportWriter.GroupsCsv(first), ReportWriter.GroupsCsv(disabled));
        Assert.AreEqual(ReportWriter.TemplatesCsv(first), ReportWriter.TemplatesCsv(disabled));
        Assert.AreEqual("6", first.SchemaVersion);
        Assert.AreEqual(2, first.ReferenceAnalysis!.Summary.MatchedRecords);
        Assert.AreEqual(1, first.ReferenceAnalysis.Summary.ComparableRecords);
    }
}
