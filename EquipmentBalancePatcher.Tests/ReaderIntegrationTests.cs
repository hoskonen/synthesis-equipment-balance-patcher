using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Synthesis.CLI;

namespace EquipmentBalancePatcher.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ReaderIntegrationTests
{
    [TestMethod]
    public void ReaderInventoriesWinningOverridesOnceWithoutMutatingInputs()
    {
        var original = new SkyrimMod(ModKey.FromNameAndExtension("Skyrim.esm"), SkyrimRelease.SkyrimSE);
        var material = new Keyword(FormKey.Factory("01E719:Skyrim.esm"), SkyrimRelease.SkyrimSE) { EditorID = "WeapMaterialSteel" };
        original.Keywords.Add(material);
        var weapon = original.Weapons.AddNew();
        weapon.EditorID = "TestSword"; weapon.Name = "Test sword";
        weapon.BasicStats = new WeaponBasicStats { Damage = 8, Weight = 9, Value = 100 };
        weapon.Data = new WeaponData { AnimationType = WeaponAnimationType.OneHandSword, Speed = 1, Reach = 1 };
        weapon.Keywords = [material.ToLink<IKeywordGetter>()];
        var baseArmor = original.Armors.AddNew();
        baseArmor.EditorID = "MissingBody"; baseArmor.BodyTemplate = null;
        var patch = new SkyrimMod(ModKey.FromNameAndExtension("Patch.esp"), SkyrimRelease.SkyrimSE);
        var winner = patch.Weapons.GetOrAddAsOverride(weapon);
        winner.BasicStats!.Damage = 12;
        var deleted = patch.Armors.GetOrAddAsOverride(baseArmor); deleted.IsDeleted = true;
        ISkyrimModGetter[] listed = [original, patch];
        using var cache = listed.ToImmutableLinkCache();
        var before = winner.BasicStats.Damage;
        var weaponPasses = 0; var armorPasses = 0;
        IEnumerable<(IWeaponGetter, ModKey)> Weapons()
        {
            weaponPasses++;
            foreach (var context in listed.Reverse().Weapon().WinningContextOverrides(includeDeletedRecords: true))
                yield return (context.Record, context.ModKey);
        }
        IEnumerable<(IArmorGetter, ModKey)> Armors()
        {
            armorPasses++;
            foreach (var context in listed.Reverse().Armor().WinningContextOverrides(includeDeletedRecords: true))
                yield return (context.Record, context.ModKey);
        }
        var snapshots = new EquipmentReader(cache).Read(Weapons(), Armors());
        var report = InventoryAudit.Build(snapshots, [], new());
        Assert.AreEqual(1, weaponPasses); Assert.AreEqual(1, armorPasses);
        Assert.AreEqual(2, snapshots.Length);
        var observed = snapshots.Single(s => s.RecordType == "WEAP");
        Assert.AreEqual("Skyrim.esm", observed.DefiningPlugin);
        Assert.AreEqual("Patch.esp", observed.WinningPlugin);
        Assert.AreEqual(12d, observed.Damage);
        Assert.AreEqual("Resolved", observed.Keywords.Single().Status);
        Assert.AreEqual(1, report.Summary.Deleted);
        Assert.AreEqual(before, winner.BasicStats.Damage);
        Assert.AreEqual((ushort)8, weapon.BasicStats.Damage);
        Assert.AreEqual(1, patch.Weapons.Count); Assert.AreEqual(1, patch.Armors.Count);
    }

    [TestMethod]
    public void ReaderPreservesMissingAndNonfiniteFields()
    {
        var mod = new SkyrimMod(ModKey.FromNameAndExtension("Test.esp"), SkyrimRelease.SkyrimSE);
        var weapon = mod.Weapons.AddNew(); weapon.BasicStats = null;
        weapon.Data = new WeaponData { Speed = float.NaN, Reach = float.PositiveInfinity };
        weapon.Keywords = [new FormLink<IKeywordGetter>(FormKey.Factory("000999:Missing.esm"))];
        weapon.Template.SetTo(weapon.FormKey); // Self template must be recorded, never followed.
        using var cache = mod.ToImmutableLinkCache();
        var snapshot = new EquipmentReader(cache).ReadWeapon(weapon, mod.ModKey);
        Assert.IsNull(snapshot.Damage); Assert.IsNull(snapshot.Speed); Assert.IsNull(snapshot.Reach);
        Assert.AreEqual("Unresolved", snapshot.Keywords.Single().Status);
        Assert.AreEqual(weapon.FormKey.ToString(), snapshot.Template!.FormKey);
        Assert.AreEqual(2, snapshot.ExtractionDiagnostics.Count(d => d.Code == "NonFiniteMetric"));
        JsonSerializationDoesNotThrow(snapshot);
    }

