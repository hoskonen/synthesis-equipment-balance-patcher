namespace EquipmentBalancePatcher;

public sealed class Settings
{
    // Relative paths resolve against Synthesis's patcher-specific settings data folder.
    public string OutputDirectory { get; set; } = "Reports";
    public int MaximumTemplateDepth { get; set; } = TemplateResolver.DefaultMaximumDepth;
    public bool EnableBalanceAudit { get; set; } = true;
    public bool EnableReferenceComparison { get; set; } = true;
    public int MinimumIndependentPeers { get; set; } = 5;
    public double RobustDeviationThreshold { get; set; } = 3.5;
    public bool IncludeEnchantedAnalysisCandidates { get; set; } = true;
    public bool EnableMaterialProgressionReport { get; set; } = true;
    public BalanceThresholds BalanceThresholds { get; set; } = new();
}

public sealed record InputPlugin(string Plugin, bool Enabled);
public sealed record ReferenceSnapshot(string FormKey, string? EditorId, string Status);
public sealed record Diagnostic(string Code, string Message)
{
    public string Severity { get; init; } = "Warning";
    public string Relevance { get; init; } = "Classification";
}

public sealed record EquipmentSnapshot
{
    public required string FormKey { get; init; }
    public string? EditorId { get; init; }
    public string? Name { get; init; }
    public required string DefiningPlugin { get; init; }
    public required string WinningPlugin { get; init; }
    public required string RecordType { get; init; }
    public string StatSemantics { get; init; } = "StoredRecord";
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
    string Status, string[] Evidence, string[] ReasonCodes)
{
    public string? MaterialFamily { get; init; }
    public string? MaterialVariant { get; init; }
    public string Scope { get; init; } = "OrdinaryEquipment";
    public string[] ScopeReasons { get; init; } = [];
}
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
public sealed record ClassificationIssue(string FormKey, string RecordType, string Code, string Message)
{
    public string Severity { get; init; } = "Warning";
    public string Relevance { get; init; } = "Classification";
}
public sealed record KeywordExample(string FormKey, string? EditorId, string RecordType);
public sealed record KeywordSummary(string FormKey, string[] EditorIds, string[] ResolutionStatuses,
    string DiagnosticCategory, bool PotentiallyRelevant, int AffectedRecords, string[] RecordTypes,
    string[] OriginPlugins, string[] WinningPlugins, KeywordExample[] Examples);
public sealed record EligibilityReasonSummary(string Stage, string RecordType, string Reason,
    string ReasonCategory, int AffectedRecords);
public sealed record AuditReport(string SchemaVersion, string PatcherVersion, string ClassificationVersion,
    string MutagenVersion, string SynthesisVersion, string NameLanguage, Settings Settings,
    InputPlugin[] InputLoadOrder, InventorySummary Summary, AuditedEquipment[] Equipment,
    GroupStatistics[] Groups, ClassificationIssue[] ClassificationDiagnostics)
{
    public KeywordSummary[] KeywordSummary { get; init; } = [];
    public EligibilityReasonSummary[] EligibilitySummary { get; init; } = [];
    public string TemplatePolicyVersion { get; init; } = EffectiveEquipmentInvestigator.PolicyVersion;
    public EffectiveEquipment[] TemplateAnalysis { get; init; } = [];
    public TemplateRootSummary[] TemplateRoots { get; init; } = [];
    public BalanceAnalysis? BalanceAnalysis { get; init; }
    public ReferenceAnalysis? ReferenceAnalysis { get; init; }
    public WeaponEvidenceAnalysis? WeaponEvidenceAnalysis { get; init; }
}
