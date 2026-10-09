namespace EquipmentBalancePatcher;

public sealed record TemplateHop(string FormKey, string? EditorId, string WinningPlugin, int Depth);
public sealed record TemplateChain(string ChildFormKey, string RecordType, string? DirectParent,
    string? RootParent, int Depth, string Status, TemplateHop[] Visited, string? FailureTarget, Diagnostic[] Diagnostics);

// The source is the complete, once-extracted winning WEAP/ARMO inventory, including deleted winners.
// It is scoped to one load order. No inheritance-field interpretation happens in this component.
public sealed class TemplateResolver
{
    public const int DefaultMaximumDepth = 128; // Maximum parent edges, excluding the child.
    private readonly Dictionary<string, EquipmentSnapshot> _records;
    private readonly Dictionary<string, TemplateChain> _completed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, string?>? _missingTargetStatus;
    public int MaximumDepth { get; }

    public TemplateResolver(IEnumerable<EquipmentSnapshot> winners, int maximumDepth = DefaultMaximumDepth,
        Func<string, string?>? missingTargetStatus = null)
    {
        if (maximumDepth < 1) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        MaximumDepth = maximumDepth;
        _records = winners.ToDictionary(s => s.FormKey, StringComparer.OrdinalIgnoreCase);
        _missingTargetStatus = missingTargetStatus;
    }

    public EquipmentSnapshot GetRecord(string formKey) => _records[formKey];

    public TemplateChain Resolve(string childFormKey, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        if (_completed.TryGetValue(childFormKey, out var completed)) return completed;
        var child = GetRecord(childFormKey);
        if (child.RecordType is not ("WEAP" or "ARMO")) throw new ArgumentException("Only equipment records are supported.");
        var visited = new List<TemplateHop> { new(child.FormKey, child.EditorId, child.WinningPlugin, 0) };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { child.FormKey };
        var current = child;
        string? failure = null;
        string status = child.Template is null ? "NoTemplate" : "Resolved";
        while (current.Template is { } link)
        {
            cancel.ThrowIfCancellationRequested();
            failure = link.FormKey;
            if (seen.Contains(link.FormKey)) { status = string.Equals(link.FormKey, current.FormKey, StringComparison.OrdinalIgnoreCase) ? "SelfReference" : "Cycle"; break; }
            if (visited.Count - 1 >= MaximumDepth) { status = "MaximumDepthExceeded"; break; }
            if (!_records.TryGetValue(link.FormKey, out var parent))
            {
                status = _missingTargetStatus?.Invoke(link.FormKey) ?? (link.Status == "Resolved" ? "MissingParent" : "UnresolvedParent");
                break;
            }
            if (parent.RecordType != child.RecordType) { status = "WrongTypeParent"; break; }
            // Record the visited deleted target, but never use it as an effective root.
            seen.Add(parent.FormKey);
            visited.Add(new(parent.FormKey, parent.EditorId, parent.WinningPlugin, visited.Count));
            if (parent.Deleted) { status = "DeletedParent"; break; }
            current = parent;
            failure = null;
        }
        var diagnostics = status is "Resolved" or "NoTemplate" ? Array.Empty<Diagnostic>()
            : [new Diagnostic("Template" + status, $"{status} at {failure}; no inherited values are proposed.")
                { Relevance = "Template", Severity = "Warning" }];
        var result = new TemplateChain(child.FormKey, child.RecordType, child.Template?.FormKey,
            status == "Resolved" ? current.FormKey : null, visited.Count - 1, status,
            visited.ToArray(), failure, diagnostics);
        cancel.ThrowIfCancellationRequested();
        // Cache only completed traversals (including explicit failures); cancellation never publishes a result.
        // Depth is child-relative; a cached suffix is deliberately not reused with a different remaining budget.
        _completed.Add(child.FormKey, result);
        return result;
    }
}
