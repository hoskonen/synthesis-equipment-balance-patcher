# v0.1c template-resolution prototype

The subsequent [v0.1d semantics validation review](TemplateSemanticsValidation.md) separates confirmed storage/traversal behavior from runtime hypotheses and specifies the manual tests required before production inheritance. This historical prototype and its Unvalidated policies remain unchanged.

## Purpose and boundaries

The long-term goal is relative consistency among comparable equipment within the user's load order: reviewed material-tier progression, intentional exceptions and independent equipment designs remain meaningful. Classification establishes comparability; future balance decisions are separate. Runtime combat difficulty, armor damage-reduction formulas and global vanilla scaling tables are outside this prototype.

v0.1c adds read-only traversal and proposed-field investigation. It creates no WEAP/ARMO overrides, plugins, statistical balance baselines or anomaly rules. Production inventory, classifications, eligibility and descriptive groups still use the same raw winning snapshots as v0.1b. Templated equipment remains ineligible in those production results.

## Traversal

`TemplateResolver` indexes the complete once-extracted winning WEAP/ARMO snapshots (including deleted winners) from the Synthesis state. Parent lookup therefore uses the same winning load order and preceding pipeline result as inventory. Production additionally probes an absent target in `state.LinkCache` to distinguish a non-equipment target from an unresolved link, without editing the snapshot. Offline replay has only its stored snapshots; an absent non-equipment target may consequently be reported UnresolvedParent rather than WrongTypeParent.

Traversal and field interpretation are independent components. A chain stores each successfully looked-up equipment FormKey, EditorID, winner and child-relative depth once. The direct parent is the child's link; the root is the terminal nontemplated same-type winner and is populated only for a successful templated chain. Failure targets are separate, including unresolved/wrong-type or revisited keys; they do not masquerade as effective roots. NoTemplate has depth zero and no parent/root.

Statuses: NoTemplate, Resolved, SelfReference, Cycle, MissingParent, UnresolvedParent, DeletedParent, WrongTypeParent and MaximumDepthExceeded. A deleted parent may appear in the visited trace but is never used as a value source. `MaximumTemplateDepth` defaults to **128 parent edges**, excludes the child, and must be positive. Exactly 128 edges ending at a root succeed; another required edge fails. Cancellation is checked at entry, every traversal edge, during field/scenario work and before caching. Completed successes and failures are cached per child for this load order; canceled traversals are not cached. Cached suffixes are not reused with a different depth budget. Complexity is bounded by records × maximum depth; the real reference dataset has at most two edges.

## Proposed fields and provenance

Every candidate field has raw child JSON, proposed JSON, source FormKey/winning plugin, versioned rule ID, resolution status, source depth, validation status and nullable valuesDiffer. Chains are serialized once per analysis entry, not copied into each field. Failures leave inherited proposals/source/depth unavailable and retain raw data. Existing metric extraction's nonfinite handling remains in force.

| Policy | Fields | Interpretation |
| --- | --- | --- |
| `prototype-1/root-replacement/…` | WEAP damage, weight, speed, reach, critical damage, stagger, animation, skill, equip type, keywords; ARMO rating, weight, armor type, slots, equip type, keywords | Use stored terminal-root value as a proposed inheritance rule. Every such value in a templated chain remains **Unvalidated**. |
| `prototype-1/child-local/…` | Name, value, enchantment, charge, scripts/VMAD, playability, flags and shield flag | Preserve stored child values explicitly, including zero and null. Name is identity data (StoredOnly). Other templated field policies remain Unvalidated; preserving raw flags/scripts does not establish their effective engine semantics. |

Resolution status distinguishes Stored, ProposedInherited and Unresolved. Validation status distinguishes StoredOnly from Unvalidated. No field is emitted as ValidatedInherited: synthetic traversal/serialization tests and report examples do not prove engine inheritance. The data model can carry that status in a later reviewed policy, but v0.1c has no validation promotion mechanism. Missing numeric/structural root values and unresolved keyword/equip-type references remain Unresolved; an empty keyword list, absent equip-type link and nullable skill may be explicit proposed values rather than zero/default fallbacks. Local null enchantment/name and zero value/charge are retained exactly.

## Hypothetical classification

Structured JSON reports the current classification/eligibility next to three **hypothetical** classifications:

1. RootReplacement uses the terminal root's keywords.
2. ChildKeywords preserves the original child's keyword set while proposing root structural/stat fields.
3. ChainKeywordUnion combines all visited keyword sets, preserving distinct resolution states and deterministic ordering.

These alternatives expose uncertainty; none is chosen as an engine rule. Each reports category/material/status/scope, changed flags and possible future eligibility. The isolated hypothetical snapshot removes the TemplateNotEvaluated gate solely for investigation. It conservatively carries scripts, special flags, invalid extraction evidence and nonplayability from every chain member, and prevents root keyword replacement from erasing artifact exclusions. Enchantment remains child-local under the proposed policy, so enchanted children stay excluded from hypothetical ordinary baselines. All scenarios carry requiredValidation gates for inherited fields, keywords, scripts/flags and root sampling. A hypothetical true eligibility flag is **not permission to include the record** in production populations.

