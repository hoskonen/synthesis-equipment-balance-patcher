namespace EquipmentBalancePatcher;

public sealed class Settings
{
    // Relative paths resolve against Synthesis's patcher-specific settings data folder.
    public string OutputDirectory { get; set; } = "Reports";
}

public sealed record InputPlugin(string Plugin, bool Enabled);
public sealed record ReferenceSnapshot(string FormKey, string? EditorId, string Status);
public sealed record Diagnostic(string Code, string Message);

public sealed record EquipmentSnapshot
{
    public required string FormKey { get; init; }
    public string? EditorId { get; init; }
    public string? Name { get; init; }
    public required string DefiningPlugin { get; init; }
    public required string WinningPlugin { get; init; }
    public required string RecordType { get; init; }
    public bool Deleted { get; init; }
    public bool? Playable { get; init; }
    public ReferenceSnapshot[] Keywords { get; init; } = [];
    public ReferenceSnapshot? Enchantment { get; init; }
    public ushort? EnchantmentAmount { get; init; }
    public bool HasScripts { get; init; }
    public bool HasVirtualMachineAdapter { get; init; }
    public ReferenceSnapshot? Template { get; init; }
    public ReferenceSnapshot? EquipmentType { get; init; }
    public string[] Flags { get; init; } = [];
    public string? AnimationType { get; init; }
    public string? Skill { get; init; }
    public double? Damage { get; init; }
    public double? Speed { get; init; }
    public double? Reach { get; init; }
    public double? CriticalDamage { get; init; }
    public double? Stagger { get; init; }
    public double? ArmorRating { get; init; }
    public string? ArmorType { get; init; }
    public uint? SlotMask { get; init; }
    public bool? ShieldFlag { get; init; }
    public double? Weight { get; init; }
    public double? Value { get; init; }
    public Diagnostic[] ExtractionDiagnostics { get; init; } = [];
}

public sealed record Classification(string? Category, string? Material, string? Handedness,
    string Status, string[] Evidence, string[] ReasonCodes);
public sealed record Eligibility(bool InventoryStatisticsEligible, bool BaselineEligible,
    bool AnalysisEligible, string[] InventoryStatisticsReasons, string[] BaselineReasons,
    string[] AnalysisReasons);
public sealed record AuditedEquipment(EquipmentSnapshot Snapshot, Classification Classification,
    Eligibility Eligibility, Diagnostic[] Diagnostics);
public sealed record MetricStatistics(int AvailableCount, int MissingCount, double? Minimum,
    double? Maximum, double? Mean, double? Median);
public sealed record GroupStatistics(string RecordType, string Category, string? ArmorType,
    string Material, bool Enchanted, int Count, int BaselineEligibleCount,
    int BaselineExcludedCount, SortedDictionary<string, MetricStatistics> Metrics);
public sealed record InventorySummary(int Weapons, int Armor, int Deleted, int Confident,
    int Partial, int Unclassified, int Templated, int Enchanted, int Scripted,
    int BaselineEligible, int BaselineExcluded, int StatisticsEligible, int UnresolvedReferences,
    SortedDictionary<string, int> Categories, SortedDictionary<string, int> Materials);
public sealed record ClassificationIssue(string FormKey, string RecordType, string Code, string Message);
public sealed record AuditReport(string SchemaVersion, string PatcherVersion, string ClassificationVersion,
    string MutagenVersion, string SynthesisVersion, string NameLanguage, Settings Settings,
    InputPlugin[] InputLoadOrder, InventorySummary Summary, AuditedEquipment[] Equipment,
    GroupStatistics[] Groups, ClassificationIssue[] ClassificationDiagnostics);
