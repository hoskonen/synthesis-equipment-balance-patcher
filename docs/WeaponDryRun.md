# Actionable weapon dry-run report — v0.1h

This milestone adds a reporting layer to the existing [canonical comparison](ReferenceComparison.md) and [ordinary weapon evidence audit](WeaponEvidenceAudit.md). A successful audit prints higher/lower base-damage review candidates in the Synthesis console and saves the same findings as `WeaponDryRun.txt` in the normal report directory. `NoPatch = true` remains unchanged; no plugin, override, dummy output or numerical balancing algorithm is introduced.

## Selection, ranking and formatting

The formatter consumes immutable v0.1g evidence rows, existing eligibility, resolved canonical identities and already calculated metric results. A headline candidate must be ordinarily inspected, remain analysis-eligible, have a resolved usable reference, differ from the reference FormKey, have available valid base-damage values, and have the existing Damage metric status `ReferenceDeviation`. The existing tolerance result is authoritative, including its threshold-equality convention. Other metric deviations cannot add a weapon to the damage lists.

Each FormKey appears at most once, even if input evidence/metric rows repeat. Distinct records with the same display name remain distinct. The formatter performs no extraction, classification or recalculation of numerical deviations. Name rules, the named-design hint list, material keywords, catalog, thresholds, settings, statistical analyzer and template restrictions are untouched.

Higher candidates sort by descending existing percentage; lower candidates sort by ascending percentage (most negative first). Ties use descending absolute damage difference, then ordinal FormKey. Each list shows at most **10** weapons; counts include all qualifying candidates. Empty lists explicitly say there are none with a defined percentage.

An undefined/nonfinite percentage is never fabricated or mixed into the percentage rankings. Such a qualifying deviation is still counted in its higher/lower direction, counted separately as unranked, and shown in a separate section ordered by FormKey, also capped at ten. The existing absolute-tolerance decision remains intact. The real replay has no undefined percentages; zero-reference behavior is covered by tests.

Every displayed weapon includes name, subtype/material, candidate/winning reference damage, signed damage difference, signed percentage, canonical name/FormKey/reference winner, candidate FormKey and defining plugin plus a different winning plugin. Existing structural and enchanted-base-stat warnings remain concise review notes. Display values use invariant formatting with at most two decimal places; ordering uses the original unrounded percentages. Newlines/tabs in names are flattened. The saved text uses deterministic LF newlines and UTF-8 without BOM, with no timestamp, duration or absolute output path.

The console contains a short inventory/statistical overview and published file paths, followed by the exact saved report. Former verbose category/material/exclusion breakdowns remain in existing detailed JSON/CSVs instead of overwhelming the console. `WeaponDryRun.txt` uses the same staging, destination/symlink checks and cancellation conventions as other reports. Synthesis's known no-export GUI completion limitation remains separate and unchanged; see [SynthesisCompletion.md](SynthesisCompletion.md).

## Actual replay results

The original stored dataset was replayed into `Reports-v0.1h`; this is not a fresh installed-profile/game run.

| Coverage | Count |
| --- | ---: |
| Total winning weapons | 4,422 |
| Ordinary weapons inspected | 391 |
| Excluded from ordinary inspection | 4,031 |
| Canonical weapon self-references | 16 |
| Other weapons with actual reference comparisons | 52 |
| Available base-damage comparisons | 52 |
| Higher damage candidates | **10** |
| Lower damage candidates | **7** |
| Undefined percentage / compared but damage unavailable | 0 / 0 |
| No numerical comparison, unassessed | 323 |
| Of those, no reviewed canonical reference | 292 |
| Remaining reference-ineligible inspection records | 31 |
| Keyword/name agreements | 86 |
| Ordinary material conflicts | 0 |
| Incomplete material evidence | 305 |
| Conflicts among excluded weapons | 20 |

The populations reconcile as **391 = 52 compared + 16 canonical self-references + 323 unassessed**. No-reference records are unassessed, never labeled balanced. “Compared” means at least one actual numerical comparison; base-damage coverage is shown separately so missing damage cannot be hidden by an available weight comparison.

The 305 incomplete-evidence records include 283 keyword-only and 22 lacking both reviewed keyword evidence and a recognized material word. Keyword-only does not mean erroneous. The excluded conflict count is a **record union**, not a sum of overlapping reasons: 13 records with multiple keyword tiers, nine keyword/name disagreements, with two in both groups, gives 20. The label covers weapons outside ordinary inspection, not a claim that all twenty are proven artifacts. This explains the difference from the nine raw keyword/name conflicts documented in v0.1g.

