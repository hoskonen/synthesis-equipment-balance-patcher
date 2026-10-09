# Equipment template investigation (v0.1b)

## Evidence and limits

The copied v0.1a report contains 6,095 direct templates: 3,423 WEAP and 2,672 ARMO. All 302 records defined by or won by RFM.esm in this report have **no direct template**. RFM is therefore useful for studying special/custom weapons, but this dataset supplies no RFM template chain to validate. Do not attribute its exclusions to inheritance.

Mutagen 0.54.4 exposes WEAP `IWeaponGetter.Template` as the typed CNAM link and ARMO `IArmorGetter.TemplateArmor` as the typed TNAM link. Its [Weapon schema](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.4/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Weapon.xml) separates DATA (value, weight, damage), DNAM (animation, speed, reach, skill, flags, stagger), CRDT, KWDA, EITM and EAMT. Its [Armor schema](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.4/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Armor.xml) separately exposes DATA, armor-rating DNAM, body template, keywords and enchantment. **BodyTemplate is a biped/armor-type subrecord, not a parent-equipment template.** Neither record has the NPC template inheritance flag system.

The [generated weapon getters](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.4/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Weapon_Generated.cs) read local record data and expose a template link; the property getters do not receive a load-order LinkCache and cannot transparently merge another record. This auditor uses those getters. Its damage, rating, weight, category structure, flags and keywords are **stored winning-child values**, not validated effective values. Matching child/parent values do not prove inheritance has already been applied. A synthetic test with differing child/parent damage and armor rating verifies that extraction retains the child values and excludes it from statistics.

## Inheritance model to validate

