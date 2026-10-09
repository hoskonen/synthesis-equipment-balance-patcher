# First real-load-order report review

## Reference and method

The user supplied the four v0.1a reports in `Reports/`. Their JSON contains 9,095 winning snapshots. Reference `audit.json` SHA-256: `A1224A661EAF8365FF6E687396325A03D95BDAE9DBC2642DF22A37B59D4A30E7`. The reference files were preserved. `tools/ReplayAudit` reclassified those immutable snapshots into the separate `Reports-v0.1b/` directory. This is a reproducible report comparison, not a second game-load-order run, engine validation or GUI reproduction.

| Metric | v0.1a | v0.1b replay |
| --- | ---: | ---: |
| Weapons / armor | 4,422 / 4,673 | 4,422 / 4,673 |
| Confident / partial / unclassified | 5,865 / 938 / 2,292 | 6,540 / 936 / 1,619 |
| Direct templates | 6,095 | 6,095 |
| Enchanted / scripted | 6,632 / 281 | 6,632 / 281 |
| Baseline eligible | 595 | 699 |
| Statistics eligible | 1,339 | 1,202 |
| Unresolved reference occurrences | 0 | 0 |
| All diagnostics (including information) | 25,173 | 10,745 |
| UnmappedKeyword / PotentialClassificationKeyword | 12,603 / 0 | 0 / 47 |
| UnknownMaterial diagnostics | 2,407 | 154 |
| UnsupportedSlotCombination diagnostics | 1,710 | 22 |

Diagnostic reduction partly reflects explicit expected exclusions; it does not mean raw uncertainty vanished. Original keywords and classification reason arrays remain available. 2,579 records now have ExpectedOutOfScope scope, 6,438 have ordinary structural scope and 78 have unsupported scope. Scope and confidence answer different questions.

## Major causes and changes

Templates dominate: 3,423 weapons and 2,672 armor. Their stored child values may differ from parent values and all remain excluded from distributions and future comparisons. Enchantment also explains baseline exclusions, but those counts overlap templates, scripts and other reasons. Neither count can be subtracted independently to compute eligible totals. Enchantment alone continues to permit descriptive/future raw-stat analysis, while excluding ordinary baseline construction.

The initial warning stream included VendorItemWeapon (3,873 records), VendorItemArmor (3,062), VendorItemClothing (636), VendorItemStaff (499), VendorItemJewelry (442), Survival_ArmorWarm (507) and MagicDisallowEnchanting (460). These exact reviewed identities now have unrelated-keyword status. Other gameplay/distribution keywords remain raw and aggregate as OtherUnknown. In this replay, `keyword-summary.csv` has 181 keyword identities: 86 recognized classification, 15 recognized unrelated, 15 potential classification and 65 other unknown. A keyword may affect many records, so these are keyword counts, not equipment totals.

The largest missing armor categories were helmets: 440 playable heavy/light records occupied hair + circlet (mask 4098), and 209 occupied head + hair + circlet + ears (12291). The classifier now permits ears on the head profile and accepts the exact hair/circlet mask only with canonical ArmorHelmet and heavy/light structure. Unidentified circlets, extra unsupported slots, combined primary slots and contradictory shield evidence remain unclassified. Helmet count rises from 97 to 772; this includes records previously unclassified with other head combinations, including nonplayable inventory records. Increased category coverage does not automatically confer eligibility.

Clothing and jewelry are expected exclusions, identified by stored armor type and the exact ArmorJewelry key respectively. Nonplayable armor is identified by its flags, without guessing whether it is a creature skin, cosmetic prop or framework fragment. The dataset has 78 records defined in DF creature-pack plugins; all are nonplayable Clothing. They stay in inventory. Generic missing-material/unsupported-slot warnings are suppressed for expected exclusions, while raw reasons, broken references, structural conflicts and scripts remain visible. Staff animation is separately out of scope for ordinary weapon analysis; it does not prove every such record is a normal magical staff.

