# Winning canonical reference comparison — v0.1f

The prototype compares ordinary stored equipment with an explicit canonical Skyrim identity of the same category, specific material and armor type. The identity is vanilla; the numbers come from its **current winning snapshot**. There are no hardcoded vanilla stats, inherited values, stat recommendations or plugin writes. The v0.1e statistical detector, classifier version 2 and template policy `prototype-1` remain unchanged.

## Reviewed catalog

The 28 FormKeys were checked against the established Mutagen FormKeys SkyrimSE 3.4.0 registry at commit `650e147f854086b47b91ea80a881751466135256`: [weapon identities](https://github.com/Mutagen-Modding/Mutagen.Bethesda.FormKeys/blob/650e147f854086b47b91ea80a881751466135256/Mutagen.Bethesda.FormKeys.SkyrimSE/Skyrim/Weapon.cs) and [armor identities](https://github.com/Mutagen-Modding/Mutagen.Bethesda.FormKeys/blob/650e147f854086b47b91ea80a881751466135256/Mutagen.Bethesda.FormKeys.SkyrimSE/Skyrim/Armor.cs). These sources associate each named, typed record with its explicit FormID; IDs were not inferred from names. Expected category/material/type were also checked against the existing classifier's results in the supplied winning snapshots. That is source and snapshot validation, not a fresh CK/runtime validation of engine behavior.

Every ID below belongs to `Skyrim.esm`. The executable catalog is the small explicit list in `EquipmentBalancePatcher/ReferenceComparison.cs`.

| Reference category/type | Iron | Steel | Elven | Dwarven | Orcish | Ebony |
| --- | --- | --- | --- | --- | --- | --- |
| Sword | 012EB7 | 013989 | 0139A1 | 013999 | 013991 | 0139B1 |
| Dagger | 01397E | 013986 | 01399E | 013996 | 01398E | 0139AE |
| Bow | — | — | 01399D | 013995 | 01398D | 0139AD |
| Heavy cuirass | 012E49 | 013952 | — | 01394D | 013957 | 013961 |
| Heavy helmet | 012E4D | 013954 | — | 01394F | 013959 | 013963 |
| Light cuirass | — | — | 0896A3 | — | — | — |
| Light helmet | — | — | 01391D | — | — | — |

Steel armor selects the A variant. Elven light cuirass selects the ordinary cuirass, not the gilded or reduced light variant. Bows deliberately omit Iron/Steel: the registry establishes LongBow/HuntingBow identities, but this review did not independently establish those records' original material assignments. A mod's winning material alone is insufficient justification to label them canonical Iron/Steel entries. Other empty cells have no reviewed entry; no cross-tier fallback is allowed. The catalog remains limited to this milestone.

## Resolution, eligibility and metrics

Resolution uses the already extracted immutable winning equipment, indexed by FormKey. A reference must exist, match its expected WEAP/ARMO/category/specific material/armor type confidently, be `BaselineEligible`, and have at least one valid metric. Missing references are `Missing`; failed identity or ordinary-record validation is `Unusable`, with explicit reasons. Templates, scripts, special flags, enchantments on references and unsupported classifications retain existing exclusions. No extra plugin loading or classification pass is introduced.

Candidates retain `AnalysisEligible`, the existing `IncludeEnchantedAnalysisCandidates` switch and all existing exclusions. Enchantment alone permits base-stat comparison by default, with `EnchantedCandidate_BaseStatsOnly`. A difference in the v0.1e structural cohort (flags, animation, skill, equipment link or exact slots) adds `StructuralDifferencesRequireReview`; it does not silently imply identical behavior. No peer minimum, MAD, duplicate-design or concentration requirement applies here.

Weapons compare Damage, Speed, Reach, Weight, CriticalDamage and Stagger. Armor compares ArmorRating and Weight. Each metric is independent:

```text
signedDifference = candidate − winning reference
absoluteDifference = |signedDifference|
percentageDifference = 100 × signedDifference / winning reference
```

Missing, negative or nonfinite metric values are `Unavailable`, never fabricated zeros. Percentage is null for zero reference values or a nonfinite calculated percentage, with a reason. The absolute gate still applies. Finite nonnegative stored zeros remain valid values.

Tolerance uses the existing `BalanceThresholds` defaults: Damage 3, ArmorRating 8, Weight 2, Speed 0.15, Reach 0.2, CriticalDamage 2, Stagger 0.2; each has relative threshold 0.25. A nonzero difference must meet **both** absolute and calculable relative thresholds; equality passes. Undefined percentage uses the absolute threshold alone. No robust-score gate or zero-MAD doubling is applied. These are descriptive review thresholds, not gameplay rules.

| Record status | Meaning |
| --- | --- |
| Reference | Candidate is the selected canonical FormKey; no deviation finding, even when a metric is unavailable. |
| WithinTolerance | All compared metrics are available and below at least one tolerance gate, or equal. |
| ReferenceDeviation | At least one available metric meets both applicable tolerance gates; other unavailable metrics remain explicit. |
| NoReference | Eligible exact category/tier/type has no catalog entry. |
| ReferenceUnavailable | Intended reference is missing/unusable, or no metric deviates and at least one requested comparison is unavailable. |
| Excluded | Existing eligibility or the comparison/enchantment setting prevents analysis. |

`EnableReferenceComparison` is the only new setting, default true. False retains catalog resolution diagnostics, produces excluded JSON records and an empty comparison CSV with its header. Existing balance settings and their validation remain shared; changing shared thresholds affects both components.

## Reports and counting

Schema **5**, patcher **0.1f**, adds `referenceAnalysis` with policy `winning-canonical-1`, resolved catalog provenance/reasons, every record's result and metric differences, and coverage grouped by exact record type/category/material/armor type. References' canonical descriptive names and winning display names are separate.

The ninth report, `reference-comparisons.csv`, contains one row per compared metric. Eligible `NoReference` or missing/unusable-reference records have one status row with empty numeric cells. `Excluded` records are retained in JSON only. Candidate identity/winner, canonical reference key/name, reference winning name/plugin, signed/absolute/percentage differences, metric status, record status and uncertainty reasons are included. Metric `Unavailable` can coexist with a record's `ReferenceDeviation` from another metric.

`matchedRecords` counts eligible records assigned a catalog identity, including the references themselves and any unavailable intended reference. `comparableRecords` counts **non-reference** records with at least one available metric comparison. `referenceUnavailableRecords` counts that record status, not every record with a missing optional metric. Catalog `missingReferences`/`unusableReferences` diagnose resolution separately. Coverage rows and summary counts preserve these distinctions; none is a count of confirmed errors.

## Available dataset and coverage

Replay used the existing `Reports/audit.json` (9,095 stored winning records), writing separately to `Reports-v0.1f`. This is the same historical dataset used for v0.1e, not a fresh Synthesis run. The four available audit directories contain no load order or winning record from `RFM - Balance Patch.esp`; its effects cannot be evaluated here.

| Coverage | Count |
| --- | ---: |
| Catalog / resolved / missing / unusable | 28 / 28 / 0 / 0 |
| Reference winners: Weapon Armor Attribute Tweaks.esp / Skyrim.esm / Update.esm | 16 / 8 / 4 |
| Existing analysis-eligible records | 825 |
| Assigned canonical identity, including self-references | 140 (17.0%) |
| Self-references | 28 |
| Other records with available reference comparisons | 112 (13.6% of 825) |
| WithinTolerance / ReferenceDeviation | 37 / 75 |
| NoReference / ReferenceUnavailable | 685 / 0 |
| Excluded, JSON only | 8,270 |
| v0.1e statistically supported records | 91 (11.0%) |

The reference approach adds **70** comparable records without statistical support. **42** non-reference records are comparable by both methods. Of the 91 statistical records, three are selected canonical references; comparing non-reference populations gives 112 versus 88. Another **46** non-reference statistically supported records have no catalog entry. The methods therefore cover different subsets: the small reference catalog complements the statistical detector and modestly improves total coverage, but does not replace it.

Coverage by reviewed category and material follows. Each cell is **matched records / deviating records**, including one self-reference per occupied cell. All these matched references were usable. Full eligible and unmatched category/tier coverage, including tiers outside this catalog, is in `referenceAnalysis.coverage`.

| Category/type | Iron | Steel | Elven | Dwarven | Orcish | Ebony |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Sword | 3 / 2 | 35 / 32 | 2 / 1 | 2 / 1 | 2 / 1 | 8 / 7 |
| Dagger | 5 / 3 | 13 / 11 | 2 / 1 | 3 / 2 | 2 / 1 | 3 / 2 |
| Bow | — | — | 2 / 1 | 4 / 3 | 2 / 1 | 7 / 2 |
| Heavy cuirass | 2 / 0 | 13 / 3 | — | 2 / 0 | 2 / 0 | 1 / 0 |
| Heavy helmet | 3 / 1 | 14 / 0 | — | 1 / 0 | 2 / 0 | 1 / 0 |
| Light cuirass | — | — | 2 / 0 | — | — | — |
| Light helmet | — | — | 2 / 0 | — | — | — |

Of 685 unmatched eligible records, 464 belong to families outside this prototype (boots, gauntlets, shields and other weapon families); 221 are in swords/daggers/bows/cuirasses/helmets but lack the exact reviewed tier/type entry. Many light armor tiers and common modded materials remain outside the catalog. No classification rules or template exclusions were weakened to improve counts.

## Significant deviations and review limits

There are **114 deviating metric rows across 75 records**: Weight 41, CriticalDamage 39, Damage 27, Stagger 4, and ArmorRating/Reach/Speed one each. The full CSV has 1,345 data rows, including non-findings and no-reference status rows. Comparing raw absolute values across different metrics is not a severity ranking; useful individual review examples follow.

| Candidate FormKey / name | Metric | Candidate / winning reference | Signed difference | Percentage |
| --- | --- | ---: | ---: | ---: |
| 001886:NW_Steel_Plate_Armors.esp — Full Plate Armor | ArmorRating | 40 / 31 | +9 | +29.0% |
| 0CAE15:Skyrim.esm — Wolf Armor | Weight | 20 / 35 | −15 | −42.9% |
| 062303:Skyrim.esm — Ulfric's Armor | Weight | 25 / 35 | −10 | −28.6% |
| 08429A:SRE.esp — Golden Bow (Ebony) | Damage | 9 / 17 | −8 | −47.1% |
| 08429B:SRE.esp — Golden Dagger (Ebony) | Damage | 5 / 10 | −5 | −50.0% |
| 09CCDC:Skyrim.esm — Blade of Woe | Damage | 11 / 5 | +6 | +120.0% |
| 0A5A16:HeartOfTheReach.esp — Heart Sentinel Sword | Damage | 13 / 8 | +5 | +62.5% |
| 0F71D0:Skyrim.esm — Dragonbane | Damage | 13 / 8 | +5 | +62.5% |

Full Plate Armor is classified **Steel**, despite its plugin name; this may be author intent or a keyword mismatch and must not trigger automatic keyword or stat repair. SRE's Golden equipment occupies multiple declared tiers, so a golden appearance/name is no proof that an Ebony comparison is appropriate. The winning material is authoritative only for this classifier's exact matching, not for intended gameplay design.

Blade of Woe, Dragonbane, Nightingale Bow and Harkon's Sword pass the existing eligibility rules in these snapshots despite names suggesting special authored roles. Several are enchanted, and their base stats may deliberately differ. This exposes the existing special-equipment detection limit; it does not authorize expanding it. Heart Sentinel Sword also has a structural review warning. A canonical winner can itself be intentionally altered or incorrectly tagged, influencing every dependent comparison. Sixteen reference winners come from an equipment tweak plugin, so these are load-order-relative comparisons, not assertions about untouched Skyrim.

Ten deviating records also have v0.1e findings of any evidence class. Of the five supported `PotentialConsistencyIssue` cases, only **Wolf Armor's weight** matches this catalog and also deviates. Ancient Nord Boots and Wolf Boots have no reviewed family; Steel Antler Bow and Bow of the Hunt have no reviewed Steel bow. Their statistical findings remain intact. New reference deviations do not inherit statistical confidence, and lack of a reference finding does not invalidate a peer finding.

## RFM inspection

The available inventory has 302 RFM-origin weapons: five analysis-eligible and 297 excluded. Exclusion reasons overlap: SpecialEquipment 294, ScriptedEquipment 46, ClassificationNotConfident 26, ConflictingMaterialKeywords 13, ConflictingWeaponCategory 8, StaffAnimationOutsideOrdinaryAnalysis 5. Existing `CantDrop`/special behavior and unsupported custom animations remain excluded.

| Eligible RFM record | Classification | New result |
| --- | --- | --- |
| 01CA9B:RFM.esm — Ebony Curved Sword | Sword / Ebony | ReferenceDeviation: CriticalDamage 7 versus winning Ebony Sword 5, +2 / +40%; Damage 14 versus 13 is within tolerance. |
| 01CA8E:RFM.esm — Nordic Straight Sword | Sword / Nordic | NoReference: unreviewed tier. |
| 021BAF:RFM.esm — Overheated Glass Sword | Sword / Glass | NoReference: unreviewed tier. |
| 021BAD:RFM.esm — Glass Tempered Greatsword | Greatsword / Glass | NoReference: outside catalog family/tier. |
| 072D77:RFM.esm — Worn Atmora Sword | Greatsword / Steel | NoReference: outside catalog family. |

The Ebony Curved Sword uses `0139B1:Skyrim.esm`, whose winner is `Weapon Armor Attribute Tweaks.esp`; its candidate winner is `RFM.esm`. One of five eligible RFM records becomes comparable; the other four and 297 excluded records retain their boundaries. No staff-animation spear is remapped. The unavailable human-authored RFM balance patch was not treated as a reference or gold standard.

## Verification and recommendation

Build succeeded and all **97 automated cases passed**. Nine new reference tests cover catalog identity, actual winning override extraction, identity failures, missing references, tier/category/type isolation, self-reference, signed/relative/zero/missing values, thresholds independent of peers, enchantment settings, excluded templates/custom weapons, deterministic reports and preserved earlier analysis. Existing pipeline and executable tests cover absent/existing output plugins and compare input/output bytes and timestamps; report sentinel tests allow exactly nine reports and preserve unrelated files. `NoPatch = true` remains set. No game-record write path was added; replay loads only JSON.

All **nine report SHA256 hashes matched across two replays**. All seven existing CSVs match v0.1e byte-for-byte. Existing JSON summary, equipment, groups, classification diagnostics, keyword/eligibility summaries, template policy/analysis/roots, input load order and **entire balanceAnalysis** match v0.1e. The source audit's SHA256 remains `A1224A661EAF8365FF6E687396325A03D95BDAE9DBC2642DF22A37B59D4A30E7`. Build retains existing NU1900 offline vulnerability-metadata and CS0436 generated-type warnings. No fresh game/runtime validation was performed.

```powershell
dotnet build 'Equipment Balance Patcher.sln' --no-restore -m:1
dotnet test 'Equipment Balance Patcher.sln' --no-build --no-restore -m:1 --verbosity minimal
dotnet run --project tools/ReplayAudit/ReplayAudit.csproj --no-build --no-restore -- Reports/audit.json Reports-v0.1f
```

The approach is worth a **limited, separately reviewed extension** as a practical descriptive companion to peer analysis. It gives inspectable comparisons for sparse groups with little code, but this catalog reaches only 112 other eligible records and the false-positive burden is substantial. First review the named cases and threshold usefulness against a fresh intended load order; separately obtain the RFM balance-patch dataset for external validation. Any later catalog extension should verify each identity and category/tier explicitly. This milestone stops at the prototype: no expansion, custom mappings, inheritance promotion or automatic adjustment follows from these results.
