# Ordinary weapon evidence audit — v0.1g

This independent, read-only prototype asks whether reviewed material keywords and a small English name vocabulary make ordinary weapon comparisons easier to trust. It reuses winning WEAP snapshots, the existing keyword catalog and v0.1f reference results. It adds no classifier mappings, numerical algorithm, eligibility promotion, inherited values or game-record writes.

## Scope and confidence

The inspection covers existing Sword, Dagger, WarAxe, Mace, Greatsword, Battleaxe, Warhammer and Bow categories. Crossbows are deferred. Existing structural conflicts, custom subtype hints, staff animation, scripts, bound/special flags, nonplayability, deletion, missing structure, unresolved links and templates prevent ordinary inspection. The report still includes their raw evidence and explicit exclusions in one row per weapon.

Material-only classification uncertainty is allowed **for inspection**, so missing or contradictory keywords can be described instead of silently omitted. `AnalysisEligible` and `BaselineEligible` are never changed. Such inspection does not authorize a reference match: reference reuse still requires existing confident classification and analysis eligibility. The 31 inspected records that fail existing analysis eligibility retain that failure visibly.

Clear custom-name words (spear, halberd, rapier, pike, quarterstaff, polearm, trident, glaive, scythe), utility/test name hints (fishing rods/knives, pickaxes, woodcutter's axe, exact Knife/Fork/Test Vorpal Sword), and a bounded explicit list of 38 named-design hints suppress ordinary inspection **only in this report**. The list includes named designs seen in the supplied inventory, such as Blade of Woe, Dragonbane, Nightingale weapons, Zephyr, Chillrend and Miraak's Sword. Matching is whole-word for phrases and exact case-insensitive trimmed name for named designs. The executable list is in `WeaponEvidenceAudit.cs`; it is not a new production special-weapon category or proof of artifact status. Possessive names or every unusual name are not automatically excluded. English names may be renamed or unavailable, and unknown special/custom behavior may escape these hints. No claim of comprehensive unique-weapon detection is made.

## Material and name evidence

Reviewed keyword identities come directly from `KeywordCatalog`, by FormKey and `WeaponMaterial` kind; EditorID text cannot establish identity. The report retains keyword identities, resolution statuses and all reviewed specific tiers, including those outside the nine-word name vocabulary. Only resolved identities contribute a material tier. Multiple distinct tiers are reported, never reduced to one by names or stats. Missing and unresolved evidence remain explicit. Material identity does not prove special damage behavior, smithing functionality or author intent.

The nine initial name words correspond to these existing keyword identities:

| Name word / material | Keyword FormKey |
| --- | --- |
| Iron | 01E718:Skyrim.esm |
| Steel | 01E719:Skyrim.esm |
| Elven | 01E71B:Skyrim.esm |
| Dwarven | 01E71A:Skyrim.esm |
| Orcish | 01E71C:Skyrim.esm |
| Glass | 01E71D:Skyrim.esm |
| Ebony | 01E71E:Skyrim.esm |
| Daedric | 01E71F:Skyrim.esm |
| Dragonbone | 019822:Dawnguard.esm |

The catalog's original identity review is documented in [AuditReview.md](AuditReview.md). Names use culture-invariant case-insensitive matching, bounded by Unicode letters, numbers, combining marks and underscore. Ironwood, Steelheart, Glasswing, Ebony2 and Steel_Sword do not match material words. Spaces, hyphens and punctuation separate words. Repeated mentions count once; two different material words are ambiguous. No material is inferred from damage, weight, models or EditorIDs.

`NameAgreement` distinguishes AgreesWithKeyword, ConflictsWithKeyword, NameOnlyEvidence and NoMaterialEvidence, with two explicit diagnostic states: KeywordOnlyEvidence and AmbiguousNameEvidence. The latter states avoid pretending a keyword-only record has no material evidence or that an ambiguous name establishes a contradiction. Multiple keyword tiers require review even when one matches the name. A phrase such as “Steel's Legacy” supplies weak word evidence, not proof that the object is ordinary or made of Steel.

| Evidence status | Meaning |
| --- | --- |
| ConsistentEvidence | One reviewed keyword tier and one recognized name material agree. Agreement is weak corroboration, not validated gameplay behavior. |
| ConflictingEvidence | Reviewed keyword tiers conflict, or a single recognized name material disagrees with the single keyword tier. Review is required; no correction follows. |
| IncompleteEvidence | Only one source, no recognized evidence, or multiple name materials. Incomplete does not mean suspicious. |
| UnsupportedSpecialWeapon | Outside this report's ordinary scope, including deferred or unsupported records. Reasons explain the specific restriction; the label does not prove every record is an artifact. |

Confidence is Unsupported, ReviewRequired, Incomplete or KeywordSupported_NameWeak. Nine inspected records retain a known keyword tier but unreviewed additional material evidence, so their existing classification remains partial. Name agreement alone cannot promote them. Numerical `referenceStatus` and individual metric results remain separate from all consistency statuses.

## Crafting investigation and deferral

The smallest candidate extraction was inspected against the pinned Mutagen 0.54.4 source, commit `0188012c607ce8bb283d2704400d37737f089134`. Its [COBJ schema](https://github.com/Mutagen-Modding/Mutagen/blob/0188012c607ce8bb283d2704400d37737f089134/Mutagen.Bethesda.Skyrim/Records/Major%20Records/ConstructibleObject.xml) exposes Items, Conditions, CreatedObject, WorkbenchKeyword and CreatedObjectCount.

A narrow future collector could enumerate winning COBJ once, retain recipes whose target resolves to an inspected weapon, and capture recipe/winner identity, ingredient identities/counts, station identity and explicitly recognized perk conditions. Multiple recipes must remain separate. Station identities must be reviewed to distinguish crafting from tempering; no arbitrary condition evaluation, ingredient-to-material conversion or crafting compatibility patcher is needed. Ingredients or smithing perks would provide supporting evidence only.

The current extractor and replay model contain only WEAP/ARMO snapshots, not COBJ, ingredients, recipe targets, stations or conditions. None of the available audit datasets supplies recipe evidence. Adding a live collector, immutable recipe storage, target/condition validation and integration fixtures would extend the input pipeline, while still providing no recipe measurements from this dataset. It is disproportionate to this small **existing-dataset** consistency experiment, so extraction is deferred. Every row and JSON summary says `Deferred_NotExtracted`, with `CraftingEvidenceNotExtracted_NotNoRecipe`. This does not mean the weapons lack recipes. Whether crafting materially improves confidence remains **unmeasured**, not a negative result or an asserted gain.

## Report and replay results

Schema **6**, executable **0.1g**, adds only `weapon-evidence.csv` and `weaponEvidenceAnalysis` policy/summary in JSON. The tenth output CSV contains **4,422 data rows**, one per winning weapon, including exclusions. Identity, defining/winning plugin, existing subtype/animation/flags/scripts/template, material sources, name tokens, confidence, disagreements, existing eligibility, scope reasons, crafting deferral and optional reference key/name/winner/metric results are retained. Metric arrays are compact JSON cells; no extra recipe or evidence files are produced. JSON omits duplicate per-weapon rows because inventory and the CSV already contain them. CSV size is approximately 2.95 MB.

Replay used the original `Reports/audit.json` and wrote to `Reports-v0.1g`. This is the historical 9,095-record dataset, not a fresh game/profile run. Existing v0.1f reports were retained separately.

| Evidence coverage | Count |
| --- | ---: |
| Winning weapons / inspected / outside scope | 4,422 / 391 / 4,031 |
| Existing analysis-eligible among inspected | 360 |
| Inspected with one reviewed material tier | 369 (94.4%) |
| Keyword/name agreement | 86 (22.0% of inspected) |
| Ordinary conflicting evidence / ambiguous names | 0 / 0 |
| Keyword-only / name-only | 283 / 0 |
| Neither reviewed keyword nor recognized name word | 22 |
| Reference available, including self-references | 68 |
| Self-references / other comparable weapons | 16 / 52 |
| ReferenceDeviation / WithinTolerance, non-reference | 49 / 3 |
| NoReference / NotEligibleForReference | 292 / 31 |

The ordinary population includes 74 swords, 43 daggers, 54 war axes, 43 maces, 52 greatswords, 42 battleaxes, 40 warhammers and 43 bows. The 31 ineligible inspection records comprise 22 missing reviewed material and nine with additional unreviewed material evidence. Most of the missing-evidence records have unavailable names; two SRE Golden weapons and AlternatePerspective's Kawoosh are also incomplete. None is promoted or called erroneous. The remaining 283 keyword-only records may simply use names without one of the nine vocabulary words, including tiers such as Nordic and descriptive styles such as Golden.

Relevant exclusion counts overlap: templates 3,423; unsupported subtype 553; staff animation 504; SpecialEquipment 437; nonplayable/unknown 118; scripts 74; named-design hints 71; deferred crossbows 36; custom-name hints 22; utility/test-name hints 17; bound weapons 10. Structural conflicts and other reasons are retained in the JSON summary. These are not disjoint categories and must not be summed.

## Review cases and RFM

No ordinary keyword/name disagreement was found, so this dataset supplies **no confirmed actionable material inconsistency**. It does contain nine raw keyword/name conflicts, all outside ordinary scope: six RFM records and three Skyrim records. The raw evidence remains visible without treating excluded weapons as ordinary candidates:

- `026CC0:RFM.esm`, Iron Battle Pitchfork: Steel keyword versus Iron name, staff animation and special behavior. Material cannot turn it into an ordinary battleaxe or staff.
- `030F1D` and `030F1E:RFM.esm`, Steel weapons of the Watch: both Silver and Steel keyword tiers, with scripts and special behavior. The Silver keyword may serve a special effect; no automatic keyword removal is warranted.
- `030F20`, `030F23` and `030F24:RFM.esm`, Dwarven/Steel Caster Poleswords: Ebony keyword versus name word, with special behavior. This is a useful author/recipe inspection question, not proof that the material is wrong.
- `04A38F` and `0EA29C:Skyrim.esm`, Ebony Blade: Daedric keyword versus Ebony name, scripted/special. The name of an artifact need not be its material classification.
- `017288:Skyrim.esm`, Steel Greatsword of Scorching: Daedric keyword versus Steel name, nonplayable and special. Its context must be checked before any repair discussion.

RFM retains exactly **five** ordinary candidates and **297** excluded weapons. Ebony Curved Sword and the two Glass swords have matching keyword/name words. Nordic Straight Sword and Worn Atmora Sword are keyword-only under this small vocabulary. Only Ebony Curved Sword has a reviewed reference: CriticalDamage 7 versus winning Ebony Sword 5, +2 / +40%, while Damage 14 versus 13 is within tolerance. The other four have NoReference. **137 excluded RFM weapons have agreeing words and keywords**, demonstrating why agreement cannot override special flags, scripts, staff animation or custom behavior. Thirteen excluded RFM records have multiple material tiers. No RFM weapon was admitted into a baseline. The available datasets still lack `RFM - Balance Patch.esp`.

Numerical review remains descriptive and uses v0.1f values/thresholds unchanged. Examples within the narrowed view include Ebony-tier Golden Bow (`08429A:SRE.esp`) Damage 9 versus 17, −47.1%; Ebony-tier Golden Dagger (`08429B:SRE.esp`) Damage 5 versus 10, −50%; and Iron-tier Golden Sword (`023F14:SRE.esp`) Weight 15 versus 9, +66.7%. “Golden” is outside the material vocabulary, so these are keyword-supported numerical differences, not name conflicts. Heart Sentinel Sword (`0A5A16:HeartOfTheReach.esp`) Damage 13 versus Steel Sword 8, +62.5%, retains its structural review warning. Author intent, enchantment and custom behavior can still explain differences.

## Validation and assessment

Build succeeded; all **112 automated cases passed**, including 15 added evidence cases. Coverage includes keyword identity independent of EditorID, agreement/conflict, missing keywords/name-only evidence, ambiguous/possessive names, substring/Unicode boundaries, conflicting tiers, all eight subtypes, custom/staff/special/utility/template/structural exclusions, compact summaries, deterministic culture/order, untouched prior components and report-only output. Existing pipeline/executable tests preserve input/output plugin bytes and timestamps for absent/existing outputs; sentinel tests permit exactly ten reports. `NoPatch = true` and the original record-extraction path remain unchanged.

All **ten report hashes matched across repeat replays**. All eight prior CSVs match v0.1f byte-for-byte. Every prior JSON property except the intentional schema/patcher version change is unchanged, including settings, raw equipment/classification/eligibility, template investigation, entire statistical analysis and entire reference analysis. The source hash remains `A1224A661EAF8365FF6E687396325A03D95BDAE9DBC2642DF22A37B59D4A30E7`. Existing offline NU1900 metadata and CS0436 generated-type warnings remain; no fresh Synthesis/game validation is claimed.

```powershell
dotnet build 'Equipment Balance Patcher.sln' --no-restore -m:1
dotnet test 'Equipment Balance Patcher.sln' --no-build --no-restore -m:1 --verbosity minimal
dotnet run --project tools/ReplayAudit/ReplayAudit.csproj --no-build --no-restore -- Reports/audit.json Reports-v0.1g
```

This small evidence view is useful as a **manual triage and provenance report**, especially for explaining RFM omissions and separating declared material from decorative names. It has not demonstrated better classification confidence or more trustworthy numerical balancing: only 86 ordinary records offer weak name corroboration, no ordinary inconsistencies were found, recipes are unmeasured and numerical findings are the existing detector's results. Retain the simple approach; review the excluded conflicts and collect a focused recipe sample only in a separately requested follow-up. There is no evidence here to justify more name rules, relaxed eligibility or automatic corrections. This milestone stops at the report and assessment.
