using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Strings;

namespace EquipmentBalancePatcher;

public sealed class EquipmentReader(ILinkCache linkCache)
{
    private readonly Dictionary<FormKey, ReferenceSnapshot> _keywords = [];

    public EquipmentSnapshot[] Read(IEnumerable<(IWeaponGetter Record, ModKey Winner)> weapons,
        IEnumerable<(IArmorGetter Record, ModKey Winner)> armor, CancellationToken cancel = default)
    {
        var results = new List<EquipmentSnapshot>();
        foreach (var (record, winner) in weapons)
        {
            cancel.ThrowIfCancellationRequested();
            results.Add(ReadWeapon(record, winner));
        }
        foreach (var (record, winner) in armor)
        {
            cancel.ThrowIfCancellationRequested();
            results.Add(ReadArmor(record, winner));
        }
        return results.ToArray();
    }

    private ReferenceSnapshot Resolve<T>(FormKey key) where T : class, IMajorRecordGetter
    {
        return linkCache.TryResolve<T>(key, out var record)
            ? new(key.ToString(), record.EditorID, record.IsDeleted ? "DeletedTarget" : "Resolved")
            : new(key.ToString(), null, "Unresolved");
    }

    private ReferenceSnapshot? Reference<T>(IFormLinkGetter<T> link) where T : class, IMajorRecordGetter =>
        link.IsNull ? null : Resolve<T>(link.FormKey);

    private ReferenceSnapshot[] Keywords(IReadOnlyList<IFormLinkGetter<IKeywordGetter>>? keywords) =>
        (keywords ?? []).Select(link =>
        {
            if (!_keywords.TryGetValue(link.FormKey, out var snapshot))
            {
                snapshot = Resolve<IKeywordGetter>(link.FormKey);
                _keywords.Add(link.FormKey, snapshot);
            }
            return snapshot;
        }).DistinctBy(keyword => keyword.FormKey, StringComparer.OrdinalIgnoreCase)
          .OrderBy(keyword => keyword.FormKey, StringComparer.Ordinal).ToArray();

    private static string? Name(ITranslatedStringGetter? name) =>
        name is not null && name.TryLookup(Language.English, out var text) ? text : null;

    private static double? Finite(double? value, string metric, List<Diagnostic> diagnostics)
    {
        if (value.HasValue && !double.IsFinite(value.Value))
        {
            diagnostics.Add(new("NonFiniteMetric", $"{metric} contains a nonfinite value; reported as unavailable."));
            return null;
        }
        if (value < 0) diagnostics.Add(new("NegativeMetric", $"{metric} has a negative stored value."));
        return value;
    }

    public EquipmentSnapshot ReadWeapon(IWeaponGetter record, ModKey winner)
    {
        var diagnostics = new List<Diagnostic>();
        if (!record.IsDeleted && record.BasicStats is null)
            diagnostics.Add(new("MissingBasicStats", "Weapon DATA/basic stats subrecord is absent."));
        if (!record.IsDeleted && record.Data is null)
            diagnostics.Add(new("MissingWeaponData", "Weapon DNAM/animation data subrecord is absent."));
        return new()
        {
            FormKey = record.FormKey.ToString(), EditorId = record.EditorID, Name = Name(record.Name),
            RecordType = "WEAP", DefiningPlugin = record.FormKey.ModKey.ToString(), WinningPlugin = winner.ToString(),
            Deleted = record.IsDeleted,
            Playable = !record.MajorFlags.HasFlag(Weapon.MajorFlag.NonPlayable)
                && !(record.Data?.Flags.HasFlag(WeaponData.Flag.NonPlayable) ?? false),
            Keywords = Keywords(record.Keywords), Enchantment = Reference(record.ObjectEffect),
            EnchantmentAmount = record.EnchantmentAmount,
            HasScripts = record.VirtualMachineAdapter?.Scripts.Count > 0,
            HasVirtualMachineAdapter = record.VirtualMachineAdapter is not null,
            Template = Reference(record.Template), EquipmentType = Reference(record.EquipmentType),
            Flags = new[] { "Major:" + record.MajorFlags, "Data:" + (record.Data?.Flags.ToString() ?? "Unavailable") },
            AnimationType = record.Data?.AnimationType.ToString(), Skill = record.Data?.Skill?.ToString(),
            Damage = record.BasicStats?.Damage,
            Speed = Finite(record.Data?.Speed, "Speed", diagnostics),
            Reach = Finite(record.Data?.Reach, "Reach", diagnostics),
            Weight = Finite(record.BasicStats?.Weight, "Weight", diagnostics), Value = record.BasicStats?.Value,
            CriticalDamage = record.Critical?.Damage, Stagger = Finite(record.Data?.Stagger, "Stagger", diagnostics),
            ExtractionDiagnostics = diagnostics.ToArray()
        };
    }

    public EquipmentSnapshot ReadArmor(IArmorGetter record, ModKey winner)
    {
        var diagnostics = new List<Diagnostic>();
        if (!record.IsDeleted && record.BodyTemplate is null)
            diagnostics.Add(new("MissingBodyTemplate", "Armor body template is absent."));
        return new()
        {
            FormKey = record.FormKey.ToString(), EditorId = record.EditorID, Name = Name(record.Name),
            RecordType = "ARMO", DefiningPlugin = record.FormKey.ModKey.ToString(), WinningPlugin = winner.ToString(),
            Deleted = record.IsDeleted,
            Playable = !record.MajorFlags.HasFlag(Armor.MajorFlag.NonPlayable)
                && !(record.BodyTemplate?.Flags.HasFlag(BodyTemplate.Flag.NonPlayable) ?? false),
            Keywords = Keywords(record.Keywords), Enchantment = Reference(record.ObjectEffect),
            EnchantmentAmount = record.EnchantmentAmount,
            HasScripts = record.VirtualMachineAdapter?.Scripts.Count > 0,
            HasVirtualMachineAdapter = record.VirtualMachineAdapter is not null,
            Template = Reference(record.TemplateArmor), EquipmentType = Reference(record.EquipmentType),
            Flags = new[] { "Major:" + record.MajorFlags, "Body:" + (record.BodyTemplate?.Flags.ToString() ?? "Unavailable") },
            ArmorType = record.BodyTemplate?.ArmorType.ToString(),
            SlotMask = record.BodyTemplate is null ? null : (uint)record.BodyTemplate.FirstPersonFlags,
            ShieldFlag = record.MajorFlags.HasFlag(Armor.MajorFlag.Shield),
            ArmorRating = Finite(record.ArmorRating, "ArmorRating", diagnostics),
            Weight = Finite(record.Weight, "Weight", diagnostics), Value = record.Value,
            ExtractionDiagnostics = diagnostics.ToArray()
        };
    }
}