    private static void JsonSerializationDoesNotThrow(EquipmentSnapshot snapshot) =>
        ReportWriter.Json(InventoryAudit.Build([snapshot], [], new()));

    [TestMethod]
    public void TemplateLinksDoNotFlattenStoredWeaponOrArmorFields()
    {
        var mod = new SkyrimMod(ModKey.FromNameAndExtension("Test.esp"), SkyrimRelease.SkyrimSE);
        var parentWeapon = mod.Weapons.AddNew(); parentWeapon.BasicStats = new() { Damage = 7 };
        var childWeapon = mod.Weapons.AddNew(); childWeapon.BasicStats = new() { Damage = 8 };
        childWeapon.Template.SetTo(parentWeapon.FormKey);
        var parentArmor = mod.Armors.AddNew(); parentArmor.ArmorRating = 28;
        var childArmor = mod.Armors.AddNew(); childArmor.ArmorRating = 25;
        childArmor.TemplateArmor.SetTo(parentArmor.FormKey);
        using var cache = mod.ToImmutableLinkCache();
        var reader = new EquipmentReader(cache);
        var weapon = reader.ReadWeapon(childWeapon, mod.ModKey);
        var armor = reader.ReadArmor(childArmor, mod.ModKey);
        Assert.AreEqual(8d, weapon.Damage); Assert.AreEqual(25d, armor.ArmorRating);
        Assert.AreEqual("StoredRecord", weapon.StatSemantics);
        Assert.AreEqual("Resolved", weapon.Template!.Status);
        Assert.IsFalse(EquipmentClassifier.Classify(weapon).Eligibility.InventoryStatisticsEligible);
        Assert.IsFalse(EquipmentClassifier.Classify(armor).Eligibility.BaselineEligible);
        Assert.AreEqual((ushort)7, parentWeapon.BasicStats.Damage); Assert.AreEqual(28f, parentArmor.ArmorRating);
    }