The practical model documented by a weapon patch author's [pre-enchant workflow](https://www.nexusmods.com/skyrimspecialedition/articles/11346) is that templates supply base equipment properties, while the child supplies its identity/name, enchantment, charge, value and scripts. The armor author's [Creation Kit tutorial](https://tesalliance.org/forums/index.php?%2Ftopic%2F7365-character-design-introduction%2F=) describes template armor as inheriting equipment properties while allowing the child name, value and enchantment to differ. These are first-hand author workflows, not a field-by-field specification for every runtime case. The English CK pages could not be retrieved during this investigation.

For WEAP, damage, speed, reach, weight, critical properties, stagger, skill, animation/type, equip type, models/sounds and classification keywords are candidates for inheritance. FULL/name, DATA/value, EITM/enchantment, EAMT/charge and VMAD/scripts must be treated separately as child-local candidates. For ARMO, rating, weight, biped/type/slots, keywords, models/armature and equip type are candidates for inheritance; FULL/name, value and enchantment remain child-local candidates. Armor EAMT, VMAD, playability/major flags and keyword replacement versus augmentation require explicit CK/runtime validation before enabling a resolver. Do not treat the broad “all other fields” description as proof of every flag or script rule. Engine behavior and mod-added runtime distribution can differ from an offline plugin view.

The safe proposed matrix is:

| Field | WEAP proposed source | ARMO proposed source | Validation gate |
| --- | --- | --- | --- |
| FormKey, EditorID, name, defining/winning plugin | Child | Child | Preserve record identity unconditionally |
| Value, enchantment | Child | Child | Verify null/zero behavior; do not fall back based on truthiness |
| Enchantment amount | Child | Child candidate | Validate armor charge behavior |
| Damage / armor rating, weight | Resolved parent equipment | Resolved parent equipment | Controlled CK/runtime fixtures |
| Speed, reach, critical, stagger, animation, skill | Resolved parent equipment | Not applicable | Verify independent subfields and nested chains |
| Keywords, equip type | Resolved parent candidate | Resolved parent candidate | Prove replacement/augmentation rules |
| Biped slots and armor type | Not applicable | Resolved parent candidate | Includes slots absent locally |
| Scripts and playability/special flags | Keep child raw; effective status unknown | Keep child raw; effective status unknown | Conservative exclusion across chain until validated |

This matrix is a design proposal. v0.1b implements **none** of these effective substitutions.

## Representative stored records

These examples come directly from the copied winning-record snapshots, not original plugin binaries or an in-game experiment:

| Child | Direct parent | Stored child / stored parent | Implication |
| --- | --- | --- | --- |
| `0008B9:RiverwoodBandits.esp` (`RWBAlfinDaggerFrost`) | `01399E:Skyrim.esm` | Damage 8 / 7; value 135 / 55 | Child damage can differ. Proposed inherited damage would be 7; retain child value independently. This is an inference pending engine validation. |
| `015CB9:Dawnguard.esm` | `0142C7:Dawnguard.esm` | Rating 25 / 28; value 175 / 175 | Rating extraction currently describes stored child data. |
| `000A7D:PrvtI_ArgonianWeapons.esp` | `000825:PrvtI_ArgonianWeapons.esp` | Damage 15 / 15; child's keywords also include MagicDisallowEnchanting | Equal stats do not establish effective keyword semantics. Custom polearm hints must survive investigation. |
| `00081F:ccBGSSSE066-Staves.esl` | `000809:ccBGSSSE066-Staves.esl` | Staff animation; stored damage 0 | Even resolved templates may remain outside ordinary weapon analysis. |
| `00387B:RFM.esm` (`aaa_Blades__Dagger`) | None | Damage 8; CantDrop | Special-flag eligibility issue, not a template issue. |
| `004345:RFM.esm` (`Steel Spear`) | None | Staff animation, OneHanded skill, damage 50, CantDrop | Engine animation does not establish ordinary spear/staff combat semantics. No ordinary subtype is assigned. |

## APIs and proposed resolver architecture

Use the Synthesis state's winning load order and its `ILinkCache.TryResolve<IWeaponGetter>` / `TryResolve<IArmorGetter>` (or typed FormLink resolution extensions) to retrieve a parent. Use `TryResolveContext` when provenance is needed. `WinningContextOverrides(includeDeletedRecords: true)` already establishes child winners. Resolution locates the winning record; it **does not compute effective equipment fields**. The inspected pinned record schemas, custom/generated implementations and Skyrim extensions expose no reviewed complete WEAP/ARMO effective-template resolver. NPC template helpers, FormLink resolution and `DeepCopy` must not be mistaken for one.

1. Keep immutable raw snapshots exactly as now. Add a separate `EffectiveEquipment` DTO with per-field value, source FormKey, source winner, rule/version and resolution status. Never rewrite raw snapshots or live Mutagen records.
2. Resolve same-record-type parents against the winning cache. Maintain a visited `(recordType, FormKey)` set, cancellation checks, an explicit depth bound (proposed 128), and the full trace. Use a completed-result memoization cache scoped to one load order; avoid caching a partially traversed cyclic result as success.
3. Reject self-links, cycles, missing/wrong-type links, deleted winning parents and exceeded depth. Return an incomplete effective view with structured failure reasons. Never silently reuse stale overrides, default to zero, or promote child raw fields into “effective” fields.
4. Apply a versioned **per-field** inheritance policy, not a generic child/parent object merge. Resolve parent effective values recursively only for fields approved for inheritance; retain the original child identity and local fields through a multi-hop chain. Null and zero are legitimate local values where validated.
5. Separate chain resolution from classification. Classify the effective view only when all required field rules are validated. Preserve unresolved custom subtype/material evidence and conservative script/special exclusions across the chain.
6. Add a dry-run comparison report first: raw versus proposed effective values, trace, provenance and changed category/material. Continue excluding templates from baselines until that report is validated on representative CK/runtime fixtures and this load order.

Future statistics could include validated templated equipment in separate enchanted/unenchanted groups. Enchanted children may become raw-stat analysis candidates but must remain excluded from ordinary unenchanted baselines. Many children sharing one root would overweight a baseline; choose root-based sampling or deduplication explicitly. Resolving templates must not automatically make every one of the 6,095 records eligible.

## Required validation cases and unresolved risks

Test nontemplated records; one parent; three-hop chains; parent overrides from later plugins; null links; missing/wrong-type/deleted parents; self-link; two-node and longer cycles; depth/cancellation; shared-root memoization; and deterministic ordering. Use contradictory child/parent stored damage, weight, rating, keywords, animation/skill, slots/type and equip type. Test local zero value, null enchantment, differing charge, scripts on child/parent, and nonplayable/bound/artifact evidence at every level. Preserve input/output plugin hashes.

The unresolved engine gates are exact keyword semantics, armor scripts/charge, flag/playability inheritance, template chains with conflicting stored fields, and mod/plugin/runtime changes to normal CK behavior. Reports alone cannot settle them. CK comparisons and small controlled in-game tests should establish these rules before implementing effective calculations. Next milestone: a separately reported template-resolution prototype with provenance and validation gates, followed by renewed classification/eligibility coverage review. Anomaly detection and balancing should wait for that evidence.
