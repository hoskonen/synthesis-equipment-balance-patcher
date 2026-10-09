# Template semantics validation — v0.1d

Research date: 2026-10-09. This is a documentation milestone over the v0.1c executable, schema 3 and `prototype-1` policies. No production policy, validation status, classification, eligibility, report schema or plugin was changed. Fixtures below are specifications for a later manual session; none was created or run in Skyrim or the Creation Kit during this milestone.

## 1. Executive summary

**No equipment inheritance rule is ready to be promoted to `ValidatedInherited` on the evidence collected here.** Parent-derived base properties and child-local name/value/enchantment are supported as a working model by author workflows. Those sources are not controlled, field-by-field Skyrim SE experiments. The English Creation Kit equipment pages and UESP record-format pages could not be retrieved. The reviewed xEdit and Mutagen schemas establish stored data and links, not the engine's template initialization algorithm.

The confirmed findings are static: WEAP uses a typed CNAM parent; ARMO uses a typed TNAM parent; DATA, DNAM, CRDT, KWDA, EITM, EAMT, VMAD and armor body/model data remain individually inspectable stored fields. Mutagen extraction retains winning-child values. Successful chain traversal does not turn those values into runtime values. SKSE supplies read-only probes for loaded form memory, with important limitations described below.

Recommend **v0.1e perform controlled validation first**, then review evidence before implementing effective values. Keywords are a blocking classification dependency: root replacement, child preservation and chain union remain competing hypotheses for each record type and chain depth. Parent scripts/flags cannot be inferred from a scalar inheritance rule. Statistical sampling must also distinguish independent designs from enchanted variants sharing a root.

## 2. Source-backed findings and evidence limits

Sources are identified below so each matrix can separate storage evidence from inheritance evidence. GitHub references are pinned to the inspected versions or commits.

