using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
public sealed class ReportingTests
{
    private static AuditReport Build(params EquipmentSnapshot[] snapshots) =>
        InventoryAudit.Build(snapshots, [new("Skyrim.esm", true), new("Test.esp", true)], new Settings());

    [TestMethod]
    public void DescriptiveStatisticsUseAvailableValuesOnly()
    {
        var metric = InventoryAudit.Describe([2, null, 4, 8, double.NaN]);
        Assert.AreEqual(3, metric.AvailableCount);
        Assert.AreEqual(2, metric.MissingCount);
        Assert.AreEqual(2d, metric.Minimum);
        Assert.AreEqual(8d, metric.Maximum);
        Assert.AreEqual(14d / 3, metric.Mean!.Value, 1e-12);
        Assert.AreEqual(4d, metric.Median);
        Assert.AreEqual(3d, InventoryAudit.Describe([2, 4]).Median);
        Assert.IsNull(InventoryAudit.Describe([null]).Mean);
    }

    [TestMethod]
    public void GroupsSeparateEnchantmentAndAccountForBaselineExclusions()
    {
        var ordinary = ClassificationTests.Weapon();
        var enchanted = ordinary with { FormKey = "000801:Test.esp", Enchantment = new("000900:Test.esp", null, "Resolved") };
        var scripted = ordinary with { FormKey = "000802:Test.esp", HasScripts = true, Speed = null };
        var deleted = ordinary with { FormKey = "000803:Test.esp", Deleted = true };
        var templated = ordinary with { FormKey = "000804:Test.esp", Template = new("000800:Test.esp", null, "Resolved") };
        var report = Build(ordinary, enchanted, scripted, deleted, templated);
        Assert.AreEqual(5, report.Summary.Weapons);
        Assert.AreEqual(3, report.Summary.StatisticsEligible);
        Assert.AreEqual(1, report.Summary.BaselineEligible);
        Assert.AreEqual(2, report.Groups.Length);
        var group = report.Groups.Single(g => !g.Enchanted);
        Assert.AreEqual(2, group.Count);
        Assert.AreEqual(1, group.BaselineEligibleCount);
        Assert.AreEqual(1, group.BaselineExcludedCount);
        Assert.AreEqual(1, group.Metrics["Speed"].MissingCount);
    }

    [TestMethod]
    public void ReportsAreDeterministicAcrossInputOrderingAndCultures()
    {
        var first = ClassificationTests.Weapon() with { FormKey = "000802:Test.esp", Damage = 11, Speed = 1.2 };
        var second = ClassificationTests.Weapon() with { FormKey = "000801:Test.esp", HasScripts = true };
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");
            var one = Build(first, second);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var two = Build(second, first);
            Assert.AreEqual(ReportWriter.Json(one), ReportWriter.Json(two));
            Assert.AreEqual(ReportWriter.InventoryCsv(one), ReportWriter.InventoryCsv(two));
            Assert.AreEqual(ReportWriter.GroupsCsv(one), ReportWriter.GroupsCsv(two));
            Assert.AreEqual(ReportWriter.IssuesCsv(one), ReportWriter.IssuesCsv(two));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [TestMethod]
    public void CsvEscapesQuotesCommasNewlinesAndPreservesUnicode()
    {
        Assert.AreEqual("\"Steel, \"\"special\"\"\nÄ\"", ReportWriter.CsvCell("Steel, \"special\"\nÄ"));
        Assert.AreEqual("", ReportWriter.CsvCell(null));
        var report = Build(ClassificationTests.Weapon() with { Name = "Steel, \"special\"\nÄ", EditorId = null });
        var csv = ReportWriter.InventoryCsv(report);
        Assert.IsTrue(csv.Contains("\"Steel, \"\"special\"\"\nÄ\"", StringComparison.Ordinal));
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(csv))
        {
            TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(",");
        var headers = parser.ReadFields()!;
        var row = parser.ReadFields()!;
        Assert.AreEqual(headers.Length, row.Length);
        Assert.AreEqual("Steel, \"special\"\nÄ", row[Array.IndexOf(headers, "name")]);
        Assert.IsTrue(parser.EndOfData);
        using var json = JsonDocument.Parse(ReportWriter.Json(report));
        var snapshot = json.RootElement.GetProperty("equipment")[0].GetProperty("snapshot");
        Assert.AreEqual(JsonValueKind.Null, snapshot.GetProperty("editorId").ValueKind);
        Assert.AreEqual("Steel, \"special\"\nÄ", snapshot.GetProperty("name").GetString());
    }

    [TestMethod]
    public void CancellationDoesNotPublishReports()
    {
        using var directory = new TemporaryDirectory();
        var existing = Path.Combine(directory.Path, "audit.json");
        File.WriteAllText(existing, "previous");
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => ReportWriter.Write(Build(), directory.Path, cancel.Token));
        Assert.AreEqual("previous", File.ReadAllText(existing));
        Assert.AreEqual(1, Directory.GetFiles(directory.Path).Length);
        Assert.ThrowsExactly<OperationCanceledException>(() => InventoryAudit.Build([ClassificationTests.Weapon()], [], new(), cancel.Token));
    }

    [TestMethod]
    public void ReportWriterTouchesOnlyItsFourReports()
    {
        using var directory = new TemporaryDirectory();
        var plugin = Path.Combine(directory.Path, "EBP.esp");
        byte[] sentinel = [1, 2, 3, 4];
        File.WriteAllBytes(plugin, sentinel);
        var paths = ReportWriter.Write(Build(ClassificationTests.Weapon()), directory.Path);
        Assert.AreEqual(4, paths.Length);
        CollectionAssert.AreEqual(sentinel, File.ReadAllBytes(plugin));
        Assert.IsFalse(Directory.GetFiles(directory.Path).Any(p => p.EndsWith(".tmp", StringComparison.Ordinal)));
        Assert.AreEqual(ReportWriter.Json(Build(ClassificationTests.Weapon())), File.ReadAllText(Path.Combine(directory.Path, "audit.json")));
    }

    [TestMethod]
    public void OutputDirectoryIsExplicitAndInvalidPathsFail()
    {
        using var directory = new TemporaryDirectory();
        Assert.AreEqual(Path.Combine(directory.Path, "Reports"), ReportWriter.ResolveDirectory("Reports", directory.Path));
        Assert.AreEqual(directory.Path, ReportWriter.ResolveDirectory(directory.Path, null));
        Assert.ThrowsExactly<ArgumentException>(() => ReportWriter.ResolveDirectory("", directory.Path));
        var file = Path.Combine(directory.Path, "file"); File.WriteAllText(file, "existing");
        Assert.ThrowsExactly<IOException>(() => ReportWriter.ValidateDirectory(file));
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EquipmentBalancePatcher-tests-" + Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