Chain failures produce no hypothetical classification. Other failures (unknown/conflicting material, custom polearm hints, unsupported armor slots, Staff animation and special behavior) can still prevent classification or future eligibility after a successful traversal. The prototype never rewrites production classifications or their reason arrays.

## Baseline integrity proposal

`templateRoots` groups successfully resolved templated children by record type and terminal root. It provides root winner, child FormKeys, enchanted-child count and each child's defining/winning plugin populations. Its samplingStatus explicitly says no baseline was created.

For a future validated baseline, start with one reviewed independent design per `(recordType, root FormKey, category, specific material tier)`, not one sample per enchanted child. Use the winning root as the initial design representative, retain per-origin/per-winner provenance, and keep enchanted children out of ordinary unenchanted populations. Do not deduplicate unrelated nontemplated designs merely because their numerical stats match. Local name, value or enchantment changes alone need not create a new statistical design; meaningful intentional variants require an explicit reviewed exception or separate design identity, preserving their child provenance and effective-field differences. A shared root is a warning of possible sampling bias, not proof all its children are equivalent. Cyclic/missing/deleted/wrong-type chains remain excluded. Actual weights, reference selections and exception rules require later validation; this milestone calculates none.

## Reference replay findings

The original audit remains unchanged. Offline replay into `Reports-v0.1c/` resolves all **6,095** templated snapshots: 6,092 one-edge chains and three two-edge chains. There are **283** terminal-root families; **267** have multiple children. Some CC staff roots have 49 enchanted variants apiece, illustrating why record-count sampling could overweight one design. **724** children have different stored child/root keyword sets. These findings describe the supplied snapshots, not runtime behavior.

| Inspection target | Observed prototype result |
| --- | --- |
| `0008B9:RiverwoodBandits.esp` (RWBAlfinDaggerFrost) | Raw damage 8; proposed 7 from `01399E:Skyrim.esm`; Unvalidated. Dagger/Elven classification remains identifiable. Child enchantment excludes a hypothetical ordinary baseline. |
| `015CB9:Dawnguard.esm` | Raw rating 25; proposed 28 from `0142C7:Dawnguard.esm`; Unvalidated. Vampire/Leather material conflict remains unresolved. |
| `000A7D:PrvtI_ArgonianWeapons.esp` | Child adds MagicDisallowEnchanting; replacement drops it, preservation/union retain it. Custom spear hints and unreviewed Argonian material still prevent ordinary classification/eligibility. |
| `00081F:ccBGSSSE066-Staves.esl` | Successful chain to `000809:ccBGSSSE066-Staves.esl`; Staff animation stays ExpectedOutOfScope in all scenarios. |
| RFM | The dataset's RFM records have no direct templates and receive no template-analysis entry. They remain independent classification/future-balance inspection targets. |

| Hypothesis (6,095 templated children) | Changed category | Changed material | Potential statistics candidates | Potential unenchanted baseline candidates |
| --- | ---: | ---: | ---: | ---: |
| RootReplacement | 29 | 45 | 5,076 | 19 |
| ChildKeywords | 0 | 0 | 5,058 | 18 |
| ChainKeywordUnion | 0 | 45 | 5,050 | 19 |

These are overlapping alternative scenarios, not additive populations or validated candidates. Production counts remain v0.1b's **699 baseline eligible, 1,202 statistics eligible**, and all four existing CSV contents match that replay byte-for-byte. Classification version stays 2. Successful chains clearly cannot substitute for classification/engine validation.

## Reporting and validation

Schema **3**, patcher **0.1c**, templatePolicyVersion **prototype-1** add `templateAnalysis` and `templateRoots` to JSON. Existing inventory/group/issue/keyword CSV schemas are unchanged. `template-analysis.csv` contains one row per templated child and candidate field, with child identity, direct/root parent, chain status/depth, raw/proposed JSON values, field source, rule/status/depth, nullable difference, validation status and resolution diagnostics. Hypothetical comparisons and full visited traces are in JSON rather than repeated as large CSV structures. With no templates, the CSV still has its header. There are six report files. Local reports/replays are ignored by Git; they are large inspection artifacts.

```powershell
dotnet run --project tools/ReplayAudit/ReplayAudit.csproj -- Reports/audit.json Reports-v0.1c
```

Replaying stored snapshots cannot prove installed GUI completion or engine field rules. Synthetic tests cover nontemplated/one-level/multilevel/shared chains, actual parent overrides, self/multirecord cycles, missing/unresolved/deleted/wrong-type links, depth/cancellation, raw-versus-proposed stats, local zero/null/enchantment/scripts, keyword alternatives, deterministic reports and unchanged production decisions. Existing Synthesis pipeline/subprocess tests now include a template child and verify six reports, code 0 and plugin byte/write-time protection.

For real validation: run under the normal mod-manager environment into a new directory; check root winners/traces against xEdit, compare the four cases above, verify Unvalidated markers and unchanged production eligibility, and rerun for byte-identical outputs. Keep the [Synthesis completion limitation/workaround](SynthesisCompletion.md) in mind. Controlled CK/runtime tests must still settle keyword replacement versus augmentation, scripts/flags, nullable local values and multilayer inheritance before any rule is marked validated. The next step is field-rule validation and reviewed reference-design sampling; balancing and anomaly detection remain future work.