    [TestMethod]
    public void TemplatePrototypeUsesOverriddenWinningParentWithoutChangingChild()
    {
        var original = new SkyrimMod(ModKey.FromNameAndExtension("Test.esm"), SkyrimRelease.SkyrimSE);
        var parent = original.Weapons.AddNew(); parent.BasicStats = new() { Damage = 7 };
        var child = original.Weapons.AddNew(); child.BasicStats = new() { Damage = 8 };
        child.Template.SetTo(parent.FormKey);
        var patch = new SkyrimMod(ModKey.FromNameAndExtension("Patch.esp"), SkyrimRelease.SkyrimSE);
        var winningParent = patch.Weapons.GetOrAddAsOverride(parent); winningParent.BasicStats!.Damage = 11;
        ISkyrimModGetter[] listed = [original, patch];
        using var cache = listed.ToImmutableLinkCache();
        var snapshots = new EquipmentReader(cache).Read(listed.Reverse().Weapon()
            .WinningContextOverrides(includeDeletedRecords: true).Select(c => (c.Record, c.ModKey)), []);
        var report = InventoryAudit.Build(snapshots, [], new());
        var proposed = report.TemplateAnalysis.Single().Fields.Single(f => f.Name == "Damage");
        Assert.AreEqual(8, proposed.RawChildValue.GetDouble()); Assert.AreEqual(11, proposed.ProposedEffectiveValue.GetDouble());
        Assert.AreEqual("Patch.esp", proposed.SourceWinningPlugin);
        Assert.AreEqual((ushort)7, parent.BasicStats.Damage); Assert.AreEqual((ushort)8, child.BasicStats.Damage);
        Assert.AreEqual((ushort)11, winningParent.BasicStats.Damage);
        Assert.IsFalse(report.Equipment.Single(e => e.Snapshot.FormKey == child.FormKey.ToString()).Eligibility.AnalysisEligible);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task SynthesisNoPatchNeitherCreatesNorOverwritesPlugin(bool existingOutput, bool separateProcess)
    {
        using var directory = new TemporaryDirectory();
        var data = Path.Combine(directory.Path, "Data"); Directory.CreateDirectory(data);
        var original = new SkyrimMod(ModKey.FromNameAndExtension("Skyrim.esm"), SkyrimRelease.SkyrimSE);
        var weapon = original.Weapons.AddNew();
        weapon.EditorID = "SyntheticSword";
        weapon.BasicStats = new WeaponBasicStats { Damage = 8, Weight = 9, Value = 100 };
        weapon.Data = new WeaponData { AnimationType = WeaponAnimationType.OneHandSword, Speed = 1, Reach = 1 };
        var templateChild = original.Weapons.AddNew();
        templateChild.EditorID = "SyntheticTemplateChild";
        templateChild.BasicStats = new WeaponBasicStats { Damage = 9, Weight = 10, Value = 101 };
        templateChild.Template.SetTo(weapon.FormKey);
        original.WriteToBinary(Path.Combine(data, "Skyrim.esm")); // Test fixture setup only.
        var pluginList = Path.Combine(directory.Path, "plugins.txt"); File.WriteAllText(pluginList, "*Skyrim.esm\n");
        var output = Path.Combine(directory.Path, "AuditOutput.esp");
        if (existingOutput)
        {
            var preceding = new SkyrimMod(ModKey.FromNameAndExtension("AuditOutput.esp"), SkyrimRelease.SkyrimSE);
            preceding.Weapons.GetOrAddAsOverride(weapon).BasicStats!.Damage = 14;
            preceding.WriteToBinary(output); // A preceding patcher's result, not an audit write.
        }
        var beforeOutput = existingOutput ? File.ReadAllBytes(output) : null;
        var inputBytes = File.ReadAllBytes(Path.Combine(data, "Skyrim.esm"));
        var outputTime = existingOutput ? File.GetLastWriteTimeUtc(output) : default;
        var ran = false;
        var reports = Path.Combine(directory.Path, "Reports");
        var runArguments = new List<string> { "run-patcher", "--GameRelease", "SkyrimSE",
            "--DataFolderPath", data, "--LoadOrderFilePath", pluginList, "--OutputPath", output,
            "--ExtraDataFolder", directory.Path, "--ModKey", "AuditOutput.esp", "--LoadOrderIncludesCreationClub", "true" };
        if (existingOutput) { runArguments.Add("--SourcePath"); runArguments.Add(output); }
        int exitCode;
        if (separateProcess)
        {
            File.WriteAllText(Path.Combine(directory.Path, "settings.json"),
                System.Text.Json.JsonSerializer.Serialize(new Settings { OutputDirectory = reports }));
            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = directory.Path
            };
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
            foreach (var argument in runArguments)
                start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
            var text = await stdout;
            exitCode = process.ExitCode;
            Assert.AreEqual(0, exitCode, await stderr + "\n" + text);
            Assert.IsTrue(text.Contains("Reports published; audit callback returning normally.", StringComparison.Ordinal));
            Assert.IsTrue(text.Contains("Synthesis pipeline returned; exit code 0.", StringComparison.Ordinal));
            ran = true;
        }
        else exitCode = await SynthesisPipeline.Instance
            .AddPatch<ISkyrimMod, ISkyrimModGetter>(state =>
            {
                ran = true;
                Program.RunAudit(state, new Settings { OutputDirectory = reports });
            }, Program.AuditPreferences())
            .Run(runArguments.ToArray()).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(ran);
        Assert.IsTrue(Program.AuditPreferences().NoPatch);
        if (beforeOutput is null) Assert.IsFalse(File.Exists(output));
        else
        {
            CollectionAssert.AreEqual(beforeOutput, File.ReadAllBytes(output));
            Assert.AreEqual(outputTime, File.GetLastWriteTimeUtc(output));
        }
        CollectionAssert.AreEqual(inputBytes, File.ReadAllBytes(Path.Combine(data, "Skyrim.esm")));
        Assert.AreEqual(11, Directory.GetFiles(reports).Length);
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(reports, "audit.json")));
        var observed = json.RootElement.GetProperty("equipment")[0].GetProperty("snapshot");
        Assert.AreEqual(existingOutput ? 14 : 8, observed.GetProperty("damage").GetInt32());
        Assert.AreEqual(existingOutput ? "AuditOutput.esp" : "Skyrim.esm", observed.GetProperty("winningPlugin").GetString());
        var templateEntry = json.RootElement.GetProperty("templateAnalysis")[0];
        Assert.AreEqual("Resolved", templateEntry.GetProperty("chain").GetProperty("status").GetString());
        var proposedDamage = templateEntry.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("name").GetString() == "Damage");
        Assert.AreEqual(9, proposedDamage.GetProperty("rawChildValue").GetInt32());
        Assert.AreEqual(existingOutput ? 14 : 8, proposedDamage.GetProperty("proposedEffectiveValue").GetInt32());
        Assert.AreEqual("Unvalidated", proposedDamage.GetProperty("validationStatus").GetString());
        Assert.AreEqual(existingOutput ? 1 : 0, Directory.GetFiles(directory.Path, "*.esp").Length);
    }
}