RFM defines or wins 302 records here: 276 confident, 13 partial, 13 unclassified in v0.1a. None has a template. Baseline reason occurrences include SpecialEquipment on 294 records, scripts on 46 and enchantment on 9; these overlap. A representative dagger `00387B:RFM.esm` has ordinary dagger/Steel evidence but CantDrop. `004345:RFM.esm` (Steel Spear) has Staff animation, OneHanded skill, damage 50 and CantDrop. Those boundaries remain conservative. No RFM-wide exclusion or material guess was introduced.

Unknown subtype hints now also cover quarterstaff, trident, glaive, scythe and OCF Pole1H/Pole2H markers. These are review gates, never mappings from names to a subtype. Eighteen Argonian weapons have an unreviewed PI_WeapMaterialArgonian key, sometimes alongside canonical Steel. Stored reviewed material remains Steel where unambiguous, but confidence is withheld pending material-tier review. This removes ten previously eligible baseline records. There are 114 newly eligible baseline candidates (61 helmets and 53 other pieces/weapons), yielding a net increase of 104. No script or template eligibility was loosened.

Statistics eligibility deliberately decreases despite higher confidence: confidently classified nonplayable and clothing records no longer enter ordinary distributions. Raw inventory and confidence remain independent; scripts, artifacts and bound/special weapons can still have stored descriptive groups when otherwise eligible, but remain barred from ordinary baseline/analysis.

## Reviewed material identities

New mappings were reviewed against **resolved keyword FormKeys and representative equipment snapshots in the supplied report**. They have not been independently checked against pristine Creation Club binaries; mods may alter keyword use. The runtime catalog matches only FormKey, case-insensitively, never item or keyword EditorID.

| Plugin | Local keyword ID | Reported keyword | Material group |
| --- | --- | --- | --- |
| Update.esm | 003277 | ccBGS_ArmorMaterialOrcishLight | OrcishScaled (family Orcish) |
| ccBGSSSE025-AdvDSGS.esm | 21BD63 / 21BD64 | ArmorMaterialGolden / ArmorMaterialDark | GoldenSaint / DarkSeducer armor |
| ccBGSSSE025-AdvDSGS.esm | 21BD65 / 21BD66 | ArmorMaterialMadness / ArmorMaterialAmber | Madness / Amber armor |
| ccBGSSSE025-AdvDSGS.esm | 21BD67 / 21BD68 | WeapMaterialAmber / WeapMaterialMadness | Amber / Madness weapons |
| ccBGSSSE025-AdvDSGS.esm | 21BD69 / 21BD6A | WeapMaterialGolden / WeapMaterialDark | GoldenSaint / DarkSeducer weapons |

Only explicit base/variant pairs can coexist without conflict: Orcish/OrcishScaled, Elven/ElvenGilded, Iron/IronBanded, ImperialLight/ImperialStudded. They select the specific material group and retain family/variant metadata; sibling tiers remain separate and conflicting sibling evidence stays unavailable. Vampire + Leather remains a conflict, since its dual use has not been validated as a tier relationship. Generic family metadata is not used to merge distribution groups.

The four Silver Armor records `000800`–`000803:ccBGSSSE056-BA_Silver.esl` contain warmth/type/category/vendor keywords but **no material keyword**. Their names do not justify a Silver material mapping. Mod-added Argonian material remains a review candidate. Missing or conflicting data is not repaired by maximizing coverage.

## Reproduce and follow up

```powershell
dotnet run --project tools/ReplayAudit/ReplayAudit.csproj -- Reports/audit.json Reports-v0.1b
```

The tool rejects output into the reference report directory and accepts schemas 1 and 2. It loads no plugins. Compare all five replay outputs across repeated runs, then perform the README's real-load-order validation. Use the template investigation's field-validation gates to choose the next milestone; report replay alone cannot validate effective stats.