| ID | Source | What was established; what was not |
| --- | --- | --- |
| M-W | [Mutagen 0.54.4 Weapon.xml](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.4/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Weapon.xml) | DATA separates value/weight/damage; DNAM separates animation/speed/reach/skill/stagger/flags; CRDT separates damage/multiplier/flags/effect; CNAM is a WEAP FormLink. Schema describes storage, not engine inheritance. |
| M-A | [Mutagen 0.54.4 Armor.xml](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.4/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Armor.xml) | TNAM is an ARMO FormLink; DATA value/weight, scaled DNAM rating, BodyTemplate, keywords, enchantment, gendered world models and ARMA links are separate. BodyTemplate is not an NPC-style parent inheritance mask. |
| M-G | [Mutagen generated weapon getters](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.4/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Weapon_Generated.cs), [armor getters](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.4/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Armor_Generated.cs) | Record getters expose local data and parent links. The auditor snapshots these getters; FormLink lookup is a separate operation. No complete engine-effective resolver was established by these getters. |
| X | [xEdit TES5 definitions, commit 9058a794](https://github.com/TES5Edit/TES5Edit/blob/9058a79437367c2cb1b757a9021ab443d743012d/Core/wbDefinitionsTES5.pas) | `wbRecord(WEAP)` and `wbRecord(ARMO)` independently define local fields and typed template links. ARMO rating uses a divisor of 100; major Non-Playable/Shield flags differ from weapon DNAM flags. The reviewed definitions provide no field-by-field runtime template-copy contract. |
| S-W | [SKSE64 PapyrusWeapon.cpp, commit 25b72352](https://github.com/ianpatt/skse64/blob/25b72352adb6543fa6d0bd3795780672b2e238e0/skse64/PapyrusWeapon.cpp), [GameObjects.h](https://github.com/ianpatt/skse64/blob/25b72352adb6543fa6d0bd3795780672b2e238e0/skse64/GameObjects.h) | Registered getters inspect loaded weapon damage, speed, reach, stagger, critical values, animation type, skill, equip slot, enchantment, charge and template pointer. Inline speed/reach/type/critical accessors read memory fields; they do not themselves traverse CNAM. This does not show what the engine copied during loading or what every combat path uses. |
| S-A | [SKSE64 PapyrusArmor.cpp](https://github.com/ianpatt/skse64/blob/25b72352adb6543fa6d0bd3795780672b2e238e0/skse64/PapyrusArmor.cpp) | GetArmorRating reads integer rating divided by 100; GetWeightClass, GetSlotMask, GetEnchantment and armor-addon/model getters inspect loaded armor components. These are observation tools, not proof of TNAM inheritance. |
| S-F | [SKSE64 PapyrusForm.cpp](https://github.com/ianpatt/skse64/blob/25b72352adb6543fa6d0bd3795780672b2e238e0/skse64/PapyrusForm.cpp), [Form.psc](https://github.com/ianpatt/skse64/blob/25b72352adb6543fa6d0bd3795780672b2e238e0/scripts/modified/Form.psc) | GetWeight reads TESWeightForm; GetKeywords/GetNumKeywords/GetNthKeyword enumerate the loaded BGSKeywordForm. Enumeration must be compared with native HasKeyword and the relevant gameplay consumer; it is not proof those paths agree. |
| W | [Weapon pre-enchant author's Patch Workflow](https://www.nexusmods.com/skyrimspecialedition/articles/11346), section “Generating the weapon records” | Describes relying on template base properties while preserving name, gold value, enchantment/charge and scripts. It also copies base data manually. This supports a hypothesis but cannot isolate runtime template copying from already matching stored fields. |
| A | [TES Alliance Character Design: Introduction](https://tesalliance.org/forums/index.php?/topic/7365-character-design-introduction/), Part II | Describes template armor with name/value/enchantment editable and other properties inherited. The broad statement does not separately test VMAD, header flags, EAMT or nested chains. Its editor tutorial is not an SE runtime experiment. |

Official/documentation targets attempted: [CK Weapons](https://ck.uesp.net/wiki/Weapons), [CK Armor](https://ck.uesp.net/wiki/Armor), [CK weapon tutorial](https://skyrimck.uesp.net/wiki/Customizing_Weapons_%26_Armor), [UESP WEAP format](https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format/WEAP), [UESP ARMO format](https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format/ARMO). Access failures mean their contents were **not verified here**; they are follow-up targets, not citations proving inheritance. No Bethesda-authored binary-format specification or engine loading implementation was obtained.

There is an important evidence tension: W explicitly treats scripts as local, whereas A's general exception list names only name/value/enchantment. That omission does not prove armor scripts inherit. Neither source specifies whether flags are copied, whether empty fields trigger fallback, or whether direct-parent effective values are recursively used. No reviewed controlled experiment resolves these points. No alternative patcher's implementation was used as engine evidence.

## 3. WEAP field matrix

“Stored source” means physically stored in **each** record, child and parent independently. Proposed sources are hypotheses, not observations. “Root?” means terminal root only if recursive inheritance for that individual field is subsequently demonstrated. Confidence describes inheritance, not the independently confirmed storage mapping. Strongly supported means the broad workflow supports the candidate; it is below the threshold for validation.

| Property | Stored source | Proposed runtime source | Evidence | Confidence | Outstanding validation |
| --- | --- | --- | --- | --- | --- |
| Base damage | Each record DATA.Damage | Parent; root? | M-W/X storage; W broad inheritance | Strongly supported | W1 damage pair; T three-level chain; getter plus controlled hit comparison |
| Weight | Each DATA.Weight | Parent; root? | M-W/X; W | Strongly supported | W1 weight pair, GetWeight and inventory/carry-weight comparison |
| Speed | Each DNAM.Speed | Parent; root? | M-W/X; W; S-W probe | Strongly supported | W1 speed pair; getter plus repeated attack timing |
| Reach | Each DNAM.Reach | Parent; root? | M-W/X; W; S-W | Strongly supported | W1 reach-only pair; getter plus controlled hit-distance comparison |
| Critical damage | Each CRDT.Damage | Parent; root? | M-W/X; W; S-W | Strongly supported | W1 critical-damage-only pair; log separately from multiplier |
| Critical multiplier | Each CRDT.PercentMult / xEdit % Mult | Parent; root? | M-W/X; W; S-W GetCritMultiplier | Strongly supported | W1 multiplier-only pair; values 1.0/2.0; never derive from critical damage |
| Stagger | Each DNAM.Stagger | Parent; root? | M-W/X; W; S-W | Strongly supported | W1 stagger-only pair; loaded getter, then repeated controlled behavior |
| Animation type | Each DNAM.AnimationType | Parent; root? | M-W/X; W; S-W GetWeaponType | Strongly supported | W2 sword/dagger pair and separately sword/staff control; getter plus equip/attack |
| Skill | Each DNAM.Skill (nullable -1 encoding) | Parent; root?; null case conditional | M-W/X; W; S-W GetSkill | Strongly supported for ordinary values; uncertain null | W2 OneHanded/TwoHanded and Z null-skill case; log skill gain independently |
| Equipment type | Each ETYP link | Parent; root?; absent-link case conditional | M-W/X; W; S-W GetEquipType | Strongly supported for ordinary links; uncertain absent | W2 distinct valid equip slots, Z absent child link; observe hand/equip behavior |
| Keywords | Each KSIZ/KWDA | Root replacement vs child vs union, possibly consumer-specific | M-W/X storage; W broad claim; S-F enumeration | Uncertain | K-W and T-W; enumerate and HasKeyword for disjoint sentinels; gameplay condition |
| Gold value | Each DATA.Value | Child, including zero? | M-W/X; W explicitly local | Strongly supported | L-W values 55/135 and child zero; distinguish base value from modified sale price |
| Enchantment | Each EITM link | Child; null fallback unresolved | M-W/X; W; S-W | Strongly supported for present child link | L-W distinct effects; Z child none/parent enchanted; getter and actual effect |
| Enchantment charge | Each EAMT UInt16 | Child; zero/null-enchantment behavior conditional | M-W/X; W; S-W | Strongly supported for present child enchantment | L-W 100/700; Z zero; fresh instance maximum/current charge separately |
| Scripts / VMAD | Each VMAD attachment/properties | Child-local candidate; parent execution unknown | M-W/X storage; W local exception | Uncertain | S-W parent-only/child-only/both tagged event scripts, fresh references and logs |
| Playability | Each major Non-Playable flag | Child vs inherited/conditional | X; S-F observation tool | Uncertain | F playable/nonplayable in both directions; loaded IsPlayable and inventory/equip |
| Special flags | Each header, DNAM.Flags/Flags2, CRDT.Flags separately | Conditional per bit; no blanket source | M-W/X; no bit-level runtime contract | Uncertain | F one bit at a time: CantDrop, bound evidence, critical-effect-on-death; test exact consumer |

The current snapshot/prototype does **not** extract critical multiplier. This is a future extraction gap, not a reason to change schema during this research. Scripts/VMAD booleans establish presence only, not script names, property values, attached instances or execution.

## 4. ARMO field matrix

| Property | Stored source | Proposed runtime source | Evidence | Confidence | Outstanding validation |
| --- | --- | --- | --- | --- | --- |
| Armor rating | Each DNAM, stored scaled integer / 100 | Parent; root? | M-A/X; A broad inheritance; S-A | Strongly supported | A1 30/60 pair, T-A; integer probes plus controlled physical damage |
| Weight | Each DATA.Weight | Parent; root? | M-A/X; A; S-F | Strongly supported | A1 20/5 pair; GetWeight/carry weight |
| Armor type | Each BODT/BOD2 type | Parent; root? | M-A/X; A; S-A GetWeightClass | Strongly supported | A2 Heavy/Light/None independently; structural getter vs keyword-based helpers |
| Biped slot mask | Each BODT/BOD2 mask | Parent; root? | M-A/X; A; S-A | Strongly supported | A2 masks 4 (32) vs 128 (37), matched ARMA controls; actual slot occupancy |
| Equipment type | Each ETYP | Parent; root?; absent conditional | M-A/X; A broad claim | Strongly supported for ordinary links; uncertain absent | A2 distinct valid equip-type pair; runtime native inspector plus equip observation |
| Keywords | Each KSIZ/KWDA | Replacement vs child vs union; separate ARMO rule | M-A/X; A broad claim; S-F | Uncertain | K-A and T-A; native HasKeyword, enumeration, worn-condition test |
| Gold value | Each DATA.Value | Child, including zero? | M-A/X; A explicit exception | Strongly supported | L-A differing/zero base values; avoid barter/enchantment display inference |
| Enchantment | Each EITM | Child; null fallback unresolved | M-A/X; A; S-A | Strongly supported for present child link | L-A distinct constant effects; Z none; getter and actor effect |
| Enchantment amount | Each EAMT UInt16 where present | Child-local candidate; runtime relevance conditional | M-A/X storage; A does not isolate it | Uncertain | L-A EAMT differing/zero with same enchantment; log native component and behavior; armor is not presumed to consume weapon charges |
| Scripts / VMAD | Each VMAD | Child vs parent vs both | M-A/X; A exception list incomplete for scripts | Uncertain | S-A tagged parent/child events; do not generalize WEAP result |
| Playability | Each major Non-Playable, legacy body flags where applicable | Conditional | X/M-A; no exact runtime contract | Uncertain | F both directions, header/body distinctions, Get/IsPlayable plus inventory/equip |
| Special flags / shield | Each header Shield and other relevant bits; body structure separate | Conditional per bit | X/M-A storage | Uncertain | F Shield independently from ArmorShield keyword/equip slot/model; drop/equip/block behavior |
| Armature | Each MODL list of ARMA links | Parent list replacement vs child list vs combination | M-A/X storage; A broad claim; S-A addon probes | Strongly supported parent candidate; list semantics uncertain | A3 GetNumArmorAddons/GetNthArmorAddon plus worn model by sex/race/first-person |
| World / worn models | Each MOD2/MOD4 world data; referenced ARMA supplies worn meshes/slots/races | Conditional relationship; parent candidate | M-A/X; A; S-A GetModelPath | Uncertain exact relationships | A3 dropped-world model separately from worn ARMA; compare male/female and compatible race |

World-model paths and armatures are absent from current snapshots. A visible mesh mismatch can arise from ARMA/race/sex/NIF slot data even if ARMO inheritance works. A GetModelPath result describes a world-model component, not necessarily the equipped mesh. SKSE GetArmorRating uses integer division: use whole-number ratings for first tests and a native inspector for fractional precision. Do not conflate Armor.IsHeavyArmor (a keyword helper) with GetWeightClass (structural data).

## 5. Keywords and functional behavior

No reviewed source proves replacement, child preservation or union for both record types or multiple levels. S-F's enumeration reads loaded keyword memory without implementing a template merge. That permits observation of initialization results but does not prove native HasKeyword, worn/perk conditions and crafting UI all consult exactly that array. Record their results separately.

For each WEAP and ARMO keyword test keep all stats, models, scripts and flags identical. Give root **R-only**, middle **M-only**, and child **C-only**, plus a shared keyword. Use distinct inert custom KYWD sentinels before testing material/gameplay keywords. Obtain this truth table for each consumer:

| Hypothesis for child | R-only | M-only (three levels) | C-only |
| --- | --- | --- | --- |
| Terminal-root replacement | true | false | false |
| Child preservation | false | false | true |
| Full-chain union | true | true | true |
| Direct-parent stored replacement | false | true | false |

Any other combination, disagreement between consumers, or load-order-dependent result requires a conditional policy. Two-level Argonian data alone cannot distinguish child preservation from union when all parent keywords are already stored on the child.

`000A7D:PrvtI_ArgonianWeapons.esp` adds `0C27BD:Skyrim.esm` (`MagicDisallowEnchanting`) beyond root `000825`. Root replacement drops it; child preservation/union retain it. Test native HasKeyword and the actual enchanting/disenchanting interaction independently. This keyword test requires a known compatible enchantment, the correct station action, an inventory instance and matched controls; otherwise an unrelated enchantment restriction can hide the result. Do not treat its name as proof of which UI action will be blocked.

Material identity and functionality must be separate findings. A `WeapMaterialSilver` identity does not demonstrate a silver-specific damage bonus, script attachment, perk condition, target immunity interaction or on-equip effect. Inspect the winning weapon VMAD, relevant perk/effect conditions and runtime execution before attributing behavior to the keyword. No silver behavior was tested or inferred here. Likewise preserve custom Argonian/polearm hints and special-item evidence until keyword consumers are validated.

## 6. Existing representative records

Rechecked the existing `Reports-v0.1c/audit.json` against the prototype. These are stored winning snapshots and proposed values, not fresh plugin reads or observations in Skyrim. FormKey origin is not the winning plugin.

| Case | What existing evidence establishes | Runtime question and pass criteria |
| --- | --- | --- |
| Riverwood dagger `0008B9:RiverwoodBandits.esp`, RWBAlfinDaggerFrost → `01399E:Skyrim.esm`, ElvenDagger | Stored damage 8 vs winning parent's 7; values 135/55. Parent winner is **Weapon Armor Attribute Tweaks.esp**, child winner RiverwoodBandits.esp. Proposal 7 is Unvalidated. | In the same complete load order, log both forms' GetBaseDamage and fresh instances. Parent-derived candidate predicts 7. Compare controlled physical hit behavior with an equivalent 7-damage control, isolating child frost damage. A result of 8 rejects this candidate for that environment; agreement only in UI is insufficient. |
| Dawnguard armor `015CB9:Dawnguard.esm` → `0142C7:Dawnguard.esm` | Stored rating 25 vs 28; both values 175. Parent winner **Weapon Armor Attribute Tweaks.esp**. Proposal 28 is Unvalidated. Vampire/Leather keyword conflict remains. | Log loaded rating for both forms; candidate predicts 28. Compare physical damage against matched unenchanted control with fixed skills/perks/tempering. Rating evidence does not resolve material conflict. |
| Argonian spear `000A7D:PrvtI_ArgonianWeapons.esp` → `000825:PrvtI_ArgonianWeapons.esp` | Damage 15/15 proves no inheritance choice. Child-only MagicDisallowEnchanting distinguishes replacement from preservation/union; custom material and polearm hints remain unresolved. | Log root/child enumeration and HasKeyword for every differing keyword, then enchanting interaction. Use disjoint synthetic K-W/T-W to distinguish preservation from union. No ordinary spear eligibility follows from resolving the chain. |
| CC staff `00081F:ccBGSSSE066-Staves.esl` → `000809:ccBGSSSE066-Staves.esl` | Both stored Staff animation, zero damage and staff keywords; successful chain. All prototype scenarios remain ExpectedOutOfScope. | Verify GetWeaponType, GetSkill, enchantment and cast behavior against parent. Matching values cannot prove inheritance; a controlled sword/staff difference is needed for that rule. Keep staves outside ordinary weapon balancing. |

RFM records in the reference dataset have no templates; they cannot validate inheritance. Winning-root provenance is especially important in the first two cases: inspecting only Skyrim.esm/Dawnguard.esm's original record could produce a different number than the report's parent candidate.

## 7. Controlled fixture specifications

**Future manual work only.** Use an isolated mod-manager profile and a disposable normal ESP, without ESL compaction, after review of this plan. The current milestone creates no plugin. Allocate fresh EditorIDs/FormIDs, retain a manifest, and never edit the user's original plugins. Use valid base meshes/equip links and avoid cycles/deleted/wrong-type chains in the engine; existing static tests cover those failures.

Start each experiment with root, templated child, and a nontemplated control matching each contradictory value. Change **one property per pair**; the overview values below must not become one inseparable experiment. Leave unrelated child/root fields byte-identical. Keep names distinct for identification.

| ID | Independent fixtures | Deliberate conflict / expected discrimination |
| --- | --- | --- |
| W1 | Separate damage, weight, speed, reach, critical damage, multiplier, stagger pairs | Root/child: damage 10/25; weight 12/2; speed 1.0/1.5; reach 1.0/1.8; critical damage 3/9; multiplier 1.0/2.0; stagger 0.5/1.0. Compare each child's loaded value and behavior to both untemplated controls. |
| W2 | Separate animation, skill, equip-type pairs | Sword/dagger animation while keeping keyword list fixed; OneHanded/TwoHanded skill; distinct valid equip links. Separate sword/staff test with appropriate controls, never include it in ordinary baselines. |
| A1 | Separate rating, weight pairs | Root/child rating 30/60, weight 20/5. Same type, slots, armature, keywords and enchantment. |
| A2 | Separate type, slot, equip-type pairs | Heavy/Light and Light/None structural types with identical keywords; slot 32 mask 4 vs slot 37 mask 128; valid distinct equip links. Match each nontemplated control's ARMA/NIF slots so rendering failure is not mistaken for slot inheritance. |
| K-W/K-A | Disjoint two-level keyword pairs for each type | Root Steel plus R-only; child Silver (WEAP) / Elven (ARMO) plus C-only; shared keyword on both. First run inert sentinels alone; then material keywords in separate copies. |
| T-W/T-A | Three levels for each type, independent scalar and keyword tests | Damage/rating root 10 or 30, middle 17 or 45, child 25 or 60. R/M/C-only keywords as separate test. Child → middle → root. Distinguish root, direct stored parent and local results. Add a later overriding plugin changing root alone; repeat with reversed declaration/FormID order to detect initialization-order effects. |
| Z | Null/zero siblings, each type separately | Zero damage/rating/weight/value/charge in independent cases; empty vs absent KWDA; absent child EITM with enchanted parent; absent vs explicit-zero EAMT; absent ETYP; WEAP skill -1. Compare absence and explicit zero; never interpret zero as missing automatically. |
| L-W/L-A | Local values/enchantments independent pairs | Distinct child/root names and values 135/55; distinct valid enchantments; weapon charge 700/100 with same enchantment in separate pair. Armor EAMT 700/100 is a storage/behavior question, not a presumed charge meter. Include root none/child enchanted and its reverse. |
| S-W/S-A | Script attachment cases per type | Root-only, child-only, both, neither; distinct script names/properties and tagged OnInit/OnEquipped/OnUnequipped logs on fresh references. No stat mutation in test scripts. Check startup and equip events separately. |
| F | Per-type and per-bit flag pairs | Root nonplayable/child playable and reverse; WEAP CantDrop separately; ARMO Shield separately from keyword/equip slot, plus matched shields. Log loaded flags where supported and exercise each bit's consumer. Do not union flags as an assumed engine rule. |
| A3 | Armature and world-model pairs separately | Different valid root/child ARMA lists with identifiable compatible meshes; separate distinct male/female world models. Test dropped and worn appearance, addon IDs, supported races/sexes, third/first person. Keep slot/type conflicts out of this pair. |

All scripts and plugin changes above belong to the future manual validation session. A separate probe quest may be used then; do not put probe attachments on equipment except in S fixtures, since doing so changes the property being measured.

## 8. Reproducible editor and runtime procedure

### A. Preserve and inspect stored values in xEdit

1. Record Skyrim executable version, CK, xEdit and SKSE versions; profile/load order, runtime DLLs, distributed keyword/stat tools, INIs and original plugin SHA256 hashes. First run a minimal official-master/SKSE/fixture profile, then the user's real profile. Record differences rather than combining results.
2. In xEdit inspect the complete winning override of each child and every parent. Record FormKey, runtime FormID, EditorID, defining/winning plugin, CNAM/TNAM, each field and all subrecords. Read DATA/DNAM/CRDT independently; record ARMO's raw scaled rating and displayed rating. Save screenshots or a text export and a manifest of expected child/root/control values.
3. For the future fixtures, create controls and contradictions, set CNAM/TNAM last, save, close xEdit, reopen and verify contradictions survived. Do not run a script that copies template data into children. Hash the finalized fixtures. An editor that normalizes values invalidates the intended contradiction; rebuild that case and document it.

### B. Check the Creation Kit separately

1. Load the future fixture as active in the disposable profile. Open each child and parent. Record disabled/editable controls, displayed values, keyword lists, template link and scripts. Record absent fields as absent, not defaulted.
2. Do not save this inspection over the canonical contradictory fixture. To investigate CK maintenance, use a separate copy/profile: save without intentional edits, inspect again in xEdit and compare bytes/fields. Then change the parent in that copy and repeat. Keep “editor display”, “editor saved values” and “runtime result” as separate columns.
3. Never call CK's greyed-out controls proof of runtime copying. Run Skyrim against the xEdit-verified contradictory fixture, not an inadvertently normalized CK copy.

### C. Capture loaded forms and actual consumers in Skyrim

1. Fully exit Skyrim between fixture variants. Launch the recorded profile through its compatible SKSE loader and use a fresh disposable save/character. Avoid previously persisted script instances, renamed/tempered/player-enchanted items and runtime mutation setters. Record player skills, perks, active effects, difficulty and equipment. Disable automatic distributors in the minimal test; test their influence separately in the real profile.
2. Resolve exact forms by plugin-local ID using `Game.GetFormFromFile(localID, pluginName)` in a future read-only Papyrus probe. Assert casts and FormIDs are non-null and match the manifest before reading anything. Full console FormIDs depend on current load order, including FE light-plugin indexing; do not substitute a report FormKey directly into a console command.
3. For manual inventory setup use `help "unique fixture name" 4`, confirm IDs against xEdit, then `player.additem <fullFormID> 1`, `player.showinventory`, and `player.equipitem <fullFormID>`. Resolve ambiguities with the manifest; these setup commands do not inspect effective base stats. Probe every root, middle, child and nontemplated control before and after first instantiation/equip.
4. Log the following API results with FormIDs and test IDs. Use the matching installed SKSE script sources; confirm the methods compile for that version. Emit Debug.Trace to Papyrus logs, with logging enabled in the disposable profile. Preserve the complete logs, not only the expected lines.

| Object | Source-verified observation methods | Limits / supplementary method |
| --- | --- | --- |
| WEAP | GetBaseDamage(), GetWeight(), GetSpeed(), GetReach(), GetCritDamage(), GetCritMultiplier(), GetStagger(), GetWeaponType(), GetSkill(), GetEquipType(), GetEnchantment(), GetEnchantmentValue(), GetTemplate() | [Weapon.psc](https://github.com/ianpatt/skse64/blob/25b72352adb6543fa6d0bd3795780672b2e238e0/scripts/modified/Weapon.psc), S-W. GetEnchantmentValue returns zero when no enchantment exists: it cannot distinguish absent EAMT from hidden stored charge. It is not remaining instance charge. |
| ARMO | GetArmorRating(), GetWeight(), GetWeightClass(), GetSlotMask(), GetEnchantment(), GetModelPath(false/true), GetNumArmorAddons(), GetNthArmorAddon(index) | [Armor.psc](https://github.com/ianpatt/skse64/blob/25b72352adb6543fa6d0bd3795780672b2e238e0/scripts/modified/Armor.psc), S-A. No Armor.GetArmorType or Armor.GetEquipType was established in these sources; use GetWeightClass and a version-compatible native inspector for equip slot/EAMT/header bits. |
| Both | GetNumKeywords(), GetNthKeyword(index), GetKeywords(), native HasKeyword(explicitKeywordForm), GetName(), IsPlayable() | S-F; enumerate every keyword FormID, not just labels. HasKeywordString delegates through keyword lookup; explicit forms avoid duplicate EditorID ambiguity. Enumeration and native predicate may disagree. |

5. Where no verified getter is available, require a documented, version-compatible read-only native inspection tool and record its version/method, or mark the field **not observed**. Gold value should be logged through a verified base-value API available in that environment; store sale-price/UI observations separately. Do not invent a console command or treat an unavailable probe as a passed case. VMAD execution requires tagged event logs; presence booleans are insufficient.
6. For keyword functionality, test a condition that queries each sentinel and compare with enumeration/HasKeyword. For armor, equip one piece and test a worn-keyword condition. For MagicDisallowEnchanting test the relevant station interaction with known working controls. Record perk/condition definitions and results. Material keywords alone do not prove functional effects.
7. For base damage/rating, compare against both nontemplated controls on the same fixed actor/target, difficulty, skills, perks, armor, resistances and active effects. Use fresh untempered, unenchanted items for isolated scalar tests. Observe target health before/after identical ordinary physical hits; exclude critical/power attacks and unrelated enchantment damage. For armor use identical incoming attacks. Displayed inventory numbers are corroboration only. Critical/stagger/speed/reach/animation require their own controlled consumers; randomness/animations must not be inferred from one trial.
8. Check weight with carry-weight change, slots with actual occupancy, armatures with addon IDs and world/worn views, playability/drop flags with their actions. An inconsistent loaded getter/gameplay outcome blocks the simple rule and requires investigation of the consumer or a runtime hook.
9. Repeat from a fresh process/save, then repeat in the real load order for the four representative cases. Verify input hashes, winners and runtime modules. Store a result row for **each field and consumer**: test ID, binary hash, raw child/direct/root values, probe result, gameplay result, repetition, environment, conclusion and unresolved caveats.

A future rule may be validated only for its documented field, record type, ordinary/absent/zero cases, depth and tested environment. A scalar result cannot validate keywords, scripts, flags or model lists. Contradictory results remain recorded; there is no automatic promotion step.

## 9. Static review, population implications and failure modes

Reviewed TemplateResolver, EffectiveEquipmentInvestigator, TemplateTests, historical investigation/prototype documents and the supplied replay. Traversal uses complete winning snapshots, rejects missing/wrong-type/deleted/cyclic chains, bounds parent edges at 128, checks cancellation and caches only completed results. Its terminal root is a graph fact; selecting that root for every stat is a separate unvalidated policy.

The prototype's per-field Unvalidated markers, detached hypothetical classifications and unchanged production gates are appropriate. Chain-wide unions of flags/script exclusions are conservative **auditor safeguards**, not claimed engine merges. Name is StoredOnly identity; other child-local proposals remain Unvalidated. No new static tests were necessary. Existing tests include synthetic contradictory numbers and keyword alternatives; they verify our implementation and cannot substitute for runtime evidence.

The report contains **6,095** templated children, **283** root families and **724** differing stored child/root keyword sets. These sets require consumer validation, not automatic replacement. Staff roots with many enchanted children show why counting each variant can overweight a design. After validation, start with a reviewed independent root/design identity, retain child provenance and separately retain meaningful functional variants. Do not deduplicate unrelated nontemplated designs because their stats match, or assume shared-root variants are functionally identical. Enchanted/scripted/nonplayable/bound/artifact items and unsupported custom types retain special exclusions until separately reviewed. No statistical baseline or anomaly calculation was added.

Remaining failure modes include CK normalization; stale snapshot winners; nullable vs absent vs explicit-zero subrecords; fractional rating loss in SKSE probes; per-instance enchantment/charge/tempering data; script persistence; runtime distributors or DLL changes; different HasKeyword/perk consumers; loader initialization order; per-field direct-parent versus recursive inheritance; flags located in different components; and ARMA/NIF/race/sex incompatibility. Failures of FormLink traversal are already excluded and must not be handed to the engine as experimental malformed fixtures.

## 10. Implementation recommendation and minimum manual gate

Ready to retain/use now: winning-record extraction, CNAM/TNAM traversal, provenance, stored field mappings/units, failure handling and deterministic raw/proposed reporting. These are static rules already implemented. **Ready production inheritance substitutions: none.** Parent-stat and child-local policies remain candidates; no schema/version/code change is justified by this research alone.

Minimum manual batches before trusting a broad production effective-equipment policy:

1. W1/W2 and A1/A2: independent contradictory scalar/structural tests for every field actually intended for production, with loaded-form probes and controlled consumers. A damage/rating pass does not approve the other fields.
2. K-W/K-A and T-W/T-A: disjoint keywords at two/three levels, scalar chains, later root override and declaration-order control; compare enumeration, HasKeyword and gameplay conditions.
3. L/Z for each type: value, enchantment and charge/amount, explicit zero and absent/null; verify no accidental parent fallback.
4. S/F for each type: scripts, playability and each relevant special bit in both directions. Unobserved flags/scripts must remain conservative exclusions.
5. A3: armature/world/worn relationships if the future policy claims model resolution; otherwise explicitly defer model effective values and retain their uncertainty.
6. Real load-order replication of the Riverwood, Dawnguard, Argonian and CC staff cases, including the **winning overridden parents**. Verify staff exclusion and custom/special boundaries remain intact.

For a smaller first release, validate only its declared subset and keep all other fields gated/unresolved. Keyword and classification inputs must still pass before admitting templates to comparable groups. Add field-specific evidence references and policy revisions only after reviewing the captured manual results; never silently replace `prototype-1` assumptions. Future critical-multiplier or armature extraction needs an explicitly reviewed schema change, separate from proving semantics.

Automated verification this milestone:

```powershell
dotnet test 'Equipment Balance Patcher.sln' --no-build --no-restore -m:1 --filter FullyQualifiedName~TemplateTests --verbosity minimal
```

Result: **14 passed, 0 failed, 0 skipped**, using existing built tests. These static tests create JSON/CSV reports only; no ESP/ESL fixture tests were run. No new tests were added and no CK/runtime tests were performed. The original report SHA256 remains `A1224A661EAF8365FF6E687396325A03D95BDAE9DBC2642DF22A37B59D4A30E7`.

Review this evidence and the manual results before proceeding to effective-stat implementation. Relative consistency within the load order remains the objective; changing global combat formulas, balancing, anomaly detection and COBJ processing remain outside this milestone.