Only **17** weapons enter the damage lists, versus 49 ordinary records with any reference-metric deviation. The other **32** deviate only in non-damage metrics. RFM's Ebony Curved Sword, for example, has CriticalDamage 7 versus 5 but Damage 14 versus 13 within tolerance, so it correctly stays out of the headline report.

## Highest-ranked findings and uncertainty

All ten higher and seven lower candidates fit the display limits in this dataset. The saved report provides their full provenance and review notes. Selected cases:

| Direction / weapon | FormKey | Damage: candidate / reference | Signed difference |
| --- | --- | ---: | ---: |
| Higher: Heart Sentinel Sword | 0A5A16:HeartOfTheReach.esp | 13 / 8 | +5 / +62.5% |
| Higher: Skyforge Steel Dagger | 09F25D:Skyrim.esm | 8 / 5 | +3 / +60% |
| Higher: Remnant Scimitar | 000805:ccEDHSSE003-Redguard.esl | 12 / 8 | +4 / +50% |
| Higher: Forsworn Sword | 00086A:ccBGSSSE059-BA_Dragonplate.esl | 12 / 8 | +4 / +50% |
| Higher: Skyforge Steel Sword | 09F25C:Skyrim.esm | 11 / 8 | +3 / +37.5% |
| Lower: Golden Dagger, Ebony tier | 08429B:SRE.esp | 5 / 10 | −5 / −50% |
| Lower: Golden Bow, Ebony tier | 08429A:SRE.esp | 9 / 17 | −8 / −47.06% |
| Lower: Golden Sword, Ebony tier | 08429E:SRE.esp | 8 / 13 | −5 / −38.46% |

These are numerical review candidates, not established gameplay errors. In particular:

- Skyforge variants plausibly represent intentional advantages over ordinary Steel. Their differences do not authorize adjustments or a new exclusion.
- Heart Sentinel Sword's name may identify a special authored role that the existing bounded restrictions do not recognize. The highest record retains its structural warning. A second record with the same display name (`0A5A15`) also qualifies; it is a different FormKey, not duplicated metric output.
- Remnant Scimitar and Forsworn Sword retain structural differences. Forsworn Sword is currently classified Steel by its actual keywords, regardless of its name/plugin. Remnant Scimitar of Fire is also included with its enchanted-base-stat warning; enchantment effects are not scored.
- Akaviri Sword, Blades Sword, Scimitar and Skyforge Steel Sword all appear at +37.5%. Their authored designs may intentionally depart from a plain Steel Sword.
- Every lower candidate is SRE Golden equipment, at Ebony, Orcish or Elven tiers. “Golden” is decorative/name evidence outside the material vocabulary, not an automatic Gold tier. Their material assignments and progression need author/load-order review rather than numerical normalization.

All displayed references in this replay are won by `Weapon Armor Attribute Tweaks.esp`. Thus the ranking is relative to the current tweaked load order, not untouched Skyrim or an unquestionable balance standard. Candidate origins/winners remain visible. No suspicious passing record was silently excluded, and the 38-name v0.1g scope list was not expanded.

## Validation and retained behavior

Build succeeded and all **124 automated cases passed**, including twelve new reporting cases: higher/lower selection, equality/tolerance and other-metric-only findings, reuse of actual tolerance results, named/custom/staff gates, self/missing/unusable references, missing damage, zero reference percentage, duplicate rows, deterministic percentage/absolute/key ordering, culture, both top-ten limits, empty lists, structural notes, precise coverage, overlapping material-conflict accounting and console/text identity. Existing pipeline/executable tests and report sentinel tests preserve input/output plugin bytes and timestamps and verify no new ESP. Exactly eleven reports are now published.

All **eleven report hashes match repeat replays**. The nine prior CSVs match v0.1g byte-for-byte. Every prior JSON property except the intentional `patcherVersion` bump remains unchanged, including schema **6**, settings, equipment/classification/eligibility, template investigation, complete statistical/reference analyses and weapon evidence summary. No extra machine-readable summary or schema change is needed. The original audit SHA256 remains `A1224A661EAF8365FF6E687396325A03D95BDAE9DBC2642DF22A37B59D4A30E7`. Existing NU1900 offline metadata and CS0436 generated-type warnings remain.

```powershell
dotnet build 'Equipment Balance Patcher.sln' --no-restore -m:1
dotnet test 'Equipment Balance Patcher.sln' --no-build --no-restore -m:1 --verbosity minimal
dotnet run --project tools/ReplayAudit/ReplayAudit.csproj --no-build --no-restore -- Reports/audit.json Reports-v0.1h
```

The reporting layer now provides a traceable review shortlist directly after a successful audit. It stops here: no automatic balancing, crafting extraction, armor changes, new evidence rules or broader classifications follow from these findings.
