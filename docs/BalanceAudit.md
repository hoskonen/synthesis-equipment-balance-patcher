# Independent equipment balance audit — v0.1e

This milestone adds a read-only comparison of stored winning equipment. It does not calculate inherited values, create overrides, alter plugins, prescribe stats, process COBJ, or change classification/eligibility. The v0.1c template prototype and [v0.1d research](TemplateSemanticsValidation.md) remain intact. The user chose independent equipment analysis before template runtime validation; that validation still gates any future template comparisons.

## Populations and design identity

`AnalysisEligible` remains the existing conservative gate: confident ordinary classification, reviewed specific material/category, no templates/deletion, unknown/nonplayability, scripts, bound/artifact/special behavior, unresolved references or invalid extracted structure. This implementation never changes that gate. Enchantment alone permits analysis when enabled, but `BaselineEligible` unenchanted candidates alone supply references. Each evaluated metric additionally must be present, finite and nonnegative. Missing/invalid metrics remain explicit assessments, not zeros. Zero damage/rating is preserved as a stored value and may warrant contextual review rather than a guessed repair.

The only design identity currently established for eligible equipment is `(recordType, winning FormKey)`. Winning overrides of one FormKey are one inventory record. Explicit template root families describe related variants, but all their templated children remain ineligible. There is no reliable explicit relationship between independent, nontemplated records in these snapshots, so there is no fuzzy deduplication. Distinct FormKeys remain distinct even if names or numerical values match. “Independent reference count” therefore means **distinct recorded identities**, not proven independent authorship. This limitation is present in JSON strategy/warnings and finding caveats.

Matching defining plugin, EditorID, display name and the entire valid metric vector generates a possible-duplicate warning, without merging/removing records. Missing/invalid metrics are normalized to unavailable only in this warning key. Similar variants with different IDs/names may evade this hint, but defining/winning plugin concentration is always reported. A single author's numerous nearly identical designs may mask deviations; the audit does not pretend to correct that automatically.

Each candidate's peers exclude its own design identity. If a later approved explicit identity links multiple eligible variants, the identity strategy must change before those variants can enter references; equality of stats is insufficient evidence. Enchanted candidates are compared on base metrics only and never establish an ordinary reference. Their enchantment behavior is not scored.

## Comparable cohorts

Primary identity always includes exact record type, ordinary subtype/category and **specific material tier**. ElvenGilded/Elven, OrcishScaled/Orcish and all other reviewed tiers stay separate; family metadata does not merge cohorts.

Structural identity also includes armor type, exact slot mask, shield flag, weapon animation, declared skill, equipment-type FormKey (case-normalized), and the sorted stored flag strings. Null structure remains distinct from an explicit value. All extracted flags are retained as a conservative structural boundary, even when their relevance is unproven; this may split cohorts unnecessarily and lower coverage. Any later normalization of flags/ancillary slots needs review rather than an automatic fallback into broader peers.

This separates swords/daggers/war axes/maces/greatswords/battleaxes/warhammers/bows/crossbows; custom types and Staff animation retain prior exclusions. Armor keeps HeavyArmor/LightArmor and cuirass/helmet/gauntlet/boot/shield distinctions. Already approved ancillary slot profiles remain classified, but exact masks are separate balance cohorts. Unsupported combinations remain excluded. Exact cohort IDs, member FormKeys, reference FormKeys and design IDs make membership reproducible.

## Distributions and provisional detector

Each cohort/metric reports valid unenchanted reference count, missing/invalid reference count, min, max, mean, median, MAD, Q1 and Q3. Quartiles use linear interpolation at `(n−1)p`; median uses the same convention. MAD is the median of absolute distances from that median. Empty populations have null statistics; small populations remain descriptive, not precise conclusions. Candidate peer statistics are recalculated after design self-exclusion, so a group with five baseline members gives each ordinary member only four peers. Group and candidate sufficiency can differ.

For value `x`, peer median `m`, and nonzero MAD:

```text
signedDifference = x − m
absoluteDifference = |x − m|
percentageDifference = 100 (x − m) / m, only when m > 1e−9 and the result is finite
robustDeviationScore = 0.6745 (x − m) / MAD, only when MAD > 0 and the result is finite
```

For positive MAD, a finding requires **all** absolute, relative and robust gates: absolute difference at least the metric threshold, relative difference at least its threshold when calculable, and absolute robust score at least the configured threshold. Equal values never flag. When percentage is undefined near zero, the absolute gate remains necessary and the missing percentage is explicit. Numerical overflow produces unavailable scores rather than Infinity or a fabricated value; the mean is computed with normalization to avoid finite-input overflow.

The modified-score convention and 3.5 starting threshold follow [NIST's outlier discussion](https://www.itl.nist.gov/div898/handbook/eda/section3/eda35h.htm). This detector is provisional **labeling for review**, not a formal hypothesis test, proof of a normal distribution, gameplay error or target stat. Skyrim equipment cohorts may be discrete, tied or multi-modal; multiple comparisons and authored variants add false-positive risks.

When MAD is zero, no division occurs and the robust score remains null. Both the absolute and relative gates are doubled; a finding is labeled a StatisticalDeviation at most, not a PotentialConsistencyIssue. `ZeroVariance` means all peers match; `ZeroMad` includes tied medians with a wider range. A near-zero median uses the doubled absolute gate and no percentage. The factor of two is a conservative initial calibration choice, not an engine rule.

Plugin concentration uses both defining and winning populations, merging case variants of the same plugin. If either maximum share is **greater than 60%**, a reference is concentrated. Exactly 60% passes this gate. It is deliberately strict: even a legitimate universal compatibility patch can reduce confidence. A concentrated reference never yields a supported consistency claim. No peers are discarded, reweighted or replaced with different materials to hide concentration.

Candidate assessments distinguish InvalidMetric, NoReference, InsufficientPeers, ConcentratedReference, SufficientPeers, ZeroVariance and ZeroMad. Every assessment is in JSON, including non-findings. A record is counted analyzed if at least one metric has sufficient unconcentrated peers; that does not imply every metric was assessable. Insufficient-peer/no-reference counts are a subset of insufficient-evidence records; summary subcounts are not disjoint.

Finding classes:

| Class | Meaning |
| --- | --- |
| PotentialConsistencyIssue / Review / SupportedForReview | Absolute/relative/positive-MAD gates passed with enough peers, acceptable concentration and no possible-duplicate hint. A contextual review is warranted; no error is confirmed. |
| StatisticalDeviation / Review / Moderate | Gates passed against enough unconcentrated peers with zero MAD; there is no valid robust score. |
| InsufficientEvidence / Info / Low | Numeric gates passed, but peer size, concentration or possible duplicates prevents a reliable comparison. Sparse examples remain visible without calling them confirmed anomalies. |

Finding CSV retains identity, display name, defining/winning plugin, category/tier, metric/value, reference range/median/MAD, peer count/keys, signed/absolute/percentage/robust differences, class/confidence, explanation and false-positive reasons. It contains one row per finding; zero findings still writes a header. Full concentration and all assessments are linked through cohort IDs and JSON. No metric is combined into DPS, power, efficiency or a recommended replacement value.

## Settings

| Setting | Default / validation |
| --- | --- |
| EnableBalanceAudit | true; false produces empty balance groups/findings while retaining exclusion accounting and both CSV headers |
| MinimumIndependentPeers | 5; integer at least 3; applies after self-exclusion |
| RobustDeviationThreshold | 3.5; finite and strictly positive |
| IncludeEnchantedAnalysisCandidates | true; disabling adds a setting exclusion, never changes inventory eligibility |
| EnableMaterialProgressionReport | true; controls separate JSON progression comparisons |
| BalanceThresholds | Nested per-metric MinimumAbsoluteDifference / MinimumRelativeDifference, each finite and nonnegative; null threshold objects fail validation |

| Metric | Minimum absolute difference | Minimum relative difference |
| --- | ---: | ---: |
| Damage | 3 | 0.25 (25%) |
| ArmorRating | 8 | 0.25 |
| Weight | 2 | 0.25 |
| Speed | 0.15 | 0.25 |
| Reach | 0.2 | 0.25 |
| CriticalDamage | 2 | 0.25 |
| Stagger | 0.2 | 0.25 |

Settings follow existing autogenerated JSON conventions; omitted properties use defaults. Threshold equality passes, provided difference is nonzero. Zero MAD doubles the metric gates. These values require calibration against actual findings. There are no editable vanilla stat tables or patching controls. Settings are copied into the report so comparisons can be reproduced.

## Material progression

All specific tiers have descriptive reference distributions in balance-groups.csv. Separate JSON progression compares only the declared **provisional Iron→Steel anchor**, holding all other structural characteristics fixed. It is a small review hypothesis, not a complete material hierarchy. It compares cohort median damage for weapons or rating for armor; inversion means Steel's median is lower than Iron's. Such a pair receives PossibleProgressionInversion only when both populations meet count/concentration/duplicate gates; otherwise it is InsufficientEvidence. Equal/greater Steel median is DescriptiveProgression, not proof of correct progression. Ranges, identities and concentration remain available in linked groups.

The reference replay supplies mostly inadequate pairs, so extending a material ladder from familiar names is not justified. Uncommon variants get no assumed order, and inversion results do not feed within-material findings or change stats. Reviewing intended material progression, overlapping distributions and independent identities remains future calibration work.

## Real-load-order replay review

Source: the user's original `Reports/audit.json`, SHA256 `A1224A661EAF8365FF6E687396325A03D95BDAE9DBC2642DF22A37B59D4A30E7`. Output: **Reports-v0.1e/**. This is offline re-analysis of stored winning snapshots, not a fresh live Synthesis, xEdit or in-game run. No plugins were loaded by ReplayAudit. The final replay retains 9,095 records, 6,540 confident classifications, 699 baseline candidates, 1,202 inventory statistics candidates, and all 6,095 template exclusions. Balance uses the narrower existing AnalysisEligible gate.

| Coverage | Count |
| --- | ---: |
| Eligible / enabled balance candidates | 825 (428 WEAP, 397 ARMO) |
| Analyzed with at least one sufficient unconcentrated peer metric | 91 |
| Insufficient evidence records | 734 |
| Of those, all metrics lack enough peers/reference | 640 |
| Excluded from balance | 8,270 |
| Candidate records with invalid/missing evaluated metrics | 0 |
| Findings | 81: 67 WEAP, 14 ARMO |
| PotentialConsistencyIssue | 5: 2 WEAP, 3 ARMO |
| StatisticalDeviation, zero MAD | 8 WEAP |
| InsufficientEvidence finding rows | 68: 57 WEAP, 11 ARMO |

Group/metric statuses (not record counts): 1,258 InsufficientPeers, 24 NoReference, 46 ConcentratedReference, 22 SufficientPeers, 31 ZeroMad, 9 ZeroVariance. There are 1,390 cohort/metric rows. Progression has 14 Iron/Steel pairs: one DescriptiveProgression, 13 InsufficientEvidence; no supported inversion. These coverage limits are intentional and should not be bypassed by merging tiers, using template proposals or treating enchanted records as baselines.

Findings by material (rows): Iron 22, Steel 15, Daedric 9, Dragonbone 7, DraugrHoned 7, Ebony 4, Dwarven 3, Orcish/Imperial/BonemoldHeavy/StalhrimLight 2 each; remaining tiers have smaller counts. Origin counts: Skyrim.esm 39, SRE.esp 20, Dragonborn.esm 10, Dawnguard.esm 6, RFM.esm 3, and three other origins one each. Winning-plugin counts show where the stored values actually come from: Weapon Armor Attribute Tweaks.esp 43, SRE.esp 20, Skyrim.esm 9, RFM.esm 3, Dragonborn.esm 2, and four other winners one each. Findings are metric rows, not counts of erroneous items; do not infer fault from an origin/winner count.

### Highest-priority supported review cases

Values were checked against the existing inventory snapshots and the findings' exact peer lists. They still need xEdit winner verification and authored-intent review.

| FormKey / name | Winning plugin | Finding | Peers / caveat |
| --- | --- | --- | --- |
| `0A5A1D:HeartOfTheReach.esp`, Antler Bow | HeartOfTheReach.esp | Steel bow damage 18 vs median 10; +80%, modified score 5.396 | 5 unenchanted peers; strongest ordinary damage review target, but custom behavior/intent may justify it |
| `0AB705:Skyrim.esm`, Bow of the Hunt | Weapon Armor Attribute Tweaks.esp | Steel bow weight 7 vs 10; −30%, score −4.047 | 6 peers; enchanted named equipment may intentionally be lighter; enchantment is not modeled |
| `0CAE15:Skyrim.esm`, Wolf Armor | Weapon Armor Attribute Tweaks.esp | Heavy Steel cuirass weight 20 vs 34; −41.18%, score −4.7215 | 12 peers; intentional named-set advantage is plausible |
| `056A9D:Skyrim.esm`, Ancient Nord Boots | Weapon Armor Attribute Tweaks.esp | Steel boots weight 5 vs 8; −37.5%, score −4.047 | 8 peers; material identity may cover a deliberately different design |
| `0CEE7C:Skyrim.esm`, Wolf Boots | Weapon Armor Attribute Tweaks.esp | Steel boots weight 4 vs 8; −50%, score −5.396 | 8 peers; same named-set caveat as Wolf Armor |

Zero-MAD cases include Headsman's Axe (weight and critical damage), Poacher's Axe (critical damage/stagger), Golden Bow (weight), and three SRE Steel sword/katana weight rows. Their individual stats merit inspection but specialized/named equipment and authored variants are prominent false-positive possibilities. No universal weapon score is inferred from multiple rows.

Sparse standout examples include Drainblood Battleaxe weight 5 vs one peer at 25; Rueful Axe weight 10 vs two peers' median 26.5; Miraak's Sword variants weight 3 vs one peer at 15; and unnamed zero-stat iron maces. These remain **InsufficientEvidence**. They illustrate missing universal unique/artifact identity and limited ordinary references, not permission to change stats or loosen exclusions.

### Representative cohort coverage

Exact structural cohorts can split a category/tier into multiple rows:

| Requested example | Reference count / result |
| --- | --- |
| Iron swords | 3 in the observed ordinary structural cohort: insufficient |
| Steel swords | 13 in an unconcentrated cohort; another 11-record cohort is concentrated, and another has only 2 |
| Elven daggers | 2: insufficient |
| Orcish maces | 2: insufficient |
| Dwarven bows | 2: insufficient |
| Heavy iron cuirasses | 2: insufficient |
| Light leather cuirasses | 2: insufficient |
| Heavy steel helmets | 11 in one exact-mask cohort with zero MAD; another cohort has 3, insufficient |
| Shields | Heavy Iron 14 but concentrated; Heavy Steel 5 with zero MAD, so ordinary self-exclusion leaves only 4; most other tiers are sparse |

These are cohort reference counts before candidate self-exclusion. No group was expanded to obtain five peers. Synthetic tests separately cover excessive weapon damage, excessive armor rating, positive/negative changes, legitimate trivial variations, and threshold boundaries.

### RFM coverage

The reference has 302 records defined or won by RFM.esm; none is templated. **Five are AnalysisEligible and BaselineEligible; 297 remain excluded.** All five currently lack sufficient peers for a supported conclusion:

| FormKey | Name | Category / material | Stored damage |
| --- | --- | --- | ---: |
| `01CA8E:RFM.esm` | Nordic Straight Sword | Sword / Nordic | 13 |
| `01CA9B:RFM.esm` | Ebony Curved Sword | Sword / Ebony | 14 |
| `021BAD:RFM.esm` | Glass Tempered Greatsword | Greatsword / Glass | 22 |
| `021BAF:RFM.esm` | Overheated Glass Sword | Sword / Glass | 15 |
| `072D77:RFM.esm` | Worn Atmora Sword | Greatsword / Steel | 7 |

Winners are RFM.esm. Worn Atmora Sword produces three insufficient-evidence rows: damage 7 vs peer median 17.5, critical damage 0 vs 9, stagger 0 vs approximately 1.1, with **four** peers. Its worn design may explain the differences. The other four yield no numeric finding under current gates; that is not proof of correctness.

Exclusion reason occurrences overlap: SpecialEquipment 294, ScriptedEquipment 46, ClassificationNotConfident 26, ConflictingMaterialKeywords 13, ConflictingWeaponCategory 8, StaffAnimationOutsideOrdinaryAnalysis 5. There is no RFM-wide ban and no forced spear/custom mapping. Full excluded FormKeys and reasons are in balanceAnalysis.records, and the existing inventory provides diagnostics/evidence. Future coverage would require per-record review of special flags/scripts, custom combat semantics and material identities plus genuinely comparable independent peers; increasing coverage is not itself justification to remove gates.

## Compatibility, verification and follow-up

Patcher version is 0.1e; audit JSON schema **4** adds balance settings and `balanceAnalysis` (policy, summary, record statuses, groups, all metric evaluations, findings and progression). Classification version **2**, template policy **prototype-1**, stored inventory/group contents and existing template objects retain their semantics. Replay accepts source schemas 1–4 and applies defaults for absent new settings. Exactly eight report files are published; disabling balance still supplies its two CSV headers. The five pre-existing CSVs compare byte-for-byte with Reports-v0.1c for this source.

Build and all **88 automated cases passed**, including twelve new balance tests: structural/material isolation, self-exclusion and FormKey identity, enchantment populations, missing/invalid metrics, sparse and concentrated populations, zero MAD/ties, near-zero/overflow, signed/boundary deviations, deterministic ordering/culture/CSV quoting, separate progression, settings validation, prototype preservation and report-only output. Existing pipeline/subprocess tests exercise absent/existing output plugins and verify input/output bytes plus output timestamps remain unchanged. Synthetic fixture creation is test setup; the auditor emits no ESP/ESL. Dependencies remain pinned, with existing NU1900 offline metadata and CS0436 generated-type warnings.

```powershell
dotnet build 'Equipment Balance Patcher.sln' --no-restore -m:1
dotnet test 'Equipment Balance Patcher.sln' --no-build --no-restore -m:1 --verbosity minimal
dotnet run --project tools/ReplayAudit/ReplayAudit.csproj -- Reports/audit.json Reports-v0.1e
```

Final replay reproducibility was verified: **all eight SHA256 hashes matched across repeat runs**, and the original source hash is unchanged. Existing JSON summary, equipment, groups, classification diagnostics, keyword/eligibility summaries, template policy, template analysis and root objects also compare unchanged with v0.1c. Whole JSON documents intentionally differ in top-level version/settings/balance additions. The five existing CSV hashes match v0.1c.

For fresh validation, run Synthesis through the user's actual mod-manager profile after relevant equipment patchers, with a dedicated output directory. Record plugin hashes before/after. Verify Antler Bow and the named armor/bow winners in xEdit, and inspect each finding's peer FormKeys, structural cohort, reference membership, enchantment exclusions and concentration. Review author intent and known functional behavior before deciding a deviation warrants correction. Preserve the GUI completion caveat in [SynthesisCompletion.md](SynthesisCompletion.md). This milestone does not certify the installed GUI or actual runtime behavior.

Next step: inspect real findings and calibrate the detector, identity strategy and narrow progression anchor. Automatic stat changes, template promotion, global combat formulas, silver corrections, keyword forwarding and COBJ remain outside this implementation and require separate authorization.
