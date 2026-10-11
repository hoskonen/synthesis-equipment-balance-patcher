using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class WeaponDryRunTests
{
    private static EquipmentSnapshot Weapon(int id, string name, double? damage = 10) => ClassificationTests.Weapon() with
    { FormKey = $"{id:X6}:Test.esp", Name = name, Damage = damage, CriticalDamage = 3, Stagger = 0.5 };
    private static EquipmentSnapshot Reference(double? damage = 10) => Weapon(1, "Steel Sword", damage) with
    { FormKey = "013989:Skyrim.esm", DefiningPlugin = "Skyrim.esm", WinningPlugin = "Tweaks.esp" };
    private static AuditReport Build(params EquipmentSnapshot[] records) => InventoryAudit.Build(records, [], new());
    private static void Count(string text, string label, int value) => Assert.IsTrue(text.Contains(label.PadRight(39) + value + "\n"), label);

    [TestMethod]
    public void HigherAndLowerDamageUseWinningReferenceValuesAndTraceableIdentity()
    {
        var text = WeaponDryRunReport.Render(Build(Reference(), Weapon(2, "Higher Sword", 15), Weapon(3, "Lower Sword", 5)));
        Count(text, "Higher damage candidates:", 1); Count(text, "Lower damage candidates:", 1);
        Assert.IsTrue(text.Contains("Damage: 15 vs 10 (difference +5; +50%)"));
        Assert.IsTrue(text.Contains("Damage: 5 vs 10 (difference -5; -50%)"));
        Assert.IsTrue(text.Contains("Reference: Steel Sword [013989:Skyrim.esm] — winner Tweaks.esp"));
        Assert.IsTrue(text.Contains("Source: Test.esp; winner Patch.esp"));
        Assert.IsTrue(text.Contains("FormKey: 000002:Test.esp"));
    }

    [TestMethod]
    public void EqualWithinToleranceAndOtherMetricOnlyDeviationsNeverEnterHeadlines()
    {
        var text = WeaponDryRunReport.Render(Build(Reference(), Weapon(2, "Equal", 10), Weapon(3, "Close", 12),
            Weapon(4, "Heavy", 10) with { Weight = 100, CriticalDamage = 30 }));
        Count(text, "Higher damage candidates:", 0); Count(text, "Lower damage candidates:", 0);
        Assert.IsFalse(text.Contains("1. Heavy")); Count(text, "Base damage comparisons:", 3);
        Assert.IsTrue(text.Contains("None with a defined percentage."));
    }

    [TestMethod]
    public void ExistingMetricToleranceResultIsAuthoritative()
    {
        var report = Build(Reference(), Weapon(2, "AtBoundary", 13));
        Count(WeaponDryRunReport.Render(report), "Higher damage candidates:", 1);
        var evidence = report.WeaponEvidenceAnalysis!;
        var rows = evidence.Records.Select(r => r with { ReferenceMetrics = r.ReferenceMetrics.Select(m => m with { Status = "WithinTolerance" }).ToArray() }).ToArray();
        var changed = report with { WeaponEvidenceAnalysis = evidence with { Records = rows } };
        Count(WeaponDryRunReport.Render(changed), "Higher damage candidates:", 0);
        var settings = new Settings { BalanceThresholds = new() { Damage = new(100) } };
        Count(WeaponDryRunReport.Render(InventoryAudit.Build([Reference(), Weapon(2, "High", 20)], [], settings)), "Higher damage candidates:", 0);
    }

    [TestMethod]
    public void ExistingNamedSpecialCustomAndStaffRestrictionsAreRetained()
    {
        var report = Build(Reference(), Weapon(2, "Blade of Woe", 100), Weapon(3, "Steel Spear", 100),
            Weapon(4, "Staff", 100) with { AnimationType = "Staff" });
        var text = WeaponDryRunReport.Render(report);
        Count(text, "Higher damage candidates:", 0); Count(text, "Weapons inspected:", 1);
        Count(text, "Excluded from ordinary inspection:", 3);
        Assert.IsTrue(report.Equipment.Single(e => e.Snapshot.Name == "Blade of Woe").Eligibility.AnalysisEligible);
    }

    [TestMethod]
    public void SelfMissingReferencesAndUnavailableDamageAreExplicitAndUnassessed()
    {
        Count(WeaponDryRunReport.Render(Build(Reference(100))), "Higher damage candidates:", 0);
        var missing = WeaponDryRunReport.Render(Build(Weapon(2, "MissingReference", 100)));
        Count(missing, "Compared with references (non-self):", 0); Count(missing, "No numerical comparison (unassessed):", 1);
        Assert.IsTrue(missing.Contains("Unassessed does not mean balanced."));
        var unavailable = WeaponDryRunReport.Render(Build(Reference(), Weapon(2, "MissingDamage", null)));
        Count(unavailable, "Higher damage candidates:", 0); Count(unavailable, "Compared but damage unavailable:", 1);
        var badReference = Reference() with { Template = new("000999:Test.esp", null, "Unresolved") };
        Count(WeaponDryRunReport.Render(Build(badReference, Weapon(2, "BadReference", 100))), "Higher damage candidates:", 0);
    }

    [TestMethod]
    public void UndefinedPercentagesRemainOutsidePercentageRankings()
    {
        var text = WeaponDryRunReport.Render(Build(Reference(0), Weapon(2, "ZeroRoot", 3)));
        Count(text, "Higher damage candidates:", 1); Count(text, "Undefined percentage (not ranked):", 1);
        Assert.IsTrue(text.Contains("Undefined Percentage — Unranked Review Candidates"));
        Assert.IsTrue(text.Contains("Damage: 3 vs 0 (difference +3; percentage undefined)"));
        Assert.AreEqual(1, text.Split("1. ZeroRoot").Length - 1);
    }

    [TestMethod]
    public void DuplicateMetricAndEvidenceRowsCannotDuplicateAWeapon()
    {
        var report = Build(Reference(), Weapon(2, "OnceOnly", 20));
        var evidence = report.WeaponEvidenceAnalysis!;
        var row = evidence.Records.Single(r => r.FormKey == "000002:Test.esp");
        row = row with { ReferenceMetrics = row.ReferenceMetrics.Concat(row.ReferenceMetrics).ToArray() };
        report = report with { WeaponEvidenceAnalysis = evidence with { Records = [evidence.Records.Single(r => r.FormKey == Reference().FormKey), row, row] } };
        var text = WeaponDryRunReport.Render(report);
        Count(text, "Higher damage candidates:", 1); Assert.AreEqual(1, text.Split("1. OnceOnly").Length - 1);
    }

    [TestMethod]
    public void PercentageAbsoluteDifferenceAndFormKeyDetermineOrderAcrossCultures()
    {
        var ironRoot = Reference(20) with { FormKey = "012EB7:Skyrim.esm", Name = "Iron Sword", Keywords = [ClassificationTests.Keyword("01E718:Skyrim.esm")] };
        var iron = Weapon(9, "LargerAbsolute", 30) with { Keywords = ironRoot.Keywords };
        var records = new[] { Reference(), ironRoot, iron, Weapon(3, "TieB", 15), Weapon(2, "TieA", 15), Weapon(4, "LargestPercentage", 20) };
        var text = WeaponDryRunReport.Render(Build(records));
        Assert.IsTrue(text.IndexOf("1. LargestPercentage", StringComparison.Ordinal) < text.IndexOf("2. LargerAbsolute", StringComparison.Ordinal));
        Assert.IsTrue(text.Contains("3. TieA")); Assert.IsTrue(text.Contains("4. TieB"));
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");
            Assert.AreEqual(text, WeaponDryRunReport.Render(Build(records.Reverse().ToArray())));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [TestMethod]
    public void BothDirectionsAreLimitedToTenWithoutTruncatingTotalCounts()
    {
        var high = Enumerable.Range(2, 12).Select(i => Weapon(i, $"High{i}", 100 + i));
        var low = Enumerable.Range(20, 12).Select(i => Weapon(i, $"Low{i}", 100 - i));
        var stronger = high.Select(s => s with { Damage = s.Damage + 100 });
        var weaker = low.Select(s => s with { Damage = s.Damage - 50 });
        var text = WeaponDryRunReport.Render(Build(stronger.Concat(weaker).Prepend(Reference(100)).ToArray()));
        Count(text, "Higher damage candidates:", 12); Count(text, "Lower damage candidates:", 12);
        Assert.IsTrue(text.Contains("1. High13")); Assert.IsFalse(text.Contains(". High2 —"));
        Assert.IsTrue(text.Contains("1. Low31")); Assert.IsFalse(text.Contains(". Low20 —"));
        Assert.AreEqual(2, text.Split("Showing 10 of 12").Length - 1);
    }

    [TestMethod]
    public void StructuralWarningsAndCoverageRemainVisible()
    {
        var report = Build(Reference(), Weapon(2, "Structural", 20) with { Skill = "OneHanded" },
            Weapon(3, "NoCatalog", 10) with { AnimationType = "OneHandAxe" }, Weapon(4, "NoMaterial", 10) with { Keywords = [] },
            Weapon(5, "Steel Staff", 10) with { AnimationType = "Staff" });
        var text = WeaponDryRunReport.Render(report);
        Assert.IsTrue(text.Contains("Review: structure differs from reference."));
        Count(text, "Total weapons in inventory:", 5); Count(text, "Weapons inspected:", 4);
        Count(text, "Compared with references (non-self):", 1); Count(text, "Canonical references themselves:", 1);
        Count(text, "No reviewed canonical reference:", 1); Count(text, "No numerical comparison (unassessed):", 2);
        Count(text, "Incomplete material evidence:", 3);
    }

    [TestMethod]
    public void ExcludedMaterialConflictsAreCountedOnceEvenWhenBothSourcesDisagree()
    {
        var item = Weapon(2, "Ebony Staff") with { AnimationType = "Staff", Keywords = Weapon(2, "").Keywords.Append(ClassificationTests.Keyword("01E718:Skyrim.esm")).ToArray() };
        var text = WeaponDryRunReport.Render(Build(item));
        Count(text, "Excluded special-weapon conflicts:", 1); Count(text, "Ordinary material conflicts:", 0);
    }

    [TestMethod]
    public void EmptyReportAndPublishedTextMatchConsoleWithoutChangingAnalysisOrPlugins()
    {
        var empty = WeaponDryRunReport.Render(Build());
        Count(empty, "Higher damage candidates:", 0); Count(empty, "Lower damage candidates:", 0);
        var report = Build(Reference(), Weapon(2, "Saved", 20));
        var before = ReportWriter.Json(report); var csv = ReportWriter.WeaponEvidenceCsv(report);
        using var directory = new TemporaryDirectory();
        var plugin = Path.Combine(directory.Path, "Existing.esp"); File.WriteAllBytes(plugin, [1, 2, 3]);
        var time = File.GetLastWriteTimeUtc(plugin);
        var paths = ReportWriter.Write(report, directory.Path);
        var saved = File.ReadAllText(Path.Combine(directory.Path, "WeaponDryRun.txt"));
        Assert.AreEqual(WeaponDryRunReport.Render(report), saved); Assert.IsTrue(ReportWriter.ConsoleSummary(report, paths).EndsWith(saved, StringComparison.Ordinal));
        Assert.IsFalse(saved.Contains('\r')); Assert.AreEqual(before, ReportWriter.Json(report)); Assert.AreEqual(csv, ReportWriter.WeaponEvidenceCsv(report));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(plugin)); Assert.AreEqual(time, File.GetLastWriteTimeUtc(plugin));
        Assert.AreEqual(1, Directory.GetFiles(directory.Path, "*.esp").Length); Assert.AreEqual(11, paths.Length);
        Assert.IsTrue(Program.AuditPreferences().NoPatch);
    }
}
