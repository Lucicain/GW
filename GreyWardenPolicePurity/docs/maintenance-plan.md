# GreyWarden Maintenance Plan

## Goal

Improve maintainability without changing gameplay behavior:

- Reduce behavior classes that mix dialogue, AI, state, and persistence.
- Reduce cross-file coupling caused by runtime static state and timing-sensitive logic.
- Centralize IDs, tuning values, and text ownership.
- Make hidden state machines easier to read and extend.

## Phase 1

Split dialogue responsibilities out of `PoliceEnforcementBehavior`.

Status:
- Completed

Done:
- Moved enforcement dialogue registration, dialogue conditions, and dialogue consequences into a separate partial file.
- Kept enforcement state progression and punishment flow in the core behavior file.
- Preserved existing gameplay behavior.

## Phase 2

Split dialogue and notification responsibilities out of `PlayerBountyBehavior`.

Status:
- Completed

Done:
- Moved recruitment dialogue, bounty reward dialogue, and map-notification flow into a separate partial file.
- Kept bounty state progression, escort AI control, and quest recovery in the core behavior file.
- Cleaned a batch of low-risk nullable warnings in the touched bounty files.

## Phase 3

Centralize shared IDs, tuning values, and text keys.

Status:
- Completed

Scope:
- Introduce `GwpIds` for hero, clan, item, party, and text keys.
- Introduce `GwpTuning` for reputation thresholds, cooldowns, rewards, and timing values.

Done:
- Added `GwpIds`, `GwpTuning`, and `GwpTextKeys` as shared constant entry points.
- Replaced scattered literals in core bounty, enforcement, patrol, resource, lore, and submodule files.
- Reduced warning count further while touching those files.

## Phase 4

Unify runtime state access.

Status:
- Completed

Scope:
- Add a single runtime entry point for `CrimePool` and `PlayerBehaviorPool`.
- Centralize new-game init, load recovery, and session reconnect behavior.

Done:
- Added `GwpRuntimeState` as a thin runtime facade over `CrimePool` and `PlayerBehaviorPool`.
- Moved new-game reset and player-behavior load/save flow behind the unified runtime entry point.
- Switched core bounty, lore, patrol, and enforcement behaviors to read runtime state through the facade.

## Phase 5

Make core state machines explicit.

Status:
- Completed

Scope:
- Introduce explicit enums for enforcement, bounty, and atonement flows.
- Reduce branching built from `bool + string + null` combinations.

Done:
- Added shared flow enums for atonement, bounty, and police task states.
- Replaced core enforcement and bounty branch checks with explicit state helpers.
- Added a computed `PoliceTask.FlowState` so escort, war, and pursuit paths are easier to read at call sites.

## Constraints

- Each phase must compile before moving on.
- Prefer structural refactors before gameplay changes.
- Keep refactors incremental so in-game regression testing stays practical.

## Player-facing release log rule

- Every player-visible gameplay, balance, feedback, compatibility, or content
  change must update both `_Module/README.md` and `_Module/README_EN.md` in the
  same change.
- Both READMEs retain exactly the newest two formal release entries, newest
  first. An `r5` package therefore ships the `r5` and `r4` logs; publishing
  `r6` replaces the oldest entry so the files contain `r6` and `r5`.
- Fold all development iterations for one upcoming version into that version's
  single entry. Each formal entry states its comparison baseline and uses short
  added/adjusted and fixed lists.
- Write from the player's point of view and summarize meaningful outcomes.
  Omit formulas, internal trigger counts, implementation details, test history,
  and exact values that are not required for a player decision. Do not add an
  unchanged-behavior section.
- Do not hide player-visible consequences behind implementation terms such as
  callbacks, patches, synthetic blows, private fields, or diagnostic events.
- Keep developer-only build, asset-publishing, and debugging procedures in this
  maintenance document rather than in the release notes shipped to players.

## Working-directory/live-module synchronization rule

- During development, deployable files under `_Module` and their counterparts
  in `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`
  must be byte-for-byte identical before an in-game test is accepted.
- Git publication is a separate release step. The working tree may remain
  uncommitted during development; the local Git commit and GitHub are updated
  together only when the user requests an upload or release.
- Sync from the working directory to the live module after each runtime-file
  change, then verify SHA-256 hashes. The MSBuild `CopyModuleData` target is the
  canonical non-build copy path.
- Exclude editor-only `Assets`, `AssetSources`, and `RuntimeDataCache` from the
  normal-client live module. Live-only compiled `bin` and generated `Shaders`
  are not source-mirror violations.
- Keep the live module as the diagnostics-enabled development/test install.
  Formal player builds use a separate diagnostics-disabled output and must not
  overwrite the live test DLL.
- Update this maintenance document with material deployments, experiments,
  failures, conclusions, asset locations, and rollback information as work
  proceeds; do not rely on chat history.

## 2026-07-16 Bannerlord 1.4.7 startup-error isolation

- A later controlled launcher comparison materially narrowed the shutdown
  failure. The Chinese-site Mod Manager writes
  `bin\Win64_Shipping_Client\ModMasterStarter.bat` and directly launches
  `Bannerlord.exe` with `/anticheat`. It also orders the official modules as
  `Native, SandBoxCore, CustomBattle, Sandbox, StoryMode, BirthAndDeath,
  FastMode`, rather than the official launcher's observed order
  `Native, SandBoxCore, BirthAndDeath, CustomBattle, FastMode, Sandbox,
  StoryMode`.
- Two otherwise GreyWarden-only manager-style runs, PID `9752` at 20:59 and PID
  `32964` at 21:07, used the reordered list plus `/anticheat` and crashed during
  native shutdown in `TaleWorlds.Native.dll` with `0xc0000005` after
  `Managed Interface deleted`.
- Two later official-style runs, PID `28696` at 21:16 and PID `39268` at 21:20,
  used the official module order without `/anticheat` and produced no Windows
  Error Reporting crash. Both printed a non-fatal `Non-Zero Device Reference
  Count` line (`ERC1513` and `ERC1567` respectively).
- Therefore the immediate reproducible difference is the launch command built
  by the Chinese-site manager, specifically its module ordering and/or forced
  `/anticheat` flag. Do not attribute this comparison to the shield LOD package
  without a separate controlled test. For current testing, prefer the official
  launcher. If further isolation is required, vary module order and
  `/anticheat` one at a time while leaving all module files unchanged.

- The startup failure was not caused by the `Useful Skips` assembly itself.
  Disabling only `UsefulSkips` left `Bannerlord.MBOptionScreen` (MCM) enabled,
  so the same failure remained.
- `LauncherData.xml` initially had MCM `v5.12.1` selected while its declared
  dependencies ButterLib `v2.11.0` and UIExtenderEx `v2.13.2` were not
  selected. That was an invalid module selection, but it was not the whole
  cause: a controlled retry enabled Harmony, ButterLib, UIExtenderEx, and MCM
  in the declared order while leaving Useful Skips absent.
- The complete dependency-stack retry on build `117484` still failed at
  20:47:36. The exact fault was an `IndexOutOfRangeException` in
  `Bannerlord.ModuleLoader.SubModuleWrappers.Patches.MBSubModuleBasePatch.Enable`,
  invoked by `Bannerlord.ModuleLoader.Bannerlord_MBOptionScreen..ctor`. All four
  module assemblies had loaded successfully before this constructor failed.
  Therefore the immediate cause is MCM's bundled module-loader patch being
  incompatible with Bannerlord `1.4.7`, not a missing prerequisite and not
  Useful Skips.
- The installed MCM package contains game-version adapters through
  `Bannerlord.MBOptionScreen.v1.4.5.dll` but no `v1.4.6` or `v1.4.7` adapter.
  Treat MCM `v5.12.1` as not yet compatible with game build `117484` even
  though it is the newest installed Workshop release.
- A controlled build `117484` run at 20:42:59 loaded only Harmony, the official
  single-player modules, and GreyWarden. It loaded
  `GreyWarden/AssetPackages`, reached `GauntletInitialScreen` at 20:43:21, and
  produced no `rgl_log_errors` entry or GreyWarden assertion. This isolates the
  immediate startup failure to the MCM dependency stack rather than GreyWarden
  or Useful Skips.
- Current local isolation state in
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Configs\LauncherData.xml`:
  Harmony, MCM, ButterLib, UIExtenderEx, ExceptionSentry, and Useful Skips are
  disabled; official modules and GreyWarden remain enabled. The pre-isolation
  launcher configuration is retained at
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Configs\LauncherData.xml.before-usefulskips-fix-20260716-2040.bak`.
- Separate earlier build-117377 messages about `gw_leader_*` hair/tattoo tags
  and obsolete `civilian="true"` equipment syntax were GreyWarden XML schema
  assertions, not the later startup stop. Do not conflate those content
  assertions with the MCM dependency-resolution failure.

## Release packaging and asset layout

### Player archive

- Public artifact name: `GreyWarden-v1.4.7.zip` with a sibling SHA-256 file.
- Create archives only during a formal GitHub push/release task. Ordinary
  `dotnet build` runs must compile and synchronize the live module without
  creating, refreshing, or copying any ZIP.
- The local formal-release output directory is the game's module parent:
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules`. The ZIP and
  checksum sit beside the `GreyWarden` directory, never inside the live
  `Modules\GreyWarden` directory and never inside repository `_Module`.
- The ZIP must contain exactly one top-level `GreyWarden` directory.
- Include only runtime data: `AssetPackages`, `bin/Win64_Shipping_Client`,
  `GUI`, `ModuleData`, `ModuleSounds`, `Shaders`, `README.md`, and
  `SubModule.xml`.
- Exclude `Assets`, `AssetSources`, `RuntimeDataCache`,
  `bin/Win64_Shipping_wEditor`, PDB files, source FBX/PNG files, and diagnostic
  logs/dumps.

### Required TPAC files

- `AssetPackages/gwp_inherited_legacy_assets.tpac`
  - Size: `332,944,246` bytes
  - SHA-256: `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - Contains the inherited armour, weapons, ordinary shield, materials,
    textures, and the original shield physics shapes.
- `AssetPackages/gwp_black_gold_shield.tpac`
  - Size: `37,594,977` bytes
  - SHA-256: `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
  - Contains `wlarge_shield_black_static`, `gwp_black`, and the three
    black-and-gold textures.
- Both files are intentionally ignored by Git because the inherited package is
  larger than GitHub's normal file limit. A distributable is not complete until
  both hashes pass.

### Modding Kit publication order

The Modding Kit clears the live `AssetPackages` directory and writes only
`pack0.tpac`. After every shield publication:

1. Ensure the Modding Kit has fully exited.
2. Preserve the new `pack0.tpac` immediately.
3. Rename it to `gwp_black_gold_shield.tpac`.
4. Copy the renamed file to repository `_Module/AssetPackages`.
5. Restore `gwp_inherited_legacy_assets.tpac` beside it.
6. Verify both file sizes and hashes before launching the client.
7. Move live `Assets`, `AssetSources`, and `RuntimeDataCache` intact to sibling
   `_GreyWardenEditorWorkspace` before a client test.

Do not concatenate the two TPAC files. They are independent valid packages.

### Editor workspace parking and restoration

The editable resource directories are currently parked intact at:

`D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\_GreyWardenEditorWorkspace`

That directory is outside the live `GreyWarden` module and contains exactly the
three editor-only directories needed to resume asset work:

- `Assets`: editable generated TPAC metadata, including the current
  `GreyWardenRecovery/dun_geo.tpac`.
- `AssetSources`: the six-LOD shield FBX and three source textures. The current
  `GreyWardenRecovery/dun.fbx` is `218,316` bytes with SHA-256
  `8FC25976E9A6E5B0663A6462EB6BB2F0F59E73C14AE899671A510825AB63B6AC`.
- `RuntimeDataCache`: generated editor cache. It is movable with the workspace
  but is not an authoritative backup and may be regenerated if necessary.

To resume editing without opening or automating the editor on the user's behalf:

1. Confirm the game and Modding Kit are fully closed.
2. Move `Assets`, `AssetSources`, and `RuntimeDataCache` from
   `_GreyWardenEditorWorkspace` back into the live module root:
   `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.
3. Preserve both files in live `AssetPackages` before publishing; the Modding
   Kit will clear that directory and create a new `pack0.tpac`.
4. The user performs all Modding Kit/editor interaction. Do not control the
   editor for them.

Before normal-client testing or building a public archive:

1. Fully close the Modding Kit.
2. Move the same three directories back to
   `_GreyWardenEditorWorkspace`; do not split their contents across locations.
3. Confirm the live `GreyWarden` module has no `Assets`, `AssetSources`, or
   `RuntimeDataCache` directory, otherwise the client can prefer editable
   resources and ignore the complete runtime packages.
4. Restore and verify both runtime TPAC files using the sizes and hashes above.

Do not delete `_GreyWardenEditorWorkspace`. It is the current resumable editor
state. The inherited `gwp_inherited_legacy_assets.tpac` remains the authoritative
irreplaceable backup; the editor workspace does not replace it.

## Solved: black-and-gold shield shutdown failure

### Player symptom

- The black-and-gold lord shield rendered correctly, but some game exits ended
  after `Managed Interface deleted` with `0xc0000005` in
  `TaleWorlds.Native.dll`.
- The failure was intermittent, so absence of a visible dialog was not enough;
  every acceptance run also checked Windows Application/WER events and dumps.

### Important findings

- Two TPAC files, their external filenames, inherited collision bodies, texture
  names, and old/new TPAC coexistence were not sufficient causes.
- Runtime retrieval and mutation of weapon meshes/materials was unsafe during
  native teardown. The release therefore uses a statically authored lord-only
  metamesh and never recolours or swaps its material at runtime.
- Reusing a repeatedly edited generated `dun_geo.tpac` produced stale editor
  state. `Ignore`/`Apply Ignores` and editor shutdown could then fault in native
  code even though the FBX geometry was valid.
- The successful rebuild was made after the editor fully exited and the old
  generated `dun_geo.tpac` was removed. Reimporting the unchanged FBX created
  new package, geometry, and metamesh GUIDs. The model, material, import settings,
  and FBX checksum remained unchanged.
- Modding Kit shutdown can still use a different native teardown path from the
  normal client. Judge the player release only by normal-client runs; keep
  editor crashes documented separately.

### Final release state

- Lord item `wlarge_shield_black` directly references the static
  `wlarge_shield_black_static` metamesh and inherited
  `bo_cap_wlarge_shield` / `bo_wlarge_shield` physics shapes.
- The shield has six complete LODs, all bound to `gwp_black`:
  - LOD0: 1,360 vertices / 1,642 faces
  - LOD1: 1,142 / 1,312
  - LOD2: 807 / 821
  - LOD3: 413 / 328
  - LOD4: 208 / 164
  - LOD5: 104 / 82
- Offline checks found no invalid index, bad face reference, degenerate face, or
  non-finite position/normal/UV/tangent value.
- Normal-client processes `22448` and `31172` both loaded
  `GreyWarden/AssetPackages`, rendered `wlarge_shield_black`, completed a
  battle, reached `Managed Interface deleted`, and passed delayed Windows
  Application/WER/dump checks with no TaleWorlds crash.
- `Non-Zero Device Reference Count` (`ERC...`) may still appear on successful
  exits. It is not a failure unless Windows also records an application crash.

## Correct shield LOD workflow

### Blender/FBX

- Put the six static mesh objects in one FBX. A dummy/empty parent is not
  required.
- Required names:
  - `wlarge_shield_black_static`
  - `wlarge_shield_black_static.lod1` through `.lod5`
- All objects must have the same origin and applied transform, one material slot,
  `gwp_black` assigned to every face, valid UVs, and decreasing polygon counts.
  LODs do not need identical vertices or topology.
- Export only the intended six mesh objects with `Selected Objects`, object type
  `Mesh`, positive Y forward, and Z up.
- `Selected Objects` is the actual inclusion rule: all six objects, including
  the unsuffixed LOD0/base mesh, must be selected when the export starts. Making
  the base mesh the last-selected/active object is a useful visual checklist,
  but Blender's FBX exporter does not require that object to be active and
  Bannerlord does not use Blender's active-object state.
- If making the base mesh active after selecting all six, use Shift-click so the
  other five remain selected. A normal click that leaves only the base selected
  produces a one-model FBX.
- The verified six-LOD FBX contains six independent root-level mesh nodes and no
  dummy/empty parent. The base mesh is present first, each `.lod<n>` node is
  present once, and the same `gwp_black` material is connected to every node.

### Bannerlord import

- The FBX dialog should report `Geometry(6) Model(6) Material(1)`.
- Treat those counts as the authoritative export-selection check. If they do
  not read six/six/one, cancel the import and correct the Blender selection or
  FBX contents instead of trying to repair the Meta Mesh afterward.
- Enable `Import meshes` and convert units to metres.
- Leave `Convert to Z-up`, skeleton, animation, morph, and physics-shape import
  disabled for this static shield.
- In Meta Mesh Editor verify LOD0-LOD5 and `gwp_black` on every LOD. `Divide Into
  Grid` is unnecessary. Do not recompute normals/tangents unless the source is
  known to require it.
- `Remove Redundant Vertices` may appear enabled by default. It was not the
  proven shutdown fix and should not be toggled repeatedly as a diagnostic.

### If the Meta Mesh Editor becomes unstable

1. Stop changing Ignore flags.
2. Close the editor completely.
3. Back up the current generated `Assets/.../dun_geo.tpac` for diagnosis.
4. Remove only that generated TPAC; preserve `AssetSources/.../dun.fbx`, the
   material, textures, and inherited package.
5. Start a fresh editor session and allow the FBX to generate a new resource.
6. Verify the published TPAC offline before replacing the stable runtime package.

## Common failure and recovery table

| Symptom | Cause | Recovery |
|---|---|---|
| FBX import reports zero/one mesh or LOD0-LOD5 are not all present | Not all six intended mesh objects were selected when `Selected Objects` export was used, or `Import meshes` was disabled | Re-export with all six selected; confirm the unsuffixed base plus `.lod1`-`.lod5`, then require `Geometry(6) Model(6) Material(1)` before importing |
| Only the black shield appears; inherited armour is missing | Client loaded live `GreyWarden/Assets` instead of `AssetPackages` | Exit the game and move `Assets`, `AssetSources`, and `RuntimeDataCache` to `_GreyWardenEditorWorkspace`; verify the next log says `Loading packages .../GreyWarden/AssetPackages` |
| Publishing removes all inherited equipment | Modding Kit cleared `AssetPackages` and created only `pack0.tpac` | Rename the new package, then restore the verified inherited TPAC before testing |
| Ignore/Apply Ignores or editor shutdown starts faulting | Stale generated resource/editor-session state | Fully exit the editor and regenerate `dun_geo.tpac` from the preserved FBX in a fresh session |
| Black shield is rotated/reversed | Wrong FBX forward-axis declaration or double Z-up conversion | Export positive Y forward/Z up and keep Bannerlord `Convert to Z-up` disabled |
| Game exit seems clean but reliability is uncertain | Native failure was intermittent and WER can be delayed | Require actual shield rendering, complete client exit, then delayed Application/WER/dump checks |
| Six-LOD package fails again | Runtime package regression | Restore the retained single-mesh shield TPAC and repeat the same acceptance test |

### Why the final six-LOD attempt succeeded

- Correctly selecting and exporting all six meshes explains why the final FBX
  exposed a complete LOD0-LOD5 set. It can also explain earlier imports that
  contained no mesh, only one mesh, or an incomplete LOD group.
- It does not explain the later native editor/client shutdown fault. The FBX
  that faulted and the FBX that succeeded after regeneration had the same
  checksum and import contents. The change that separated those attempts was a
  fully closed editor plus regeneration of the stale generated resource, which
  produced fresh package/geometry/metamesh identifiers.
- It also does not explain the test where only the shield appeared. That client
  loaded the editable `Assets` directory instead of the two complete packages
  in `AssetPackages`, so the inherited armour package never entered that run.
- Rigged equipment is different: its FBX must include the required mesh and
  skeleton/armature data. Tutorials that select both and make the armature
  active are not evidence that a static shield's base mesh must be active.

## Legacy package recovery

- Canonical recovery directory:
  `C:\Users\lucif\Documents\GreyWarden旧资源恢复\pack0_2026-07-15`.
- Package GUID: `cec987dc-80fc-47dd-9865-6fe9e9274db3`.
- Inventory: 20 metameshes, 18 materials, 33 textures, and 2 physics shapes.
- Models and textures were exported for reconstruction, and raw/external data
  was retained. Common formats cannot recreate the original physics shapes, so
  keep the inherited TPAC itself as the authoritative backup.
- The inherited package is irreplaceable; the black-and-gold shield package is
  reproducible. Never delete or overwrite the inherited package during shield
  publication.

## Formal release checklist

1. Build the diagnostics-enabled `Release` against Bannerlord `1.4.7` and keep
   it in the live test module.
2. Confirm the live module has no `Assets`, `AssetSources`, or
   `RuntimeDataCache` directory.
3. Confirm both runtime TPAC hashes above.
4. Confirm both player READMEs describe functions/results only, retain exactly
   the newest two formal versions, and match their live copies.
5. Build a diagnostics-disabled player DLL into a separate staging directory
   with live deployment disabled. Decompile it and confirm all diagnostic write
   methods are inert and no test log can be created.
6. Stage one top-level `GreyWarden` directory without `tools`, scripts, logs,
   developer notes, editor binaries, PDBs, or source assets.
7. Commit and push the release source and documentation to GitHub as part of
   the same formal release task.
8. Create the versioned ZIP and its `.sha256` file directly under the
   game's `Modules` directory, never under `Modules\GreyWarden` or `_Module`.
9. Inspect ZIP paths, verify the packaged DLL hash equals the separate player
   build, and confirm no diagnostic/test content exists; then create/update the
   GitHub release and upload the matching ZIP and checksum.
10. Run at least one battle that renders the black shield, exit the client, and
   check delayed Windows Application/WER/dump state.

# 2026-07-16 English-base and Simplified-Chinese localization

## Scope and language architecture

- The module now treats English as the source-language fallback. Every
  player-visible C# string has a stable `{=gwp_*}` key and an English default;
  static XML names and descriptions use the same keyed-English pattern.
- Simplified Chinese is supplied by:
  - `_Module/ModuleData/Languages/CNs/language_data.xml`
  - `_Module/ModuleData/Languages/CNs/std_gwp_strings_xml-zho-CN.xml`
- The CNs table contains 507 keyed entries covering items, troops, clan and hero
  names, dialogue, quest text, encyclopedia text, map notifications, enforcement
  status, debugging UI that players can open, and dynamically formatted text.
- `GwpText.cs` is the single code-side localization boundary. It creates a
  `TextObject`, binds named variables such as `{VAR_1}`, and returns the text in
  the current game language. Named variables were used instead of interpolating
  a finished sentence so English and Chinese can change word order safely.

## Voice and terminology

- Core lore, greetings, troop requisition, bounty recruitment, atonement,
  arrest dialogue, adopted-heir encyclopedia text, and deterrence reactions were
  manually rewritten rather than accepted as raw machine translation.
- The English register is restrained and old-Imperial, not pseudo-Shakespearean.
  Canonical terms include:
  - `Grey Wardens` for 灰袍守卫
  - `the old, undivided Empire` for 统一帝国
  - `constabulary house` or `order` for the police-family institution
  - `provost patrol` / `provost` for 纠察队 / 纠察官
  - `amendment`, `atonement`, `inner ward`, `case rolls`, and `lawful fine`
- The six founders now have separate localization keys instead of the former
  shared `gw1` key. Generated female heirs use stable romanized English names
  and their original Chinese names in CNs.

## Save compatibility

- Existing saves may contain plain Chinese crime labels, while saves made in an
  English session may contain the earlier English label. `GwpText.CrimeType`
  recognizes both forms and renders the canonical label in the current game
  language wherever crime records are shown.
- Generated Grey Warden heirs are deterministically renamed by their stable hero
  ID during the existing family-presentation refresh, so old saves receive the
  current language's name set without changing which identity was selected.
- No save fields or saveable type identifiers were removed or renumbered.

## Validation evidence

- Release build succeeds against Bannerlord 1.4.7 with 0 compiler errors. The
  remaining nullable warnings pre-date localization and do not block output.
- Roslyn scan after conversion found 0 CJK-bearing C# string literals outside
  the CNs language table. Chinese source comments remain developer-only.
- Localization integrity check found no duplicate keys with differing values,
  no missing CNs entries (excluding TaleWorlds' special `{=!}` non-localized
  barter placeholder), and no English/Chinese named-placeholder mismatch.
- All shipped XML files parse successfully. The 1.4.7 civilian-equipment and
  hero-appearance schema repairs remain present.
- Ordinary `Release` build copied the bilingual module data and README to
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.
  Runtime acceptance still requires checking both English and CNs in the normal
  client and confirming that the log loads `AssetPackages`, not the editor tree.
- After the working-directory/live-module synchronization rule was made
  mandatory, the runtime mirror was copied again with the `CopyModuleData`
  target and verified by SHA-256: all `25` deployable `_Module` files matched
  their live counterparts, the Release DLL matched the live client DLL, all
  `13` live XML files parsed successfully, the README matched, and the live
  module contained none of `Assets`, `AssetSources`, or `RuntimeDataCache`.

# 2026-07-16 returning-patrol interception crash and encyclopedia button

## Reproduction evidence and root cause

- The English encyclopedia screenshot showed the deterrence action label
  extending beyond a fixed `150`-pixel button. The Simplified Chinese label was
  shorter and remained inside the same frame, so this was a layout-width defect,
  not a missing or incorrectly selected localization entry.
- The latest client log was
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_27944.txt`.
  At `22:13:15`, the lawful-fine barter was rejected; at `22:13:36`, the player
  instead entered the smaller passage negotiation; and at `22:13:39`,
  `gwp_patrol_barter_post_success` accepted it. The patrol was dismissed and the
  encounter closed normally.
- At `22:14:00`, the player manually intercepted that returning patrol. The
  conversation character was a `Grey Warden Heavy Infantry` troop rather than a
  hero, and the log selected the native
  `default_conversation_for_wrongly_created_heroes` line. The crash dumper began
  immediately afterward. This isolates the crash from MCM, Useful Skips, the
  payment barter, and localization loading.
- A negotiated passage intentionally differs from payment of the lawful fine:
  it restores peace and grants four days of safe-conduct while leaving negative
  standing and the existing crime record in place. The user's remaining wanted
  state after paying about `300` was therefore expected; the later conversation
  fallback and crash were not.
- The former suppressed-meeting branch attempted to finish the encounter from
  inside the normal patrol dialogue condition and then returned `false`. During
  a manual interception this left the engine without an applicable GreyWarden
  start line, allowing it to fall through to the unsafe native line for the
  troop-led party.

## Repairs

- `PolicePatrolBehavior.OnSessionLaunched` now registers the high-priority
  `gwp_patrol_returning_start` dialogue before the ordinary enforcement start.
  It applies only to a GreyWarden patrol marked as returning or while patrol
  meetings are suppressed, gives a localized dismissal, and closes the player
  encounter through `GwpCommon.TryFinishPlayerEncounter`.
- `PatrolDialogCondition` no longer mutates or finishes `PlayerEncounter` from
  inside its condition when meetings are suppressed. It simply declines the
  ordinary enforcement branch, leaving the dedicated returning-patrol line to
  own the conversation.
- The encyclopedia deterrence button now uses `CoverChildren`, retains a
  `150`-pixel minimum width, allows up to `300` pixels, and gives its label
  `24`-pixel horizontal margins. This preserves the compact Chinese button while
  expanding the English button around its full label.
- The returning-patrol dismissal has English source text and a CNs entry under
  `gwp_patrol_returning_dialogue`; no save schema was changed.

## Validation and deployment

- Localization integrity after the repair is `507` English defaults and `507`
  CNs entries, with zero conflicting keys, duplicate translations, missing or
  extra entries, and named-placeholder mismatches. Every shipped XML file parses
  successfully.
- A full `dotnet build GreyWardenPolicePurity.slnx -c Release` completed with
  `0` errors and the existing `44` nullable-analysis warnings, then copied the
  module data and compiled assembly to the normal client and editor module
  directories.
- All `25` deployable source `_Module` files matched their live-module copies by
  SHA-256. The normal-client and editor assemblies matched at
  `755638561B8568A105CB6C496250A5CC0A017C8623EFFC1F6F0798F441A7ECD5`,
  and the repository/live player READMEs matched.
- The live module contained none of `Assets`, `AssetSources`, or
  `RuntimeDataCache`. The protected asset-package hashes remained unchanged:
  `gwp_black_gold_shield.tpac` =
  `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B` and
  `gwp_inherited_legacy_assets.tpac` =
  `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`.
- Final acceptance still needs an in-game retry of the exact sequence: reject
  the lawful fine, negotiate passage, catch the returning patrol, confirm its
  one-line dismissal, and verify that the encyclopedia button contains the full
  English label at the user's display scale.

## Follow-up after failed in-game retry at 22:34

- The first returning-patrol repair was incomplete. In
  `rgl_log_36420.txt`, the original enforcement meeting succeeded, the lawful
  fine was rejected at `22:33:52`, negotiated passage was accepted at
  `22:34:02`, and the first conversation ended normally at `22:34:06`.
- The player caught the patrol again almost immediately. At `22:34:07`, the
  new high-priority `gwp_patrol_returning_start` line was selected, proving that
  the native `default_conversation_for_wrongly_created_heroes` fallback had
  been eliminated. However, the log ended before `Conversation End`, and the
  crash dumper recorded an exception immediately afterward.
- The remaining defect was in the new line's consequence: its target was
  already `close_window`, but the consequence also called
  `GwpCommon.TryFinishPlayerEncounter`. That attempted a second encounter
  shutdown while the conversation engine was creating or closing the line.
  Therefore the first repair changed the crash location rather than completing
  the fix.
- The returning line now has no consequence and relies exclusively on
  Bannerlord's normal `close_window` flow. All suppression-time calls that
  force-finished `PlayerEncounter` from the hourly tick or map-event callback
  were also removed. Suppression now only selects the dedicated safe dialogue
  and prevents the ordinary enforcement dialogue from being selected.
- The first responsive-width button was also rejected visually in the in-game
  retry: its `CoverChildren`/`MaxWidth=300` layout produced a long, thin banner.
  The button has been restored to the original fixed `150 x 48` proportions,
  and the English button label is now the compact `Deterrence`. The longer
  explanatory wording remains in the hover hint and details window.
- The corrective Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. The deployed normal-client and editor assemblies
  match at SHA-256
  `47BFC370099AAFC607BF7E07BE8A1767ED31B51B93752D3DA13E0CC38033184C`.

# 2026-07-16 Old English names and split player READMEs

## Naming policy

- Direct romanizations of the former Chinese founder names were rejected. The
  six stable founder IDs use attested Old English feminine names only in
  English, while CNs preserves the original Chinese names:
  - `gw_leader_0`: `Aethelflaed` / `梵蒂`
  - `gw_leader_1`: `Cyneburh` / `约珥`
  - `gw_leader_2`: `Mildthryth` / `弥瑟`
  - `gw_leader_3`: `Wynflaed` / `圣铎`
  - `gw_leader_4`: `Eadgifu` / `晨曦`
  - `gw_leader_5`: `Wulfhild` / `暮光`
- The generated daughter/adopted-heir pool has two intentionally independent
  presentations on the same `36` stable keys. English uses charter-attested Old
  English spellings such as `Eadgyth`, `Ealhswith`, and `Leofgifu`; CNs restores
  the original pool beginning `澄音`, `祈安`, `望舒` and ending `书宁`, `凝光`,
  `夕晨` rather than phonetic translations of the English names.
- If the full pool is exhausted, English uses Roman numerals `II` through `X`;
  CNs preserves the original suffixes `二` through `十`.
- `RefreshPoliceClanFamilyPresentation` now reapplies keyed localized names to
  the six founders as well as deterministic names to generated members. This is
  required so existing saves adopt the new names when loaded; no new campaign
  is required and no save field was added or changed.
- Founder encyclopedia prose uses the appropriate displayed name in each
  language. Stable hero IDs and localization keys were deliberately kept
  unchanged for save and translation compatibility.

## Player documentation policy

- The former bilingual `_Module/README.md` was too detailed for players and has
  been replaced by two short files:
  - `_Module/README.md`: Simplified Chinese
  - `_Module/README_EN.md`: English
- Both contain the same installation, latest-update, playable-content, and
  contact information. Release notes now state only what was added or fixed;
  voice-direction rationale, implementation details, test history, and minor
  internal changes remain in this maintenance document only.

## Validation and deployment

- The English-default and CNs localization tables both contain `510` keyed
  entries. Validation found `0` conflicting defaults, duplicate keys, missing
  entries, extra entries, placeholder mismatches, or XML parse errors.
- The six founders and `36` generated-heir keys are complete in both languages.
  English and CNs deliberately use different name sets while sharing stable
  keys, so changing the game language selects the intended set.
- `_Module/README.md` and `_Module/README_EN.md` are each `38` lines. The
  English file contains no CJK text, and both live copies match their repository
  versions byte for byte.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. The normal-client and editor DLLs match at
  SHA-256
  `2BC151327BBFC490BF3F15B6A95507B059F3D56D2C50898C669CDA204B4A42F4`.
- All `26` deployable `_Module` files match the live module. The live module
  contains no `Assets`, `AssetSources`, or `RuntimeDataCache` directory. The
  protected asset packages remain unchanged at their hashes recorded above.

# 2026-07-16 village-relief encounter side repair

- Symptom: after choosing the custom option to help villagers resist an active
  raid, the next native `encounter` screen said that the player had arrived to
  plunder the village and warned that raiding would declare war on its faction.
- In-game verification clarified that the actual battle sides were already
  correct: the player fought beside the militia against the raiders. The defect
  was presentation-only, so replacing or rebuilding the map event would be an
  unnecessary and potentially disruptive repair.
- Cause: the custom option correctly calls
  `PlayerEncounter.JoinBattle(BattleSideEnum.Defender)`, but Bannerlord retains
  the raid event's standard hostile-village `ENCOUNTER_TEXT` when the generic
  `encounter` menu initializes.
- Repair: the direct defender join remains unchanged. A one-shot
  `AfterGameMenuInitializedEvent` override now replaces `ENCOUNTER_TEXT` and
  `ATTACK_TEXT` only for the next `encounter` menu opened by the custom village
  defense action. CNs tells the player that they have joined the militia to
  resist the raiders; English conveys the same meaning. The override then clears
  itself and cannot affect ordinary encounters or a choice to aid the raiders.

# 2026-07-16 release archive placement repair

- Symptom: every ordinary build appeared to recreate
  `Modules\GreyWarden\GreyWarden-v1.4.7.zip` and its checksum inside the live
  module, adding roughly `374 MB` of non-runtime data to the test installation.
- Cause: the formal `v1.4.7-r2` archive and checksum had been parked inside
  repository `_Module`. The build target did not generate them, but its broad
  `_Module\**` copy item treated them as ordinary runtime files and recopied
  them into the live module after deletion. Exact `.gitignore` entries hid the
  misplaced source artifacts from `git status`.
- Repair:
  - Moved the `v1.4.7-r2` ZIP and checksum to
    `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules`, beside the
    live `GreyWarden` directory. The ZIP SHA-256 remains
    `673FD72AE70A467C3336D7DDC1762AA2CDE8735BAB05A0464FD38BBF1C4CEE27`,
    matching the asset published in GitHub release `v1.4.7-r2`.
  - Removed both copies from repository `_Module` and live
    `Modules\GreyWarden`.
  - Removed the two exact archive ignores so any future archive accidentally
    placed in `_Module` becomes visible to Git.
  - Added defensive `*.zip` and `*.zip.sha256` exclusions to `CopyModuleData`.
    A normal build therefore cannot copy a release archive into the live module
    even if one is mistakenly placed under `_Module` again.
- Formal archives are now created only as part of a GitHub push/release task;
  no automatic packaging target was added to compilation.

# 2026-07-17 current archive and source push

- At the user's request, refreshed the local player archive as part of the same
  task that commits and pushes the current source, without creating a tag or a
  GitHub Release.
- Output remains beside the live module rather than inside it:
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip`
  - `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip.sha256`
- New archive size: `349,278,336` bytes. SHA-256:
  `98A1FDE4E6992675AB8CA9C463EC0A1028F04FD30092F142D03D07DCA9DBA6CC`.
- The ZIP contains one top-level `GreyWarden` directory and `28` runtime files.
  It includes both player READMEs, the current normal-client DLL, both protected
  TPACs, GUI, ModuleData/languages, sounds, shaders, and `SubModule.xml`.
- Validation found no editor tree, editor binary, PDB, nested ZIP, or diagnostic
  content. A full extraction produced `0` missing, mismatched, or extra files;
  the extracted DLL matches the live normal-client DLL. Protected TPAC hashes
  remain unchanged.
- This archive refresh is local only. No new GitHub Release is to be created;
  GitHub receives only the source commit on `main` in this task.

# 2026-07-17 mission-local battle mastery rebalance

## Player-visible rules

- Removed the former `1.5x` effective maximum-health multiplier. Grey Warden
  humans now use the active native game mode's ordinary maximum-health result.
- A real or fallback kick/shield-bash contact against an enemy human unlocks
  `1000` effective One Handed and Athletics for that individual Grey Warden for
  the rest of the current mission. The existing rank-based `40/60/80%`
  knockdown roll remains separate; a landed alternative attack unlocks mastery
  whether or not that roll produces a knockdown.
- Each Grey Warden tracks bow releases independently. The tenth missile release
  whose weapon's relevant skill is Bow unlocks `1000` effective Bow for that
  agent for the rest of the mission. Misses count because the rule is based on
  arrows fired, not targets hit; crossbows and thrown weapons do not count.
- No campaign character skill is written and no mastery field is serialized.
  State is kept by the mission behavior, removed when the agent is deleted, and
  cleared with the behavior at mission end.

## Effective-skill implementation

- Bannerlord 1.4.7's character-development model allocates `1024` skill levels
  and naturally tops out at `1023`. The requested mastery value of `1000` stays
  inside that engine range while deliberately exceeding the ordinary
  combat-stat reference value of `300`.
- `GwpAgentStatCalculateModel.ApplyBattleMastery` supplies the mission-local
  value without touching the underlying troop or hero. Native stat models also
  call their own `GetEffectiveSkill` implementations while rebuilding driven
  properties, so `GwpBattleMasteryEffectiveSkillPatch` applies the same result
  to the base, Sandbox, and available Naval implementations. Calling
  `Agent.UpdateAgentStats()` when mastery unlocks makes movement, weapon
  handling, accuracy, damage, and AI recalculate immediately.
- No separate shield skill was added. Bannerlord exposes driven shield
  properties rather than a normal `SkillObject` equivalent to One Handed or
  Athletics, and the user explicitly chose to omit that part of the bonus.

## Initial-stat and equipment rebalance

- Normal troop skills now match the native Empire counterpart of the same
  branch and tier: recruit remains equal to Imperial Recruit; heavy infantry
  matches Imperial Legionary; archer matches Imperial Palatine Guard; knight
  matches Imperial Elite Cataphract.
- The custom-battle commander no longer uses the former all-`300` skill set;
  it now uses the same native strong Empire knight-lord profile already used by
  a founding Grey Warden. The six campaign founders retain their existing
  differentiated native strong-lord profiles.
- Equipment was otherwise left unchanged. Only the recruit-exclusive
  `winfhelmet` changed, from `52` to `45` head armour. The heavy winged helmet,
  knight and lord equipment, great-shield stats, protected inherited equipment,
  and black-gold shield were not changed.

## Build and validation

- The first Release build after implementation completed with `0` errors and
  the existing `44` nullable-analysis warnings. The build target deployed the
  module data and both normal-client/editor assemblies to the live module.
- All `17` deployable XML files parse successfully. The `24` deployable source
  files have `0` missing or hash-mismatched live counterparts; both player
  READMEs therefore also match their live copies.
- The normal-client and editor DLLs match at SHA-256
  `E3C9174B967A4625F9A2AC71F8FCF2CE95946768B777EB5A8C4111CAD9A012C9`.
  The live module contains no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP,
  or checksum file, so this is a valid normal-client test layout.
- Static validation found no remaining health-multiplier reference. Parsed
  troop values exactly match the intended Empire baselines and the
  recruit-only helmet reports `45` head armour.

# 2026-07-17 incremental battle mastery follow-up

- In-game verification confirmed that the temporary effective One Handed and
  Athletics value of `1000` works and produces the intended very strong combat
  result. The rejected part was the abrupt direct unlock, not the effective
  skill mechanism or its upper limit.
- The first follow-up briefly changed the rule to `+20` One Handed/Athletics on
  a registered alternative-attack contact and `+10` Bow per arrow. The user
  then clarified that contact is the wrong trigger: the mod's native/fallback
  resolver already guarantees the control result, and growth belongs to the
  deliberate kick/shield-bash action itself.
- The final rule awards each eligible Grey Warden agent `+50` effective One
  Handed and `+50` effective Athletics when one alternative-attack action is
  accepted by the shared AI/player resolver. It is awarded before target
  selection, so the action still counts if no native collision occurs or no
  fallback target is currently available. It is no longer awarded from
  `OnAgentHit`, preventing native/fallback resolution from becoming a second
  growth event.
- Every actual Bow missile release adds `+10` effective Bow. The per-agent
  bonuses are clamped before addition and the final effective value is capped
  at `1000`; no underlying troop/hero skill or save data is changed. All Grey
  Warden troops, cavalry, founders, later clan members, and the custom-battle
  commander use the same rules. No cavalry-only kill bonus was added.
- The player-facing CN/EN release entries were deliberately compressed to one
  experience-level sentence: Grey Wardens grow stronger while fighting. Exact
  triggers and values remain here only. `AGENTS.md` now makes this the default
  rule for future player README updates: no trigger counts, stat gains, caps,
  formulas, or minor tuning unless omission would prevent a necessary player
  decision.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files match the live module;
  both runtime DLLs match at SHA-256
  `C8A85F5F3AA3498BAC5CA34C0BD47F8864F6EA0B08AE066E368D3AE8BB4809E6`.
  All deployable XML parses, and the live normal-client layout contains no
  editor tree or archive file.

# 2026-07-17 player release-note granularity correction

- The earlier policy of reducing each player-visible update to one sentence was
  too aggressive: it hid materially different balance and progression changes
  behind one vague line. The CN/EN r4 notes now use two concise player-facing
  bullets, separating the troop rebalance from the new in-battle mastery loop.
- `AGENTS.md` now requires one short bullet per meaningful player-visible
  feature or outcome, while grouping minor tuning and keeping exact triggers,
  values, caps, formulas, implementation details, and inconsequential notes in
  this maintenance document.
- Both revised READMEs were copied to the live module. All `24` runtime
  deployable files match their live counterparts; the CN README SHA-256 is
  `6412CAD5C081F1663150D4F9B91586F7B936CC5567A3241936D21BD7AB76966D` and
  the EN README SHA-256 is
  `356221766D5367CFA22D8B436AD484C35D20422FC45008A17848DDEC77CB67F6`.
  The live module still contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, or archive file. An initial all-file comparison reported
  the repository's `12` editor-source files as missing from live; this is the
  required normal-client layout, so the corrected runtime-only comparison
  excludes those three editor trees and reports `0` mismatches.

# 2026-07-17 Grey Warden sparring mission research

- The local game install contains the user's legacy reference mod at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\MissionDuel`.
  Its active client DLL identifies itself as MissionDuel `1.0.5` and has
  SHA-256
  `D52378DD262BCE86ABCBEDB833A4554B27954274BA96705782C2A92C6117B6E5`.
  It is a read-only reference and is not a Grey Warden dependency.
- Decompilation established that MissionDuel does not create a peaceful duel
  mission. It injects `DuelMissionLogic` into every mission, waits for an
  already-running `MissionMode.Battle` field battle, and temporarily suspends
  that battle around two existing hero agents. It therefore cannot be reused
  wholesale for the requested dialogue-triggered sparring flow.
- Its spectator technique is directly useful. When a duel starts it holds all
  non-duelist formations at their current median positions, changes them to a
  loose arrangement, orders hold fire and face-enemy, disables formation AI,
  clears each spectator's target and automatic target selection, and sets
  `Agent.MortalityState.Invulnerable`. It gives the two duelists explicit
  targets in temporary formations and periodically plays the native cheer
  yells and `act_cheer_1` through `act_cheer_4` animations. The 1.4.7 runtime
  enum was verified as `Mortal = 0`, `Invulnerable = 1`, and `Immortal = 2`.
- The legacy mod's post-duel behavior must not be copied: it restores both
  armies and resumes the real battle, and it contains unrelated gold,
  equipment, attribute, and relationship rewards. Grey Warden sparring should
  instead keep campaign parties and save data untouched, declare the winner,
  then permit the normal Tab mission exit.
- The current 1.4.7 official city path is
  `CampaignMission.OpenArenaDuelMission(...)`. The stock controller spawns only
  the player and target, uses `SimpleAgentOrigin`, converts defeat to
  unconsciousness through `ArenaAgentStateDeciderLogic`, reports the winner by
  callback, and supplies the arena exit rules. The mission should be queued
  after dialogue closes, following the stock quest pattern, rather than opened
  inside the active conversation mission.
- For a field challenge, calling `CampaignMission.OpenBattleMission(...)`
  without a genuine campaign `MapEvent` is unsafe: the stock battle opener
  dereferences `MobileParty.MainParty.MapEvent` and attaches casualty, party,
  encounter, loot, and result logic. Fabricating a temporary war encounter
  would create avoidable campaign side effects.
- The safe design still reuses the original game systems: obtain the local
  battlefield from `SceneModel.GetBattleSceneForMapPatch(...)`, create its
  initializer through `SandBoxMissions.CreateSandBoxMissionInitializerRecord`,
  open a campaign-mode `MissionMode.Battle`, spawn agents from their real
  characters and equipment with `SimpleAgentOrigin`, use native teams,
  formations, battle views and nonlethal agent-state logic, and allow Tab only
  after the duel resolves. A community source example in
  `actualAnian/RealmsForgotten` confirms that a dialogue-deferred custom duel
  can be opened with `MissionState.OpenNew` and manually spawned hero agents,
  although its hard-coded arena scene, positions, killed-only result test, and
  immediate auto-exit are not suitable for direct reuse.
- Recommended field version: spawn the player and challenged Grey Warden as
  the only combatants, then spawn the two present parties' soldiers in two
  held spectator formations. Apply the proven no-target, hold-fire,
  invulnerable and cheering layer only to spectators. On either duelist's
  unconscious state, freeze combat, display the result, enable normal Tab exit,
  and return to the campaign map without casualties, prisoners, loot, war,
  relation, experience rewards, or save-data changes.
- This entry records research and architecture only. No player-visible code,
  README, build output, or live module file was changed for the sparring
  feature in this step.

# 2026-07-17 Grey Warden sparring implementation

## Campaign entry and town path

- `GreyWardenSparringBehavior` is registered as a campaign behavior and adds a
  dialogue challenge for healthy Grey Warden lords when the player is healthy
  and not already in a map event. The option is suppressed during active
  patrol/enforcement conversations so it cannot displace a crime-resolution
  flow.
- The conversation closes before any new mission is opened. In a town, the
  behavior queues the settlement's arena through `GameMenuManager.NextLocation`
  and opens the native `CampaignMission.OpenArenaDuelMission` only after the
  town menu resumes. The stock nonlethal duel, equipment, result callback, and
  Tab-exit behavior are retained; the bout does not create campaign casualties
  or rewards.

## Field path and spectators

- A field challenge resolves the current map patch through
  `SceneModel.GetBattleSceneForMapPatch`, then opens that native battlefield as
  an `ArenaDuelMission` view with a custom mission controller. It deliberately
  does not create a `MapEvent` or attach the campaign battle casualty, loot,
  prisoner, encounter, or result systems.
- The player and challenged lord spawn as the only combatants, on foot, with
  their real characters and battle equipment. Healthy troops from the two
  present parties fill held ranks behind them up to the player's configured
  battle-size limit. Spectators have no targets, hold fire, cannot be harmed,
  and use native cheer calls and animations. If the challenged lord is already
  in the player's party, the shared roster is spawned only once and neither
  duelist is duplicated as a spectator.
- `ArenaAgentStateDeciderLogic` converts a duelist's defeat to unconsciousness.
  After that removal, the survivor stops fighting, the spectators cheer, a
  localized result is displayed, and normal Tab exit is enabled. Before a
  result, a Tab request is refused. No party roster, hero health, relation,
  gold, inventory, experience, prisoner state, or save field is changed.
- `GwpBattleReinforcementBehavior` explicitly rejects missions containing
  `GreyWardenFieldSparringMissionController`; the field scene therefore cannot
  trigger the Grey Warden reinforcement feature merely because it reports
  itself as a field battle. Normal campaign field battles are unchanged.

## Localization, build, and deployment

- English remains embedded as the base language. Ten new Chinese strings cover
  the challenge, acceptance, launch/spawn failures, start, both town/field
  results, and the pre-result exit refusal. Both player READMEs record the
  feature at experience level only under `v1.4.7-r5`.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. The build target deployed module data and the
  normal-client/editor assemblies to the live module.
- All `17` deployable XML files parse successfully. All `24` deployable source
  files have `0` missing or hash-mismatched live counterparts. The normal-client
  and editor DLLs match at SHA-256
  `9580EDFCEFA8003853B8B122C5251839B00A3B1EA04655B483FE6EB8D275F7B2`.
  The live CN README hash is
  `03EB9D2FEF0C8E439F2516D327107A7E7C36908C040714C31F98ED51984880E6`;
  the EN README hash is
  `464B1A2F9BC17867992B8AE0C4535FD7C9B33082AD1D66D562FC887969FBC7FB`.
- The live normal-client layout contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum file. No archive, Git commit, push, tag,
  or GitHub Release was created in this implementation task.

# 2026-07-17 field sparring first-test diagnosis and repair

- The first in-game field test did not reach the custom duel at all. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_37848.txt`, the
  second challenge closes at approximately line `7015`, returns to the
  `encounter` menu, applies a `-10` Grey Warden relation change, and opens a
  stock mission named `Battle` at approximately line `7031`. The hundreds of
  soldiers attacking the player were therefore participants in a genuine
  campaign battle, not failed spectators from the custom controller.
- The cause was the still-active mobile-party `PlayerEncounter`. Closing its
  conversation mission without peacefully finishing the encounter allowed the
  stock encounter menu to interpret the return as an attack. The field
  challenge now calls `GwpCommon.TryFinishPlayerEncounter()` from the dialogue
  consequence and waits until both the encounter and any `MapEvent` are absent
  before opening the practice mission. It does not force-end the conversation
  mission from inside that consequence; the dialogue's normal `close_window`
  transition performs that step.
- The stored challenge survived the accidental battle. After the player lost,
  the same log records `DefenderVictory`, capture by Vandi, and only then
  `Opening new mission ArenaDuelMission` near line `7871`. Pending field bouts
  are now cancelled whenever the main party starts a genuine `MapEvent`, or if
  the player/opponent becomes a prisoner or otherwise invalid. A stale duel can
  no longer launch after battle or during prisoner processing.
- The delayed custom mission then failed before spawning the player. Its stack
  trace identifies `Mission.GetSpawnPathFrame(...)` from `SpawnBout`; that API
  requires the stock campaign battle spawn-path selector, which this peaceful
  no-`MapEvent` mission intentionally does not have. Spawning now derives the
  centre and orientation from the native battlefield's `battle_set` entity and
  grounds all positions against the scene terrain. It no longer depends on a
  campaign battle spawn pipeline.
- If a future battlefield is genuinely missing the required scene entity or
  agent creation otherwise fails, the controller now reports the abandoned
  bout and ends the mission automatically on the next mission tick. It no
  longer leaves a playerless mission waiting for a Tab exit. The Chinese
  failure message was updated to match this automatic return; English remains
  the embedded base text.
- The repaired Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `17` deployable XML files parse, and all
  `24` deployable source files have `0` missing or hash-mismatched live
  counterparts. The normal-client and editor DLLs match at SHA-256
  `33C5608133B0C6689A2A0D4241232E10FF2EAE6F60E36521A472F9D670EB0D9D`.
  The live CN README hash is
  `2AEE373C3186FB253DB2E4D6F3384485F24A67298062BE6FD9B01BFC9EC834CD`;
  the EN README hash is
  `7D144113AB9006A6D0D572FEEC19D4E3D81F183785CCAB40D447C7ECA8112CD5`.
  The live normal-client layout contains no editor tree, ZIP, or checksum file.
  No archive, Git commit, push, tag, or GitHub Release was created.

# 2026-07-17 field sparring second-test diagnosis and repair

- The second test did open the intended custom scene even though the player
  only saw the paused campaign map again. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_24584.txt`, the
  conversation closes at `05:41:18`, `Opening new mission ArenaDuelMission`
  follows immediately, `MissionScreen` activates, the map screen deactivates,
  and `battle_terrain_L` loads. The mission then fails during its first spawn
  tick, returns to the map, and makes the successful scene transition too
  brief to be visible.
- The first independent failure is a managed `NullReferenceException` at the
  old `SpawnBout` ground-position calculation. The previous repair replaced
  the stock `Mission.GetSpawnPathFrame` call with the scene's `battle_set`, but
  then converted nearby two-dimensional points through
  `WorldPosition.GetGroundVec3`; that conversion is not initialized safely in
  this deliberately MapEvent-free mission. The new code takes exact XYZ frames
  from the battlefield's authored `spawn_path_*` paths. It selects the longest
  valid path, places both duelists and the two rank centres around its midpoint,
  and uses the scene's own ground-height query only for lateral spectator
  offsets. The `battle_set` frame remains a guarded compatibility fallback for
  custom scenes without paths.
- The delayed native crash is a separate failure, not a campaign-map movement
  error. The user click merely occurred while the invalid mission state was
  still unwinding. The matching dump is
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.24584.dmp`.
  CLR inspection identifies
  `SandBox.Missions.MissionLogics.MissionAgentHandler.EarlyStart()` as the
  throwing managed frame during `Mission.AfterStart`. In Bannerlord 1.4.7 that
  handler reads `Settlement.CurrentSettlement.Position`; a field mission has
  no current settlement, so the value is null. `MissionAgentHandler` has been
  removed from the field sparring behavior list. It remains untouched in the
  native town-arena path, where a settlement and `MissionLocationLogic` exist.
- The legacy local MissionDuel DLL was checked again rather than imported. It
  never opens a mission and injects only its duel logic into an already-valid
  field battle, so it does not contain a reusable scene launcher. Its useful
  formation-hold, no-target, invulnerable-spectator and cheering behaviors are
  already retained. The new launcher follows the same field-safe principle by
  excluding settlement-only behaviors, while still avoiding the war,
  casualties and relation changes that wholesale reuse of a real battle would
  introduce.
- A successful field spawn now writes a concise diagnostic containing the
  selected authored path and spectator count. A future failure still reports
  the localized abandonment notice and exits automatically rather than leaving
  a playerless mission active.
- The repaired code completed a full Release build with `0` errors and the
  existing `44` nullable-analysis warnings; the final unchanged incremental
  build after documentation synchronization completed with `0` errors. All
  `17` deployable XML files parse, the `10` Chinese sparring ids remain present,
  and all `24` runtime source files have `0` missing or hash-mismatched live
  counterparts. The normal-client and editor DLLs match at SHA-256
  `DB08C862E63C8BE0BDC6572E9D4AA7DBF71CC9369504FBCA6BF7E56EDEC27378`.
  The live CN README hash is
  `DFC77BBB862506C22CD17DB13CC0D62FE60D53ED9BC7853499C6B673E80586BF`;
  the EN README hash is
  `210250B2F78F8000E7F07A1A1AE71F1502A7A71FFB734843BCA24C592C9643D7`.
  Decompilation of that deployed DLL confirms `GetAllSpawnPaths` and
  `GetGroundHeightAtPosition` are present while both the old `WorldPosition`
  conversion and the field `MissionAgentHandler` construction are absent.
  The live normal-client module contains no editor tree, ZIP, or checksum file.
  No archive, Git commit, push, tag, or GitHub Release was created.

# 2026-07-17 field sparring third-test lifecycle diagnosis and repair

- The third test reproduced the short loading animation, immediate return to
  the campaign map, and localized abandonment notice three times. The evidence
  is in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_28152.txt` at
  approximately lines `4275-4345`, `4442-4512`, and `4630-4700`. Each attempt
  opens `ArenaDuelMission`, deactivates `MapScreen`, activates `MissionScreen`,
  loads `battle_terrain_L`, and then throws from `SpawnBout`. This confirms that
  the campaign launcher and scene selection are working; the failure is inside
  custom mission initialization.
- The decisive ordering evidence is that the spawn exception is logged before
  `Mission-AddTeam-Defender` and `Mission-AddTeam-Attacker`. The custom
  controller called `SpawnBout` from `OnPreMissionTick`, but its teams were not
  created until `AfterStart`. Consequently `Mission.PlayerTeam` and
  `Mission.PlayerEnemyTeam` were still null when the code requested the duel
  formations. The source line reported at `SpawnBout` line `528` is the
  optimized caller sequence rather than proof of another terrain-coordinate
  failure.
- The installed Bannerlord 1.4.7
  `ArenaDuelMissionController` was decompiled again as the authoritative
  lifecycle reference. Its `AfterStart` method initializes mission teams first,
  then resolves spawn frames and immediately spawns both duelists. The field
  controller now follows that exact order: mode setup, team initialization,
  position resolution, duelists, spectators, and formation hold all occur from
  `AfterStart`. The premature `OnPreMissionTick` spawn override has been
  removed.
- Positioning has also been simplified to the battlefield's `battle_set`
  centre, which the earlier test already proved is present in
  `battle_terrain_L`. Nearby height probes retain the anchor's valid Z value as
  a fallback. This removes the unnecessary authored-path query without
  reintroducing the unsafe `WorldPosition` conversion.
- Internal phase labels now distinguish team initialization, position
  resolution, each duelist, each spectator rank, and formation holding in any
  future exception log. They are developer diagnostics only and are not shown
  to players.
- The repaired code completed a full Release build with `0` errors and the
  existing `44` nullable-analysis warnings; the final incremental build after
  README synchronization completed with `0` errors. Decompilation of the
  deployed controller confirms there is no `OnPreMissionTick` override and
  that `InitializeTeams()` precedes `SpawnBout()` inside `AfterStart`.
- All `17` deployable XML files parse, and all `24` runtime source files have
  `0` missing or hash-mismatched live counterparts. The normal-client and
  editor DLLs match at SHA-256
  `CDE6E43AAAFC356BCD52EC591C728ED7E62E3C343964941A3D3B7FC992CBBB36`.
  The live CN README hash is
  `D5299297F12C06C46B431AB942A2E6662E805565494E8E59A58F4614C04AB4E8`;
  the EN README hash is
  `EE580BFEC079A23793ADC971F61699AEC3685917AE410C4E558E3B4A6A41B793`.
  The live module contains no editor tree, ZIP, or checksum file. No archive,
  Git commit, push, tag, or GitHub Release was created.

# 2026-07-17 field sparring direct-launch and staging redesign

- The required transition is not a shorter campaign-map delay. Returning to
  the map and requiring any movement click before loading the field scene is
  itself invalid behavior. The old field launcher was subscribed to
  `CampaignEvents.TickEvent`; because campaign time is stopped when the map
  conversation closes, that callback did not advance until a player input
  resumed map movement. The movement click was therefore acting as the launch
  trigger.
- The campaign `TickEvent` listener and its delay counter have been removed.
  `SubModule.OnApplicationTick` now services only the queued field challenge.
  Once the stock `close_window` transition has disposed the conversation
  mission, the next application frame verifies that no conversation, mission,
  encounter, or `MapEvent` remains and immediately calls
  `MissionState.OpenNew`. This path does not depend on campaign time, party
  movement, or any world-map click. `MapState` is only the safe predecessor
  state between two missions, not an input gate.
- The friendly party encounter is still ended with
  `PlayerEncounter.Finish(false)` before launch so the practice bout cannot be
  converted into a campaign war, casualty event, relation penalty, capture, or
  prisoner flow. A genuine `MapEvent` involving the main party cancels the
  pending challenge rather than allowing a stale bout to open later.
- The installed Bannerlord 1.4.7 `HideoutMissionController` was decompiled as
  the authoritative staged-duel reference. Its sequence is now mirrored:
  teams begin non-hostile, the opponent waits for an in-mission conversation,
  the conversation-end callback enables hostility only for the two duelists,
  spectators are placed on `Team.Invalid` during the fight, and only the
  winner's original team is restored for the victory reaction. The local old
  MissionDuel mod remains a secondary reference for invulnerable, untargetable,
  held spectators; its launcher was not copied because it only injects logic
  into an already-running campaign battle.
- Field placement now uses
  `BattleSpawnPathSelector.FindBestInitialPath`, the same native authored path
  selection used for field deployments. The two spectator ranks are placed 25
  metres to either side of the selected midpoint and face one another, leaving
  a 50-metre central ground. The opponent waits at the midpoint; the player
  begins just ahead of the friendly rank and walks into the centre. Scenes
  without a usable authored path retain a grounded `battle_set` fallback.
- Before the bout, both teams are non-hostile. Spectators spawn in line
  formation with formation AI disabled, hold-fire orders, no automatic target,
  invulnerable mortality, and a centre-facing direction. No cheer action is
  issued during scene creation. On reaching the waiting opponent, proximity
  starts `MissionConversationLogic`; accepting the centre-field exchange ends
  that conversation and begins the duel. During the duel, both ranks remain
  invulnerable and scripted in place while looking toward their own duelist.
- After either duelist falls, hostility ends, only the winning rank is restored
  to its original team, and native `AgentVictoryLogic` schedules the standard
  high-cheer reaction. Tab remains blocked until the duel has a result.
- Three new Chinese localization ids cover the centre-field exchange and the
  approach prompt; English remains the embedded base language. There are now
  `13` Chinese sparring ids, and both public READMEs describe the corrected
  experience without exposing the implementation or numerical layout.
- The full Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings; the final incremental deployment completed with
  `0` errors. Decompilation of the deployed assembly confirms the
  `OnApplicationTick` launcher, native field path selector, mission
  conversation logic, spectator `Team.Invalid` transition, and native victory
  timer are present. It also confirms the old campaign `TickEvent` launcher,
  premature `OnPreMissionTick` spawn, and manual spectator-cheer path are
  absent.
- All `17` deployable XML files parse successfully. All `24` deployable source
  files have `0` missing or hash-mismatched live counterparts. The normal-client
  and editor DLLs match at SHA-256
  `3D7F0BD234577B65A779AE3BE0BD95F1195D92B69E22CE2FD38860742121FA48`.
  The live CN README hash is
  `85171790B676E41EE8FAF962F8FB6DD7B02975BA016ED54D27A6037C445D578A`;
  the EN README hash is
  `4621FD57662EAEF551C7EEE8141775365DF518A5C6FC8F80478C6A0E77F0DE8F`.
  The live normal-client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum file. No archive, Git commit, push, tag,
  or GitHub Release was created.

# 2026-07-17 field sparring native-deployment and conversation-view repair

- The fourth field test proves that the application-frame launcher repair is
  successful. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_39072.txt`, the
  campaign conversation ends at line `4111`, `Opening new mission
  ArenaDuelMission` follows at line `4115`, and the field scene reports a
  successful spawn at line `4177`. There is no intervening world-map click or
  movement command. This part must not be rolled back.
- The remaining deployment error was caused by treating the return value of
  `BattleSpawnPathSelector.FindBestInitialPath` as two completed side
  deployment frames. Its pivot is the encounter point on one authored path,
  not the defender and attacker formation bases. Manually placing both ranks a
  short fixed distance around that pivot produced the reported point-blank
  lines and made the waiting lord's approximate path-facing direction appear
  side-on or backwards.
- Bannerlord 1.4.7's installed `MenuHelper.EncounterAttackConsequence`,
  `BattleSpawnPathSelector`, `SpawnPathData`, and
  `DefaultBattleMissionAgentSpawnLogic` were decompiled as the current native
  references. The mission initializer now supplies `SceneHasMapPatch`, the
  campaign patch coordinates, and an encounter direction before scene load.
  The custom controller marks the mission as a field battle in `EarlyStart`,
  allowing the engine to initialize terrain-snapped side path data before the
  controller's `AfterStart` runs.
- The controller now reads the defender and attacker data through
  `Mission.GetInitialSpawnPathData`, applies the stock
  `ComputeSpawnPathDeploymentOffset` and `ComputeDeploymentBaseOffsets`
  calculations, and places each spectator formation at its native side base.
  The player begins just ahead of the defender line, while the opposing lord
  waits at the authored encounter point and faces the player's actual
  position. Only custom scenes without valid spawn paths retain a guarded,
  wide `battle_set` fallback.
- The local legacy reference at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\MissionDuel`
  was decompiled again. Its useful architectural lesson is that it never
  creates a pseudo-battle or replaces native deployment: it injects into an
  already valid field battle, stops formations at their cached positions,
  orders hold fire, disables targets, and makes non-duelists invulnerable.
  Those principles are retained here without copying its obsolete API calls.
- The approach conversation was entering campaign dialogue logic correctly,
  but the UI was absent. The same test log starts the conversation at line
  `4212` and selects AI line `gwp_sparring_field_centre` at line `4219`, yet no
  player response line follows. The former `ArenaDuelMission` view set contains
  neither `MissionConversationCameraView` nor the mission conversation UI and
  also attaches an arena audience handler. That explains both the zoom with no
  dialogue and the inappropriate cheering on entry.
- The first conversation-view repair tried the stock `Alley`
  combat-with-conversation view set because it supplies both conversation
  views without an arena audience. The subsequent test proved that this view
  set also carries a boundary-crossing UI whose separate mission-logic
  dependency is absent in the lightweight field bout. The attempt was removed
  after the entry-crash dump identified that dependency; see the following
  diagnosis.
- Mission teams are briefly genuine enemies while the field-battle path and
  agents initialize, then become non-hostile for the approach and dialogue.
  Accepting the challenge restores mission-only hostility; the two spectator
  formations remain immobile, hold fire, untargetable, and invulnerable, and
  the winner's formation alone receives the native victory reaction. No
  campaign `MapEvent`, faction war, casualty ledger, capture, relation change,
  or battle reward is created. Using a real campaign `MapEvent` was rejected
  because its stock finish path necessarily applies exactly those persistent
  battle consequences.
- The first full Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings; the final incremental deployment completed with
  `0` errors. Decompilation of the deployed DLL confirms the map-patch
  initializer fields, `Alley` view selection, both native side path queries,
  stock deployment-offset calculations, encounter-frame placement, and the
  hostile/non-hostile/hostile stage transitions. The obsolete direct call to
  `FindBestInitialPath` is absent from the deployed controller.
- All `17` deployable XML files parse, all `13` Chinese sparring localization
  ids remain present, and all `24` deployable source files have `0` missing or
  hash-mismatched live counterparts. The normal-client and editor DLLs match at
  SHA-256
  `73093E43B6079025C8522FA19CE0ED6AA3D16626E3C093263916967D6EFBB6BC`.
  The live Chinese README hash is
  `D262E2C8A64031DE7C3BD9B948E208D014D7CE28A6808BB78B3755EDD63330AF`;
  the English README hash is
  `C8CCAB249D98AC03461935C2E39ADB40269FC1A9FBCB6FD84DC9803B8C91739E`.
  The live normal-client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum file. No archive, Git commit, push, tag,
  or GitHub Release was created. In-game confirmation of the revised positions
  and complete centre-field dialogue remains the next test.

# 2026-07-17 field sparring entry-crash diagnosis and repair

- The next test did not fail during module startup or save loading. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_27772.txt`, the
  initial challenge conversation ends at line `4094`, `Opening new mission
  Alley` appears at line `4098`, `battle_terrain_L` loads, both teams are
  created, and line `4160` reports all `209` spectators spawned. The final log
  entry is `MissionScreen-OnActivate`; the process then crashes before the
  first playable frame.
- The matching dump is
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.27772.dmp`.
  WinDbg/CDB identifies a managed `System.NullReferenceException` in
  `TaleWorlds.MountAndBlade.ViewModelCollection.BoundaryCrossingVM..ctor`,
  called by `MissionGauntletBoundaryCrossingView.OnCreateView`. This is the
  exact `Alley` view dependency described above, not an agent, roster, dialogue,
  XML, or map-state failure.
- The `Alley` view selection has been removed. Field sparring now uses the
  stock `TownMerchant` free-roaming conversation view set: it supplies
  `MissionConversationCameraView` and the Gauntlet conversation UI but carries
  neither `MissionAudienceHandler` nor the boundary-crossing view. Its optional
  barter/name-marker views remain dormant unless called, while the existing
  mission status, equipment, leave, spectator, and notification views remain
  usable during the bout.
- The same test also showed `battle_set fallback` rather than the intended side
  deployment data. The engine's internal spawn selector had not retained side
  records for this lightweight mission at `AfterStart`, although the authored
  paths were present—the earlier implementation had already proved
  `FindBestInitialPath` could resolve `spawn_path_02` at that point. The
  controller now first accepts engine-initialized side data when available and,
  otherwise, rebuilds the identical defender/attacker `SpawnPathData` pair
  through the public native selector and terrain-snapping API. It then applies
  the same stock deployment-offset calculations as before. `battle_set` is now
  reserved only for scenes with no usable authored path at all.
- The corrective full Release build completed with `0` errors and the existing
  `44` nullable-analysis warnings; the documentation-synchronizing incremental
  deployment completed with `0` errors. Decompilation of the deployed assembly
  confirms `TownMerchant`, the campaign patch fields, the engine-initialized
  path check, the late native path reconstruction, and both stock deployment
  offset calls. The crashing `Alley` mission name is absent from the assembly.
- All `17` deployable XML files parse, all `13` Chinese sparring ids remain
  present, and all `24` deployable files have `0` missing or hash-mismatched
  live counterparts. The normal-client and editor DLLs match at SHA-256
  `31781EC44F2F1FD216B4432F6B391C68B3FB7EB573498A552E804B63EA73461B`.
  The live Chinese README hash is
  `A24AEEC6D3CF04B17AE565082E514D774E920A6D0AB9F94A8F772F046D03E185`;
  the English README hash is
  `7839DE6D16A6FA2409C094E09EF51AD2650F2784FA5F42FC698395BBB490D52D`.
  The normal-client module contains no editor tree, ZIP, or checksum file. No
  archive, Git commit, push, tag, or GitHub Release was created. The next test
  should confirm entry, native deployment, the complete centre conversation,
  duel isolation, winner-only cheering, and Tab exit in that order.

# 2026-07-17 field sparring native-spawn and marching-formation redesign

- The latest user test proved that the remaining defect was architectural, not
  another short-distance or facing adjustment. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_12848.txt`,
  `TownMerchant` opens at lines `4437` and `4910`, but the old controller still
  reports `battle_set fallback` at lines `4499` and `4972`. That pseudo-battle
  spawned spectators through fixed custom frames, so the challenged lord could
  appear apart from the army, face away from the player, and skip the visible
  march from the native deployment lines. The fixed-frame/manual-`SpawnAgent`
  route is now retired rather than patched again.
- The replacement builds two `CustomBattleCombatant` instances from the healthy
  members of the real player and opponent parties, preserving each party's name,
  culture, banner, general, character equipment, and mounts. The mission now uses
  `MissionCombatantsLogic`, `BattleSpawnLogic("battle_set")`,
  `DefaultBattleMissionAgentSpawnLogic`, `BattlePowerCalculationLogic`,
  `CustomBattleAgentLogic`, and the indispensable
  `CustomBattleMissionSpawnHandler`. The handler, not the custom controller, owns
  `InitWithSinglePhase`; agents therefore enter through Bannerlord's native field
  deployment path on the first spawn-logic tick. A field challenge is rejected
  when the opponent belongs to `MobileParty.MainParty`, preventing the same
  roster and player character from being cloned onto both sides.
- The first native-spawn draft delegated directly to
  `CustomBattleTroopSupplier`, whose `CustomBattleAgentOrigin.BattleCombatant`
  returns `CustomBattleCombatant`. The 1.4.7
  `CampaignAgentComponent.OwnerParty` implementation hard-casts that value to
  `PartyBase` while starting conversation animations, so the centre conversation
  would deterministically throw `InvalidCastException`. The final
  `GreyWardenSafeTroopSupplier` preserves the stock allocation order but wraps
  every supplied troop in `GreyWardenSafePartyAgentOrigin`: its
  `BattleCombatant` is the real owning `PartyBase`, while wound, kill, route,
  removal, score-hit, and banner callbacks are no-ops. This keeps campaign stat
  and conversation code compatible without writing the practice result back to
  either roster.
- No `MapEvent` or campaign war is created. `MissionCombatantsLogic` continues to
  receive the two side-labelled `CustomBattleCombatant` objects because a real
  `PartyBase.Side` is `None` outside a `MapEvent`; the agents themselves use the
  safe party-backed origins above. The mission deliberately omits
  `BattleEndLogic` and `BattleObserverMissionLogic`, so sparring cannot produce
  campaign casualties, prisoners, loot, relation changes, or an automatic
  campaign battle settlement. `MissionCombatantsLogic` uses `NoTeamAI`: native
  teams, formations, equipment, spawn paths, and battle mode are retained, while
  the sparring controller—not a competing field-battle tactic—owns the ceremonial
  march.
- The controller does no spawning in `AfterStart`. It captures both heroes in
  `OnAgentCreated`, immediately makes human agents invulnerable and targetless,
  waits for `IsInitialSpawnOver`, then stops both spawners and disables
  reinforcements. During staging the native teams are mutually non-hostile; every
  formation holds fire, leaves AI control, and receives explicit line, facing, and
  movement orders. This prevents the former entry shouting, premature attacks,
  arrows, and victory gestures without changing the native starting positions.
- A fixed battle axis is calculated once from the two armies' native deployment
  centres. Both armies then move from those positions toward the midpoint. Target
  positions are clamped through the scene navigation mesh instead of changing a
  cached `WorldPosition` face in place. The controller measures the live front
  ranks from agent projections, corrects both sides toward a `25 m` target gap
  (accepted range `20-30 m`), and requires every active formation to reach its
  target and settle before the opponent may leave the line. A guarded timeout
  records difficult terrain and continues from the closest achieved ranks rather
  than teleporting them.
- The challenged lord remains in its native formation throughout the army march.
  Only after both ranks settle is the lord detached and ordered, using the same
  scripted-walk pattern as the hideout boss sequence, to the navigation-reachable
  midpoint at a walking speed. At the centre its scripted frame, look direction,
  and movement direction are refreshed toward the live player position so it
  cannot settle with its back to the player. The player stays under direct control
  and must approach the lord to trigger the pre-bout conversation.
- The field mission keeps the proven `TownMerchant` view set because it contains
  both `MissionConversationCameraView` and the Gauntlet conversation UI without
  the `Alley` boundary-view dependency that caused the earlier activation crash.
  The official `CombatWithDialogue` view was also audited, but it requires the
  complete battle-end/observer/boundary contract; adding that contract would
  reintroduce automatic battle settlement into a friendly bout.
- When the conversation closes, all non-duellists move to `Team.Invalid`, hold
  their current positions, remain invulnerable and targetless, and look toward
  their own champion. Only the player and challenged lord return to hostile teams
  and mortal state. On the first knockout the teams return to peace, only the
  winning spectators are restored. The controller applies the same
  `HighCheerActions` and `0.25`-to-`3`-second preset used by the stock hideout
  boss duel before calling `AgentVictoryLogic.SetTimersOfVictoryReactionsOnBattleEnd`;
  no manual yell, direct cheer action, or custom voice is issued. Tab becomes
  available only after that native victory transition has begun.
- A Release compile-only validation completed with `0` errors; the warnings are
  the repository's existing nullable-analysis warnings and the three redesigned
  sparring sources add none. Full Release deployment, live hash comparison, XML
  validation, and deployed-assembly decompilation are recorded in the validation
  bullet below after the final build.
- Final Release validation on 2026-07-17 completed with `0` errors and `44`
  pre-existing nullable warnings. The normal-client and editor
  `GreyWardenPolicePurity.dll` copies both have SHA-256
  `93F59FF5A726FCBB8545CB6C5CC2A3A805629AFE909DF5954D9F3F7E4B199027`.
  All `24` deployable `_Module` source files have present, hash-identical live
  counterparts; all `17` deployable XML files parse successfully and the Chinese
  table contains all `13` `gwp_sparring*` ids. Repository/live README hashes are
  respectively `E3BA8FAB38861B0664C21C7E7D8C8FD06D00A23F9FA526427B08A77BC1FD5DE1`
  and `9253EB54BB4E6AACE3C1C9525AA13D3FBAAE19693833F073DD2FB5F3A28F9061`
  for Chinese and English. The normal-client live module contains no `Assets`,
  `AssetSources`, `RuntimeDataCache`, ZIP, or checksum file. Decompilation of the
  deployed assembly confirms the application-tick launcher, safe party-backed
  origin, native spawn handler and spawn logic, marching state, movement orders,
  centre conversation, `Team.Invalid` spectator isolation, and native victory
  timer. It also confirms that manual `.SpawnAgent(`, `battle_set fallback`, and
  `FindBestInitialPath` paths are absent. No archive, Git commit, push, tag, or
  GitHub Release was created.

# 2026-07-17 field sparring opposite-side native deployment repair

- The next two in-game tests exposed a native-deployment contract error rather
  than a formation-distance tuning problem. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_23988.txt`, the
  native spawner completed with `177` agents and the controller prematurely
  reported a `24.3 m` front gap before remaining permanently in
  `OpponentAdvancing`. The preceding
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_6164.txt` follows
  the same sequence with `236` agents and a reported `25.0 m` gap. Neither log
  contains an exception or crash. The player observation that both armies were
  mixed at the player deployment point is therefore the authoritative symptom.
- The former `native armies spawned at their field deployment lines` log was
  unconditional. When the two army centres coincided, the controller silently
  substituted `Vec2.Forward`, so its later projection calculation could create
  a false `25 m` gap inside the mixed crowd. The controller now records
  `Mission.HasSpawnPath`, `Mission.IsFieldBattle`, both army centres, and their
  separation, and refuses to start the ceremonial march unless the engine has
  produced a valid field spawn path with at least `50 m` between the deployment
  centres. This validation is not a Tab escape path.
- Bannerlord 1.4.7's installed `Mission.AfterStart`,
  `MissionBoundaryPlacer`, `BattleSpawnPathSelector`,
  `DefaultMissionDeploymentPlan`, `DefaultBattleMissionAgentSpawnLogic`, and
  `BannerlordMissions.OpenCustomBattleMission` were decompiled as the current
  references. Their lifecycle is decisive: every behavior's `EarlyStart` runs,
  then the engine initializes the battle spawn-path selector, and only then do
  behaviors enter `AfterStart`. `MissionBoundaryPlacer.EarlyStart` supplies the
  `walk_area` boundary used by `GetPatchSceneEncounterPosition`; without that
  boundary the patch-to-scene conversion is invalid and the authored spawn path
  cannot be selected.
- The lightweight sparring mission omitted `MissionBoundaryPlacer` and labelled
  its `MissionCombatantsLogic` as `NoTeamAI`. Consequently
  `Mission.HasSpawnPath` was false and `Mission.IsFieldBattle` was false. The
  deployment logic fell back to the fixed `battle_set` markers instead of the
  scene's authored `spawn_path_01` through `spawn_path_05`. In the tested
  `battle_terrain_L` data, the attacker and defender fixed markers are only
  about `4.4 m` apart, while an authored battle path is hundreds of metres
  long. This exactly accounts for both armies spawning together.
- The mission behavior chain now includes `MissionBoundaryPlacer` and identifies
  itself as `MissionTeamAITypeEnum.FieldBattle`, matching the stock field-battle
  contract. `BattleSpawnLogic("battle_set")`, the defender/attacker combatant
  order, `CustomBattleMissionSpawnHandler`, safe party-backed agent origins, and
  all existing no-casualty controls remain unchanged. Once agents exist, the
  sparring controller continues to disable formation AI, hold fire, clear
  targets, make non-duellists invulnerable, and issue the ceremonial march and
  line orders. No real campaign `MapEvent` or persistent faction war is needed
  to obtain the native opposite-side spawn path.
- The user explicitly declined an abnormal-state Tab safety exit. No such
  fallback was added: Tab remains available only after the duel result and
  native victory reaction, as before. The root deployment contract was repaired
  instead.
- The first Release compilation after the code correction completed with `0`
  errors and the existing `44` nullable-analysis warnings. Because this
  project's build targets always mirror module data and binaries, that build
  also deployed the corrected DLL to both live binary directories. The final
  documentation-synchronized build and source/live hash validation are recorded
  below after completion.
- The documentation-synchronized incremental Release build then completed with
  `0` errors and `0` incremental warnings. Decompilation of the deployed client
  DLL confirms `MissionBoundaryPlacer`, mission team-AI enum value `1` (verified
  against the installed 1.4.7 enum as `FieldBattle`), the native-spawn validity
  check, and the unchanged result-gated `OnEndMissionRequest`. It also confirms
  the stock `AgentVictoryLogic` reaction timer and contains no abnormal-state
  Tab bypass.
- Final source/live validation reports all `24` deployable files present and
  hash-identical, all `17` XML files parseable, and all `13`
  `gwp_sparring*` Chinese localization ids present. Both deployed runtime DLLs
  have SHA-256
  `F0BE2F6E48FB66B0C23FCA6DD3B078C2821F342FCAD7265B73314E5491BEA894`.
  Repository/live README hashes match at
  `8DF3E8A030CE6A5244F7F61C374AE7CF976F3D367A13E762644C936B16475FB9`
  for Chinese and
  `7D7F4AE6338E9F15E308C7DF480975D44050CAD3490BCF819EEF857BE776E77E`
  for English. The live normal-client module contains no editor tree, ZIP,
  checksum, or other archive file. No archive, Git commit, push, tag, or GitHub
  Release was created in this repair task. In-game confirmation of opposite-side
  native deployment and the complete march-to-duel flow remains the next test.

# 2026-07-18 lone-player field-sparring staging repair

- The first test of the corrected opposite-side native deployment used a lone
  player party against an opponent with troops. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_6132.txt`, the
  native deployment check at line `3525` reports valid field spawn data,
  player centre `(302.4,856.3)`, opponent centre `(500.0,574.0)`, and `344.5 m`
  separation. Native spawning completed with `236` agents and only `3`
  formations, all belonging to the opponent. The ranks completed their march
  at line `3551`, but the log never reached `opponent reached the field centre`
  before the session ended. There was no exception or engine error; the state
  machine remained in `OpponentAdvancing`.
- The lone-player case was previously launchable but not modelled explicitly.
  `CalculateRankFrontGap` returned the desired `25 m` whenever either side had
  no non-duellist rank, so the displayed gap was synthetic. The controller now
  treats a one-sided formation set as valid without inventing a second rank,
  skips bilateral gap correction, and places the meeting point in front of the
  actual settled opponent line. If neither side has spectators, it advances
  directly from the two duelists' midpoint.
- The post-march hold order was the cause of the visible retreat and turn. It
  repeatedly issued `MovementOrderMove(formation.CachedMedianPosition)` after
  the challenged lord had left its formation; that median is recalculated as
  formation membership and slots change and therefore was not a fixed hold
  point. The replacement snapshots every non-duellist's actual `WorldPosition`
  at the instant ranks are accepted, removes formation control, and maintains
  an individual no-attack scripted frame facing the duel ground. Original team
  and formation references are retained for winner-only restoration and the
  existing native victory reaction.
- Opponent arrival no longer requires an exact `1 m` target match plus a
  simultaneously settled velocity. Arrival within the ceremonial tolerance,
  or a stopped position near the navigation-clamped destination, is accepted;
  a bounded navigation timeout accepts the nearest reached point instead of
  leaving the mission in `OpponentAdvancing` forever. Acceptance snapshots the
  lord's actual position, fixes movement there, and continues refreshing only
  its facing toward the player. The existing player-within-`3 m` conversation
  trigger, duel-result-gated Tab rule, spectator isolation, no-casualty origin,
  and victory sequence remain unchanged.
- The first Release build after the state-machine correction completed with
  `0` errors and the repository's existing `44` nullable-analysis warnings.
  The build deployed the new DLL and the then-current module data to both live
  binary targets. Final documentation-synchronized build, source/live hashes,
  XML validation, and deployed-assembly checks follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable `_Module` source
  files are present and hash-identical in the live normal-client module; all
  `17` deployable XML files parse and all `13` Chinese `gwp_sparring*` ids are
  present. Client and editor DLLs match at SHA-256
  `1DDAF123F3E5A7E607BC82D6818EF3946442F0B57F36AC1D15E2693364545630`.
  Repository/live README hashes match at
  `CC73CAA1389D3041C06FE0671C254EF5EFBCE93AD86AB0C8F71548BCE463FDA7`
  for Chinese and
  `A55EFD650EA4F53685806262ACBDFB610CE13C109C8064C84F45E3AF862AFFF0`
  for English. The live client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry.
- Decompilation of the deployed client assembly confirms the explicit
  single-sided formation path, `float.NaN` instead of a synthetic front gap,
  fixed spectator-frame methods, zero movement limits, bounded opponent
  advance, and fixed opponent hold. The former `HoldFinalRanks` method and its
  moving `CachedMedianPosition` hold order are absent. No archive, Git commit,
  push, tag, or GitHub Release was created. In-game confirmation of fixed ranks,
  opponent lock, centre conversation, duel, cheer, and result-gated Tab exit is
  the next test.

# 2026-07-18 field-sparring interaction and post-bout conversation follow-up

- The next in-game test confirmed the lone-player repair: native deployment,
  the march, fixed spectator ranks, opponent detachment, and the centre-field
  transition all worked. The remaining presentation issue was the opponent's
  deliberately capped `0.65` walking speed, which made the short ceremonial
  advance look like a slow shuffle. The cap is now `1.8` while the existing
  scripted no-attack/no-run route, navigation destination, arrival lock, and
  timeout remain unchanged.
- The user rejected the automatic proximity conversation. The installed 1.4.7
  `SandBox.Conversation.MissionLogics.MissionConversationLogic` was decompiled
  as the authority: its `IsThereAgentAction` supplies the native interaction
  action between `0.2` and `2 m`, and `OnAgentInteraction` calls
  `StartConversation`. The controller now enables that action only while the
  player is within the opponent's small interaction area. Its own earlier
  proximity call to `StartConversation` is removed; the controller records the
  player/opponent interaction before the native logic runs, preserving the
  custom centre-dialogue condition and the existing conversation-end duel
  callback. Frozen ranks remain non-interactable because the conversation
  behavior stays globally disabled whenever the player is outside the central
  opponent's enable radius.
- A field result now queues a separate map conversation with the challenged
  hero. `GreyWardenSparringBehavior.OnApplicationTick` waits until the sparring
  mission is closed, the campaign has returned to `MapState`, no encounter,
  map event, mission, or other conversation is active, then calls the native
  `CampaignMapConversation.OpenConversation` with no bodyguards. High-priority
  result lines provide different encouragement after a win and a loss, followed
  by one player acknowledgement; the pending state clears on the native
  one-shot conversation-end callback. The field mission's cheer timing and
  result-gated Tab behavior are otherwise unchanged.
- The first Release compile/deploy after these changes completed with `0`
  errors and the existing `44` nullable-analysis warnings. Final synchronized
  documentation build, hashes, XML/localization counts, and deployed-assembly
  inspection follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable files are present
  and hash-identical in the live client module; all `17` XML files parse and the
  Chinese table now contains all `16` `gwp_sparring*` ids. Client and editor
  DLLs match at SHA-256
  `248DC7B9A55662261E01F8FF96F19942705382903E9C0E7A623E36F9CA68F2B9`.
  Repository/live README hashes match at
  `A59B8F7F8818B6C64EF3D6AFEB2A344FD87E2E822E3D269631D91CD0A807F8A3`
  for Chinese and
  `FE86BE0AD28A204C663A55AB59689857A8F97CC27BA6D332D2F04CC1B7CF4279`
  for English. The live client module contains no editor tree, runtime cache,
  ZIP, or checksum entry.
- Decompilation of the deployed client DLL confirms the `1.8` opponent speed,
  controller `OnAgentInteraction`, distance-gated native interaction enable,
  queued result state, both result dialogue ids, native
  `CampaignMapConversation.OpenConversation`, and the one-shot cleanup. The
  field controller contains no direct `MissionConversationLogic.StartConversation`
  call. No archive, commit, push, tag, or GitHub Release was created. The next
  test should verify the faster advance, interaction-key prompt and centre
  exchange, then both win/loss post-bout responses after returning to the map.

# 2026-07-18 field-sparring interaction-mode and mounted-facing correction

- The next test disproved two assumptions in the preceding implementation. In
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_35932.txt`, ranks
  freeze at line `3517`, the opponent begins advancing at line `3518`, and does
  not lock until line `3519`, about `22.5` seconds later. Line `3522` proves the
  controller received the player's agent interaction, but no conversation UI
  followed. The user also observed no interaction prompt and continuous mounted
  rotation as the opponent tracked the player's changing position. The earlier
  `rgl_log_28612.txt` is a second speed failure: its opponent still had `20.5 m`
  remaining when the `30`-second navigation timeout fired.
- Merely enabling `MissionConversationLogic` was the failed interaction
  approach. Its installed `IsThereAgentAction` rejects several mission modes;
  this lightweight field-battle mission is outside the stock free-roam action
  contract even though the controller itself receives an interaction event.
  The controller now overrides `IsThereAgentAction` for exactly one pair—the
  main agent and challenged lord—within the central interaction radius. Its
  `OnAgentInteraction` then directly calls the native
  `MissionConversationLogic.StartConversation`. The stock behavior remains
  globally disabled so frozen spectators cannot expose talk actions. Because
  the launch is still reached only through the engine's agent-action input,
  proximity alone cannot open the dialogue.
- The `1.8` speed cap and `DoNotRun` flag were both insufficient. Opponent
  advance now permits a `4.5` maximum and removes `DoNotRun`, while retaining
  the navigation-clamped scripted destination and no-attack flag. Both travel
  and final hold use the fixed forward direction `-_battleAxis`; all per-tick
  direction-to-player calculation and `SetLookAgent(player)` calls were removed.
  On arrival the actual position is still snapshotted, speed is set to zero,
  and the same fixed forward rotation is reapplied, preventing the rider and
  mount from circling as the player moves around them.
- The first Release compile/deploy after this correction completed with `0`
  errors and the existing `44` nullable-analysis warnings. Final synchronized
  documentation build, hashes, and deployed-assembly inspection follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable files are present
  and hash-identical live, all `17` XML files parse, and all `16` Chinese
  `gwp_sparring*` ids remain present. Client and editor DLLs match at SHA-256
  `496B56F3F8B5E01C7EAFD57704FF083B291639C042450C9FD5B696EA4590F78A`.
  Repository/live README hashes match at
  `C65A8FB7D842E421897100B62311D71F94F4C1E61D76F5C3A453A93444DF66D7`
  for Chinese and
  `1DE0F6784632CC3A4C1993C0C1E8FFCA49AA518D17C235E85AC56F8687A2EAC9`
  for English. The normal-client module contains no editor tree, runtime cache,
  ZIP, or checksum entry.
- Decompilation of the deployed DLL confirms the `4.5` rider/mount caps, direct
  controller `IsThereAgentAction`, the interaction-event
  `MissionConversationLogic.StartConversation` call, and fixed `-_battleAxis`
  directions in both advance and hold. The advance scripted flags decompile to
  value `6` (`NoAttack | ConsiderRotation`) with no `DoNotRun`; both movement
  methods clear `SetLookAgent`, and the old player-direction helper and facing
  refresher are absent. No archive, commit, push, tag, or GitHub Release was
  created. The next in-game test should confirm a visible action prompt, an
  actual conversation after the input, fast stable advance, and no mounted
  tracking rotation.

# 2026-07-18 field-sparring missing-go-to flag repair

- The immediate retest in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_33996.txt` proved
  the opponent did not move at all: it begins advancing at line `3515`, reaches
  the `30`-second timeout at line `3516` with the full original `35.0 m`
  remaining, and is incorrectly locked in place at the army line. Because the
  mission then reported `AwaitingApproach` at the wrong location, the user's
  expected central interaction was unavailable as well. There was no exception
  or engine error.
- Decompilation of the installed `Agent.AIScriptedFrameFlags` identified the
  omitted contract: `GoToPosition=1`, `NoAttack=2`, `ConsiderRotation=4`,
  `NeverSlowDown=8`, and `DoNotRun=16`. The failed advance deployed only value
  `6` (`NoAttack | ConsiderRotation`), so it supplied a destination frame but no
  command to travel to it. The corrected advance uses value `15`, explicitly
  combining `GoToPosition | NoAttack | ConsiderRotation | NeverSlowDown`, while
  retaining the `4.5` rider/mount caps and fixed `-_battleAxis` rotation.
- The timeout is reduced to `15` seconds. It no longer accepts and locks the
  opponent at an unreached army-line position: if navigation still fails, the
  public `Agent.TeleportToPosition` fallback moves both rider and mount to the
  already navigation-clamped meeting point before the normal fixed hold and
  interaction phase. `TownMerchant` was also re-audited in the installed
  `SandBox.View.dll`; its view set includes the mission agent-status UI handler
  required to render agent actions. A one-shot `centre interaction action is
  available` log now records when the player is actually within the controller's
  action range, separating movement/distance failures from input/UI failures.
- The Release compile/deploy completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files are hash-identical live,
  all `17` XML files parse, and all `16` Chinese sparring ids remain present.
  Client and editor DLLs match at SHA-256
  `B9E764EAC0A3A5D89FDBC0B30E97F2684C8B71729520305AD45324DF6043431D`.
  Repository/live README hashes remain
  `C65A8FB7D842E421897100B62311D71F94F4C1E61D76F5C3A453A93444DF66D7`
  and
  `1DE0F6784632CC3A4C1993C0C1E8FFCA49AA518D17C235E85AC56F8687A2EAC9`.
  Decompilation of the deployed DLL confirms scripted flag value `15`, direct
  interaction action and `StartConversation`, the shortened timeout, forced
  navigation-point fallback, and availability diagnostic. No archive, commit,
  push, tag, or GitHub Release was created; in-game confirmation is next.

# 2026-07-18 native advance, visible interaction text, and plain-language repair

- The next test in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_34760.txt`
  confirmed the deployed flag-`15` advance still did not move. The opponent
  starts at line `3518`, reaches the `15`-second timeout at line `3519`, and is
  visibly teleported by the fallback. Lines `3523` and `3531` prove the custom
  F interaction action and direct conversation launch worked, while lines
  `3538`, `3540`, and `3562` prove result queuing, victory resolution, and the
  post-bout map conversation worked. The remaining interaction defect was
  presentation: no primary/secondary interaction text was supplied to the UI,
  so F functioned without a visible prompt.
- `HideoutCinematicController` was decompiled again for the exact stock movement
  contract rather than inferring flag semantics. Its move phase clears prior
  staging separately and issues `SetScriptedPositionAndDirection` once with
  `addHumanLikeDelay=true` and `AIScriptedFrameFlags.None`; it does not use
  `GoToPosition`, `NeverSlowDown`, or repeated per-tick order replacement. The
  field controller now mirrors that sequence: the challenged lord is excluded
  from spectator freezing, detached from formation, has rider and mount
  scripted movement disabled once, has both speed caps reset to `-1`, and
  receives one stock-style scripted destination with a fixed forward rotation.
  Tick code only observes progress. The teleport fallback is removed; the
  timeout now logs remaining distance without pretending the lord arrived.
- `TownMerchant` already provides the Gauntlet agent-status view, but
  `AgentInteractionInterfaceVM` obtains its visible name, key, and action label
  exclusively through `Mission.FocusableObjectInformationProvider`. The
  lightweight mission had no callback for active conversational agents. The
  controller now registers a provider callback in `AfterStart` and, only for
  the challenged lord during `AwaitingApproach`, supplies the hero name plus
  the engine's current `CombatHotKeyCategory` action key and a localized
  `Talk/交谈` label. The existing custom F action and direct conversation call
  remain unchanged.
- All `gwp_sparring*` Chinese strings were rewritten in straightforward modern
  language; the English fallbacks were simplified at the same time. The Grey
  Wardens' ancient organizational origin no longer causes individual dialogue,
  result messages, or instructions to use archaic speech. The new interaction
  label raises the Chinese sparring-id count from `16` to `17`.
- The first Release compile/deploy after these changes completed with `0`
  errors and the existing `44` nullable-analysis warnings. Final synchronized
  documentation build, hashes, XML validation, and deployed-assembly inspection
  follow below.
- The documentation-synchronized incremental Release build completed with `0`
  errors and `0` incremental warnings. All `24` deployable files are present
  and hash-identical live, all `17` XML files parse, and all `17` Chinese
  `gwp_sparring*` ids are present. Client and editor DLLs match at SHA-256
  `FE9E5D858CB7FF20C2DD4D7BB748EE6E2508EA2708AA4BFAB2DB7FAF0A0A5839`.
  Repository/live README hashes match at
  `55DFE82B66F3145A90DC868BC84B6A9C6D3606AE9737E59E15ECBC8E650F6A27`
  for Chinese and
  `D13DE0EBF8950468CC1EFB0E3B4FD94774A1791F66D06B4554268B4369AF6E81`
  for English. The normal-client module contains no editor tree, runtime cache,
  ZIP, or checksum entry.
- Decompilation of the deployed DLL confirms the focusable-information callback,
  current combat-hotkey lookup, localized talk label, one-shot opponent movement
  order, rider/mount `-1` caps, `addHumanLikeDelay=true`, and scripted flag value
  `0`. `TeleportToPosition`, `GoToPosition`, and `NeverSlowDown` are absent from
  the deployed field controller. No archive, commit, push, tag, or GitHub
  Release was created. The next test should confirm natural physical advance,
  the visible name/key/talk prompt, and plain-language dialogue.

# 2026-07-18 field-sparring hold and native duel-boundary correction

- The next in-game test in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_42856.txt`
  confirmed the physical advance, visible interaction action, centre
  conversation, loss resolution, and post-bout map response. The controller
  recorded the opponent arriving with only `0.6 m` remaining. The user
  observed two remaining runtime defects: the mounted opponent continued to
  creep forward after that lock, and native cavalry combat AI could carry the
  opponent through the frozen spectator line.
- The old hold repeatedly sent a rider-only
  `SetScriptedPositionAndDirection` frame while merely capping both rider and
  mount speed. That left the mount's own position and residual travel state
  unsnapshotted. The corrected arrival path snapshots rider and mount world
  positions separately, clears their travel scripts once, applies zero speed
  limits to both, and gives each a position-only hold frame. Per-tick waiting
  code now maintains the caps and fixed look direction without continually
  issuing a new travel-and-rotation destination. Starting the bout explicitly
  clears the mount's hold script and look lock before restoring unrestricted
  speed.
- The settled front-line target is increased from `25 m` to `100 m`, with an
  accepted stable range of `90-110 m`. When the centre conversation starts the
  normal scene `walk_area` still remains unchanged. Only when the player begins
  the bout does the controller replace that boundary with a `100 m`-wide
  rectangle centred on the meeting point; its longitudinal edges stop `4 m`
  short of the actual opposing rank fronts. This uses Bannerlord's native
  mission-boundary input to combat navigation so mounted AI turns back into
  the open ground instead of receiving a repeated scripted correction.
- The redundant approach quick message was removed because the focusable-agent
  provider already supplies the native lord-name, interaction-key, and
  `Talk/交谈` prompt. Field win/loss quick messages no longer instruct the
  player to press Tab; they now report only the result. The unused
  `gwp_sparring_approach` localization entry was removed.
- The Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files are present and
  SHA-256-identical in the live module, all `17` XML files parse, and the
  Chinese table contains `16` unique `gwp_sparring*` ids with no remaining
  approach id. Client and editor DLLs match at SHA-256
  `BB38DC0A50BE7F36BF96E10A27A97F702DD2A881B4D202B6FD0E2A4BF9A650D1`.
  Repository/live README hashes match at
  `5F9B3843C02667FA816777959311C7A4FDD2CEE0589294210F82BB7443DF8C08`
  for Chinese and
  `1F2DD178249452BA50C26E3BC235C60BE93DD65243D3AC801890127910663E82`
  for English. The live client module contains no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry.
- Decompilation of the deployed client DLL confirms the `100/90/110 m` rank
  constants, separate rider and mount position-only hold calls (flag value
  `18`, `NoAttack | DoNotRun`), mount hold cleanup at duel start, and the
  remove/add replacement of `walk_area` immediately before combat. The
  deployed controller contains no `gwp_sparring_approach` reference, and its
  result fallbacks contain no Tab instruction. No archive, commit, push, tag,
  or GitHub Release was created. The next in-game test should cover mounted
  arrival stillness and a long cavalry exchange near both army lines.

# 2026-07-18 direct formation targets and selectable duel styles

- The next formation test with troops exposed a target-correction ordering
  defect. While both armies were still marching from native deployment, the
  controller compared their current front gap with the final `100 m` target
  every second and shifted both destination sets by up to `8 m`. Because the
  current gap naturally remained large during the approach, the destinations
  kept moving inward; after the ranks finally became too close, the same loop
  slowly moved those destinations outward again. Gap correction now runs only
  after every formation has reached its current destination and stopped.
  Initial formation-centre targets also use each formation's observed front
  offset from its cached median after the line-arrangement preparation period,
  rather than assuming the cached `Formation.Depth / 2` describes its final
  physical front. The first march should therefore end near the desired front
  gap without a second collective retreat.
- The centre conversation now asks for a duel style. Mounted combat retains
  both current mounts. Foot combat enters a peaceful preparation phase: the
  opponent repeatedly requests the engine's normal dismount action while held
  stationary, and combat begins only after both duelists are on foot. The
  player's deadline starts when the choice closes the conversation. Remaining
  mounted after `30 s` resolves the bout as a loss without applying synthetic
  damage; result state records a rule violation so the map conversation uses a
  dedicated rebuke rather than the ordinary consolation response.
- When mounted combat is selected by an unmounted player, the closest mounted
  enemy spectator is temporarily removed from the frozen-rank list and receives
  the same one-shot stock scripted travel used for the lord's physical advance.
  The courier rides to a navigation-clamped point beside the meeting centre,
  performs the normal dismount action, leaves the mount stationary, and gets a
  one-shot return order to the exact stored rank position. The courier rejoins
  the frozen spectator list after arriving on foot. Combat starts only after
  the courier has returned and the player has mounted the delivered horse.
- No custom loan-horse label or prompt is supplied. Decompilation of Bannerlord
  `1.4.7` confirmed `Mission.OnAgentInteraction` directly calls
  `Agent.Main.Mount(targetAgent)` and returns before mission behaviors whenever
  the target is a mount. The controller therefore only exposes an agent action
  for that one delivered horse; `MissionFocusableObjectInformationProvider`
  supplies the stock horse name and mount-key text, and the engine owns the
  actual mounting action. Its mission-local mount difficulty is lowered only
  when necessary to the player's current riding skill so the offered horse is
  genuinely usable. Other horses retain normal interaction and difficulty.
- The final Release build completed with `0` errors and the existing `44`
  nullable-analysis warnings. All `24` deployable files are present and
  SHA-256-identical in the live module; all `17` XML files parse; and the
  Chinese table contains `20` unique `gwp_sparring*` localization ids matching
  all `20` localized code references. The conversation-only state id
  `gwp_sparring_field_style` deliberately has no localization row because it
  is never rendered.
- Client and editor DLLs match at SHA-256
  `1A425E6D95638863FE9B8E58049FC30A6B2E5307C6C7941716F0DD94D91EEBC8`.
  Repository/live README hashes match at
  `45C2820D53AA2A807CB2D0CEF64E3251302A120941607EB9B7EFDB8F87CEB0B3`
  for Chinese and
  `2A3D2169CA6BEE5EDA33B6AA49CA90BDDF176942AA69F7082A873E39D5A02899`
  for English. The live module contains no editor tree, runtime cache, ZIP, or
  checksum entry.
- Decompilation of the deployed DLL confirms correction is gated by both
  formation-target arrival and stillness, the `30 s` foot deadline and direct
  rule-violation resolution, the mounted-loan delivery/dismount/return states,
  the loan-horse-only `1.75 m` action gate, mission-local difficulty adjustment,
  and the horse team transfer only after the player mounts. The deployed
  campaign behavior contains both style choices and the separate violation
  result conversation. No archive, commit, push, tag, or GitHub Release was
  created. In-game validation should cover a small two-sided formation, both
  foot-rule outcomes, and mounted selection by an unmounted player.

# 2026-07-18 mounted-loan courier arrival tolerance repair

- The first unmounted-player test reached the mounted-loan branch correctly:
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_38836.txt`
  records normal formation, lord advance, interaction, and the enemy cavalry
  courier beginning delivery at line `3585`. No later loan-state diagnostic or
  exception appeared during the remaining test time. Visually the courier rode
  up and stopped but never dismounted, leaving the mission stuck in
  `MountedLoanStage.Delivering` rather than proving an animation failure in
  `Dismounting`.
- Delivery previously required the mounted courier's rider position to come
  within exactly `2 m` of the navigation-clamped point. Normal agent and mount
  avoidance can stop a horse slightly farther away when the player and opposing
  lord already occupy the centre. Delivery now also accepts a courier that is
  fully stopped within `6 m`, matching the proven stopped-near principle used
  by the lord advance. At that transition the actual stop position is
  snapshotted, rider and mount travel scripts are both disabled, both speed caps
  are set to zero, combat targets are cleared, and the normal dismount request
  is issued immediately. A new one-shot diagnostic records the accepted
  distance before the existing repeated dismount requests take over.
- The Release build completed with `0` errors and the existing `44` warnings.
  All `24` deployable files are hash-identical in the live module, all `17` XML
  files parse, and the normal-client module still contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  SHA-256
  `E4D64178BD63EEFEA7EBC6E1A358B103668A67FD03872DBCC37266794DF01684`.
  Repository/live README hashes match at
  `7B90A4E6730D7E5A7E459E18778E71FF03497C17B8785A7A1B93898C2BDE375C`
  for Chinese and
  `23576D69BA5F11ADAFC579D01FC3BBEE68CA0BD7F362BBD9B98CEFA12AC19E53`
  for English.
- Decompilation of the deployed DLL confirms the `6 m` stopped-near constant,
  rider and mount scripted-movement cleanup, immediate normal `Mount` toggle
  used as the dismount request, repeated fallback requests, and the new accepted
  distance diagnostic. No archive, commit, push, tag, or GitHub Release was
  created. The next test should repeat the exact unmounted-player mounted-style
  path and verify courier dismount, original horse interaction, courier return,
  and combat start.

# 2026-07-18 one-agent dismount formation repair

- The immediate retest in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_6840.txt`
  disproved the remaining distance theory. Line `3608` records that the courier
  was accepted at exactly `2.0 m`, had both travel scripts cleared, and received
  the attempted `Agent.Mount(mount)` toggle. The courier still remained mounted
  through the end of the test. This proves the player-interaction toggle is not
  a reliable command channel for an AI-controlled rider even after navigation
  and velocity conditions are satisfied.
- Installed `1.4.7` decompilation identified the actual AI contract.
  `Formation.SetRidingOrder(RidingOrderDismount)` stores the formation order and
  applies `Agent.SetRidingOrder(Dismount)` to each member; the AI consumes that
  riding order rather than the transient player event-control flag set by
  `Agent.Mount`. The courier is now assigned at the delivery point to an unused
  enemy formation containing exactly that one rider. Only this temporary
  formation receives stop, hold-fire, fixed-facing, and dismount orders. Once
  the rider is on foot, the temporary formation returns to `RidingOrderFree`,
  the courier detaches, runs back under the existing one-shot script, and is
  restored to the exact original formation before being frozen at the saved
  rank position. Other spectator formations never receive a riding order.
- The same one-agent formation path now drives the challenged lord's automatic
  dismount for foot bouts, preventing the identical detached-AI failure there.
  The player's foot-style line was shortened from the rule-exposing
  `I'll dismount within 30 seconds` to simply `Let's fight on foot/步战`; the
  unchanged `30 s` forfeit rule remains internal and discoverable through play.
- The Release build completed with `0` errors and the existing `44` warnings.
  All `24` deployable files are hash-identical live, all `17` XML files parse,
  and the live module contains no editor tree, runtime cache, ZIP, or checksum
  entry. Client and editor DLLs match at SHA-256
  `31646420F3886A28084A8F6D50C8BC3E7EA90321390ACB256AAE5C2E868F4F0E`.
  Repository/live README hashes match at
  `3E3FC3081446443244B89C1691ECD17750CAEA10945B258EA414661897EB850A`
  for Chinese and
  `5C851D12D598DF039C3469B7827A8E30892C66EADE5F2E5AC228FFFC05924585`
  for English.
- Decompilation of the deployed controller confirms selection of an empty enemy
  formation, the one-agent assignment, formation and agent dismount orders,
  release to free riding, and restoration of the original courier formation.
  Deployed dialogue contains only the short foot-style choice. No archive,
  commit, push, tag, or GitHub Release was created. The next test should verify
  the temporary-formation diagnostic, physical dismount, courier return, horse
  interaction, and combat start without any other spectator dismounting.

# 2026-07-18 immediate lord dismount and independent foot-rule timer

- The next foot-bout test in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_16212.txt`
  reached the temporary-formation branch: line `3705` records creation of the
  one-agent formation and line `3706` records the foot selection, but the
  opposing lord remained mounted and combat never began. This disproves the
  formation riding order as a reliable way to dismount the challenged lord in
  his stationary centre-field state. It does **not** disprove the courier's
  one-agent formation path: that rider reaches the delivery point through a
  different scripted-movement state, and the revised courier path has not yet
  received its requested in-game test. The courier formation, dismount order,
  return movement, and original-formation restoration are therefore preserved
  unchanged for separate validation.
- Bannerlord `1.4.7` decompilation confirms that the public `Agent.Mount(mount)`
  call only raises the transient `EventControlFlag.Dismount`, while formation
  and agent riding orders remain AI requests. `Agent.MountAgent` instead has a
  private setter that calls native `IMBAgent.SetMountAgent`; native callbacks
  update the rider/mount caches, mount-without-rider registry, formation state,
  components, mission behaviors, and driven stats. Foot selection now invokes
  that setter for the opposing lord through one cached `MethodInfo`, verifies
  both `lord.MountAgent == null` and `mount.RiderAgent == null`, grounds the lord
  at the established meeting point, hides the now riderless duel mount, and
  starts combat immediately. A missing setter or failed native separation
  aborts with an explicit diagnostic instead of leaving preparation stuck.
- Player compliance no longer gates combat startup. The internal deadline is
  now `20 s` from foot-style selection and is checked during the live fight.
  The first observed player dismount permanently satisfies the check and hides
  the riderless player duel mount; remaining mounted through the deadline
  resolves the existing rule-violation loss and result rebuke. The dialogue
  remains the short `Foot combat/步战` choice and does not explain the timer.
- The Release build completed with `0` errors and the existing `44` nullable
  warnings. All `24` deployable files are SHA-256-identical in the live module,
  all `17` XML files parse, and the live normal-client module contains no
  `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry. Client
  and editor DLLs match at SHA-256
  `5EBCC4C445C001610DA9C72EE032B7C2C5730CED517637ED6E81001316FB0592`.
  Repository/live README hashes match at
  `0EFDBE3DDA1DA8546353D3E02FDC1D2D78D5F2499BFD4612B24AB2ABA204DE63`
  for Chinese and
  `4B2CB58B4FD31D798D156DD07FA48C5992320A94C8E4B1DAF453B21AF19519AF`
  for English.
- Decompilation of the deployed DLL confirms the `20 s` constant, cached
  private mount setter, immediate forced-opponent-dismount call, post-detach
  rider/mount validation, and absence of the old `waiting for both lords`
  branch. It also confirms zero opponent-lord calls and two courier calls to
  `ApplyTemporaryDismountOrder`, preserving the one-agent courier experiment.
  No archive, commit, push, tag, or GitHub Release was created. The next
  in-game test should select foot combat while both lords are mounted, verify
  that the opponent appears on foot and attacks immediately, then separately
  test player dismount before and after the `20 s` deadline. The mounted-loan
  courier remains a later, independent test.

# 2026-07-18 native animated dismount controller

- The combined retest in
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_36036.txt`
  disproved both remaining implementations. In the first bout, line `4252`
  records the private-setter mount detachment and line `4258` immediately
  starts combat. In game the horse vanished, but the lord retained a mounted
  pose and repeatedly changed height/position while fighting. This proves
  `IMBAgent.SetMountAgent(-1)` updates the rider/mount relationship but does
  not itself execute the animation-state transition required for a built
  agent. Teleporting the rider afterward compounded the visible jitter. The
  private setter, reflection dependency, forced rider teleport, and all
  associated diagnostics have now been removed.
- The second bout in the same log independently tests the loan courier. Line
  `4551` starts the unmounted-player mounted branch; line `4557` creates the
  one-agent formation and line `4559` accepts the courier exactly `2.0 m` from
  the delivery point. The rider then remained mounted until the test ended.
  This formally disproves the temporary formation plus
  `Formation.SetRidingOrder(Dismount)`/`Agent.SetRidingOrder(Dismount)` as a
  sufficient physical-dismount trigger in this lightweight field mission.
  Keeping the courier in an isolated formation is still useful for ownership
  and formation safety, but the riding order is now only a supporting order,
  not the mechanism expected to complete the action.
- Installed `1.4.7` code was re-audited before the replacement. The public
  `Agent.Mount(currentMount)` path sets `Agent.EventControlFlag.Dismount`; the
  native action system then selects and runs the correct dismount animation,
  and only animation completion invokes `Agent.OnDismount`. That callback is
  what notifies the formation, every agent component, mission behaviors,
  mounted-state listeners, driven-stat recalculation, both rider/mount caches,
  and the mission's free-mount registry. The public `RidingOrder` API only
  passes an AI intention to native code. Official `Agent.Controller` handling
  also confirms that moving an AI human to `AgentControllerType.None` removes
  its `HumanAIComponent`, while restoring `AI` adds the component back. Local
  TaleWorlds documentation contains animation taxonomy but no higher-level
  public API for forcing an arbitrary AI rider to dismount. Web searches for
  current community examples found no `1.4.7` path more complete than these
  official engine contracts.
- Both challenged-lord and courier dismounts now use the same native animated
  state machine. At the stable stop point, scripted travel is disabled, rider
  and mount speeds are held at zero, the previous controller is stored, an AI
  rider is temporarily changed to `None`, and the stock
  `EventControlFlag.Dismount` is submitted immediately and on every preparation
  tick. The controller logs when `GetCurrentActionType(0)` first becomes the
  engine's `Dismount` action, then waits for `MountAgent == null`; it never
  changes the mount relationship itself. On the real `OnDismount` result, the
  prior controller and free riding order are restored. The lord proceeds to
  foot combat; the courier retains the one-agent formation until completion,
  then detaches and returns to his exact saved rank position on foot.
- Foot preparation deliberately no longer calls the centre hold routine while
  the lord is dismounting, preventing the old scripted frame lock from
  interrupting the native animation. The courier is already removed from the
  frozen spectator list during delivery. Each native dismount has a `10 s`
  diagnostic timeout; failure records the current action type, event flags,
  and controller and safely cancels the mission through the existing tick
  exception boundary instead of silently hanging. The player's independent
  `20 s` foot-rule timer and short dialogue remain unchanged.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical in
  the live module; all `17` XML files parse; and the live normal-client module
  contains no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum
  entry. Client and editor DLLs match at SHA-256
  `28D0555894E459C5D87CE09A51E4522127FE5ADB58980E4EED4B47073D94CEA2`.
  Repository/live README hashes match at
  `4A46D84F35E33E60FBBC886217B967E1A2E128CF2D370CA768873C3F0216B8AE`
  for Chinese and
  `E09CD4AB410687F8CB54B14761C6A11BEFE9FB0F858D96D12478CFF865B28FA0`
  for English.
- Decompilation of the deployed client DLL confirms both the internal `20 s`
  player grace period and `10 s` native-action timeout; it contains controller
  suspension to `None`, repeated stock dismount event submission, action-type
  detection, and controller restoration. It contains no reflection import,
  private `MountAgent` setter, direct mount detachment, or forced rider
  teleport. The courier still receives its isolated formation and supporting
  riding order without affecting any spectator formation. No archive, commit,
  push, tag, or GitHub Release was created. The next in-game test should first
  verify visible lord dismount animation and clean on-foot combat, then verify
  courier dismount, usable horse, foot return, and combat start. If either
  stalls, the new `entered the native dismount animation` line and the timeout
  diagnostic will distinguish event rejection from animation completion.

# 2026-07-18 preserve ordinary dismounted horses

- The successful native-animation retest exposed one remaining presentation
  defect: after either duelist dismounted for foot combat, the horse vanished.
  This was not an engine cleanup. The controller still called the old
  `HideFootDuelMount` helper after a confirmed dismount; that helper changed
  the loose horse to `Team.Invalid`, capped its speed, made it invulnerable,
  and called `FadeOut(true)`. The same helper ran when the player satisfied the
  foot rule, which explains why both the opposing lord's and player's horses
  disappeared consistently.
- Ordinary duel mounts now receive no custom cleanup. The player horse is not
  stored or modified after dismount at all. The opposing lord's horse only has
  the scripted movement and zero-speed staging restrictions removed because
  those restrictions were installed earlier to hold the mounted lord at the
  meeting point; its team, mortality, interaction, damage, AI, and subsequent
  battlefield behavior remain native. The helper no longer calls `SetTeam`,
  `SetMortalityState`, or `FadeOut`. The special mounted-loan horse remains
  separate and retains only the delivery-flow handling needed to make the
  offered horse usable by the player.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical in the
  live module, all `17` XML files parse, and the normal-client module contains
  no editor tree, runtime cache, ZIP, or checksum entry. Client and editor DLLs
  match at SHA-256
  `B04933C7ECBDE148E460B169DC573A516C50A44DA8C7543E743EFF268E1E34A0`.
  Repository/live README hashes match at
  `55C142AB400902C5B62DCD95111AF342FC8CB2038502BC957A162C369F0BD5AA`
  for Chinese and
  `5B62A75941ED35E98106195514F6F25A79546D7E74F2065B565A90B306DFD68B`
  for English.
- Decompilation of the deployed client DLL confirms that the ordinary-foot
  mount path now contains only `DisableScriptedMovement` and removal of the
  temporary speed cap for the opposing lord's loose horse. It contains no
  `FadeOut`, team change, or mortality change in that helper, and there is no
  stored player mount or player-horse cleanup call. Existing spectator and
  purpose-built loan-horse safety code remains intentionally separate. No
  archive, commit, push, tag, or GitHub Release was created.

# 2026-07-18 player excluded from alternative-attack fallback targeting

- Grey Warden kick and shield-bash actions use two distinct paths. A real
  native collision is observed by `OnAgentHit` and receives the existing
  damage-model knockdown decision. If the animation produces no native hit,
  `GwpAlternativeAttackControl.GetNearestEnemyTarget` selects one enemy within
  two metres and later registers the small synthetic control contact. The
  reported player knockdown without a visible hit came from this fallback
  target selection, not from the real collision path.
- The requested exception is implemented only at the point where the nearby
  fallback candidate is chosen: `candidate.IsMainAgent` candidates are skipped.
  No duplicate guard was retained in the delayed resolver or synthetic-contact
  helper after review, keeping this small rule at its single source of truth.
  Consequently the player cannot be selected for an off-target nearby fallback,
  while a genuine native kick or shield-bash collision against the player still
  reaches the unchanged damage model and can knock the player down normally.
  NPC fallback behavior and all existing probabilities remain unchanged.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical in the
  live module, all `17` XML files parse, and the normal-client module contains
  no editor tree, runtime cache, ZIP, or checksum entry. Client and editor DLLs
  match at SHA-256
  `7D53DC7E2540F8F992D609C600D4E0A255037907588BF2318F219EF38CB792C4`.
  Repository/live README hashes match at
  `A3C2A8CE7747812CC789872E2223424D3FE9606C24B672F76B64AB84E4490A4B`
  for Chinese and
  `E6BE00960848ECBEA9B9E9FC9DCA899898400DD31B92555C0248F0E96AC00069`
  for English.
- Decompilation of the deployed client DLL confirms exactly one
  `IsMainAgent` check in `GetNearestEnemyTarget` and no such check in
  `GwpAlternativeAttackControl.Apply`. No archive, commit, push, tag, or GitHub
  Release was created. In-game validation should place the player beside an NPC
  target during a missed Grey Warden alternative attack, then separately take
  a direct kick or shield-bash hit.

# 2026-07-18 duel arena lateral width adjustment

- Only the duel boundary's lateral half-width changed, from `50 m` to `100 m`.
  The resulting total cross-field width is `200 m`. The approximately `100 m`
  separation between the two army fronts, the dynamically calculated
  longitudinal boundary, and the `4 m` clearance before each spectator line
  are unchanged.
- The final deployed client decompilation confirms
  `DuelArenaHalfWidth = 100f`, `DesiredFrontGap = 100f`, and
  `DuelArenaLineClearance = 4f`. The Release build completed with `0` errors;
  all `24` deployable files match the live module and all `17` XML files parse.
  Client and editor DLLs match at SHA-256
  `BF791CC037419FF0B40AD83F8CBE03A085032849A387F49E78337381311B1B7E`.

# 2026-07-18 formal v1.4.7-r3 release

- The player release baseline is GitHub release/tag `v1.4.7-r2`, not the
  intermediate local development labels that were used while the field-duel
  work was being tested. Both player READMEs were rewritten as one concise
  `2026-07-18 v1.4.7-r3` entry describing only the differences a player sees
  after upgrading from `r2`: complete English/CNs support, troop and
  mission-local mastery rebalance, the rebuilt field-sparring mode, its wider
  mounted-combat ground, the player alternative-attack fallback exception, and
  the campaign UI/returning-patrol/village-defense text repairs. The former
  `r3/r4/r5/r6` step-by-step development history was removed from the shipped
  README and remains documented only in this maintenance history.
- The protected runtime asset packages remain byte-identical between repository
  and live module:
  - `gwp_inherited_legacy_assets.tpac`:
    `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - `gwp_black_gold_shield.tpac`:
    `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
- The formal archive was rebuilt beside the live module at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip`
  with its checksum at the same path plus `.sha256`. The ZIP contains one
  top-level `GreyWarden` directory and `27` runtime files. It deliberately
  excludes `Assets`, `AssetSources`, `RuntimeDataCache`, editor binaries, PDBs,
  `shader_compile_report.log`, nested archives, and diagnostic content. Size:
  `349,302,387` bytes. SHA-256:
  `13B5B4BDED0563E24027B14808512B3500BFDB4E073268D88AECE567754D0FBF`.
- The archive was extracted twice: once in its unique package workspace before
  replacing the prior local archive, and again from the final Modules-directory
  path. Both checks found `27` extracted files, `0` missing/hash-mismatched
  files, and `0` extra files against the selected live runtime set. Final
  archive/live hashes match for the client DLL at
  `BF791CC037419FF0B40AD83F8CBE03A085032849A387F49E78337381311B1B7E`,
  Chinese README at
  `F34F0D011A93764F41DE4F53E4308565072F9EC939E459BB5824FBE2B32AC31B`,
  and English README at
  `F86D25C29DF39DB059E1CE21F1DF18909912D5C0DF2881EDFC4C26E3FE4273FE`.
- This entry is the rollback point for the formal source commit, `main` push,
  `v1.4.7-r3` tag, and GitHub Release. The release assets must be the exact ZIP
  and checksum recorded above; do not regenerate them after the source commit.

# 2026-07-18 native town-arena duel transition repair

- In-game testing of the town challenge found that accepting the bout ended in
  the ordinary arena walkabout beside the arena master, with no fight. The
  previous transition set `GameMenuManager.NextLocation` to the arena before
  ending the conversation mission and then attempted to call
  `CampaignMission.OpenArenaDuelMission` from `BeforeGameMenuOpened`. The
  location transition and the duel mission competed for the same state change;
  the ordinary location mission won.
- The original `SandBox.dll` was decompiled with the repository-local
  `.codex_tmp\ilspy\ilspycmd.exe`. `SandBox.SandBoxMissions` confirms that
  `OpenArenaDuelMission` directly opens mission type `ArenaDuelMission`, uses
  `ArenaDuelMissionController`, and spawns the player and named opponent from
  two different `sp_arena` frames. With `requireCivilianEquipment=false` and
  `spawnBothSidesWithHorse=false`, both combatants use their first battle
  equipment and no horse. `SandBox.View.dll` confirms the same mission type
  automatically loads `MissionAudienceHandler`, the native status/equipment
  views, cheer-bark handling, and campaign spectator view.
- Town challenges no longer set `NextLocation` or subscribe to the town-menu
  opening event. After the conversation mission closes and `MapState` is
  active, the campaign behavior opens the native duel mission directly. The
  stock duel controller remains responsible for teams, arena spawn markers,
  equipment, AI, defeat detection, audience, and cheers; no custom arena
  combat controller was added.
- The duel result now enters the existing post-bout conversation queue. The
  campaign behavior closes the finished native duel mission on the next
  application tick and then opens the same concise victory/defeat response
  conversation used by field sparring. This deliberately replaces the former
  one-line quick-information result.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `093FA5B6C765195AC4830BD9BF4265FA71F95F49DE1BD8910AF6080930E69A81`.
  Repository/live README hashes match at
  `242DC17C3B0944C5F6FE017E7F209214CCAB846065CD7D1D619B87C636CF83B4`
  for Chinese and
  `68D0F72AA1698634899CD28342C8475B58697C1F814E5FF9EEACBBAB4AEDA517`
  for English.
- Decompilation of the deployed client DLL confirms that the town launcher no
  longer contains a `NextLocation` transition and calls
  `OpenArenaDuelMission(scene, arena, opponent, false, false, callback, 100f)`.
  It also confirms that the callback queues the post-bout conversation and the
  application tick closes the completed mission. No archive, commit, push,
  tag, or GitHub Release was created. The next in-game test should verify the
  two separate arena spawns, complete personal equipment, absence of mounts,
  audience audio, and both victory and defeat return conversations.

# 2026-07-18 town-arena knockdown and return-conversation repair

- The first native-arena retest confirmed that both combatants and the fight
  now spawn correctly, but exposed two follow-up defects. The application tick
  ended the mission immediately when `ArenaDuelMissionController` reported a
  winner, cutting off the visible fall. The result was also sent through
  `CampaignMapConversation.OpenConversation`, which is the correct field-bout
  path but selects a world-map conversation scene and therefore moved a town
  challenge far away from its original presentation.
- Decompiled stock `ArenaDuelQuestTask.MissionTick` establishes the native
  timing: once either arena agent becomes inactive, it starts a
  `BasicMissionTimer` and ends the mission only after `4 s`. The town callback
  now follows the same delay before closing `ArenaDuelMission`, allowing the
  knockdown and audience response to finish visibly.
- At challenge acceptance, the behavior records the active conversation
  mission's `SceneName` and `SceneLevels`. After the delayed arena exit, town
  results now call the stock `CampaignMission.OpenConversationMission` with
  those recorded values and two no-horse, no-bodyguard, post-fight character
  records. This recreates the original town conversation presentation and lets
  the existing concise win/loss dialogue finish the entire flow. Field results
  retain their previous map-conversation route and horse behavior.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical live, all
  `17` XML files parse, and the normal-client module contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  `9E1F6DC02F0FA77D6323FAFFABB1B0B3DFABA16340F89C41F5B8002B883BA00E`.
  Repository/live README hashes match at
  `A6ABFE089D5C43C50448B451AA134B870DD8DC768C32408669F0E0663C864ABC`
  for Chinese and
  `3DEA5AC311B82C4B22D19AE94C1553E404D912A733BF26382A92770B623BDF1B`
  for English.
- Decompilation of the deployed client DLL confirms the `4 s` timer gate,
  capture of the original `SceneName` and `SceneLevels`, the town-only call to
  `OpenConversationMission`, and retention of
  `CampaignMapConversation.OpenConversation` for field results. No archive,
  commit, push, tag, or GitHub Release was created.

# 2026-07-18 town meeting background and longer result hold

- The next retest confirmed that the four-second hold works, but the player
  requested another five seconds. The total post-result arena hold is now
  `9 s` before the mission closes.
- The latest `rgl_log_29624.txt` proves why the result conversation still used
  an outdoor background. The queued diagnostic recorded `scene=` with an empty
  value. The challenge began from a menu/tableau lord conversation, not a live
  three-dimensional mission, so `Mission.Current` was null and there was no
  original `SceneName` to preserve. Passing the empty value to
  `OpenConversationMission` correctly triggered its stock map-scene fallback,
  which loaded `conversation_scene_forest`.
- Town results now select the stock `MeetingSceneData` whose culture matches
  the current settlement and pass its explicit `meeting_castle_*` scene ID to
  `OpenConversationMission`. These scenes are registered by the original
  `meeting_scenes.xml` and include the official meeting conversation spawn
  prefab. An empire meeting scene is retained only as a fallback if the game
  data contains no matching culture entry. The invalid attempt to remember a
  nonexistent menu-conversation mission scene was removed.
- The same log contains no managed exception or Grey Warden stack trace during
  shutdown. It records successful save, screen cleanup, `There are no living
  managed objects`, and normal managed-interface deletion. The only error log
  entries are repeated FMOD event-not-found messages without a call stack;
  these begin during general startup/audio loading rather than at the sparring
  shutdown. No code change was attributed to an unproven exit crash.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical live, all
  `17` XML files parse, and the normal-client module contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  `A706973F3661DD6F0FD2970B524088509E1B9C565E22C1EC6B70B932591E7542`.
  Repository/live README hashes match at
  `D91223270C82B38421285739AB89048E6895E2E0D69CA4C156DCEC0C71DFABA6`
  for Chinese and
  `42F2BC76D3173B60743FB40E5FB253BD00CF871D0C7360A9F6E01C82E12C2C8E`
  for English.
- Decompilation of the deployed client DLL confirms the `9 s` timer gate, the
  culture-string lookup over `GameSceneDataManager.Instance.MeetingScenes`,
  the stock empire meeting fallback, and the explicit town
  `OpenConversationMission` call. No archive, commit, push, tag, or GitHub
  Release was created.

# 2026-07-18 use the actual nearest lord-hall interior

- The generic culture-matched `meeting_castle_*` scene was rejected in favour
  of the actual settlement interior. Town-result scene selection now first
  uses `Settlement.CurrentSettlement` when it is a town or castle with a
  `lordshall` location. If mission-state transitions temporarily clear the
  current settlement, it compares the main party's current map position with
  every town and castle that has a lord hall and selects the nearest one.
- The selected settlement's `lordshall` location resolves its authored scene
  through `GetSceneName(settlement.Town.GetWallLevel())`. The explicit result
  passed to `OpenConversationMission` is therefore the real keep interior for
  that settlement and upgrade level, not an outdoor map conversation or a
  generic culture meeting scene.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical live, all
  `17` XML files parse, and the normal-client module contains no editor tree,
  runtime cache, ZIP, or checksum entry. Client and editor DLLs match at
  `CA3A4CA82F9D3AA1C80E5F40CBE6121CFDC2E7959539895BFC96BBAEA3CCF6AA`.
  Repository/live README hashes match at
  `7AEC0F333286599FEF32F824BAAC0EDB4EFED2EB6B6111A453837196D7D542CE`
  for Chinese and
  `59BDB1B596F992355D9754E33651EB264CF584DD9EB01296D24BD39F993BADE1`
  for English.
- Decompilation of the deployed DLL confirms current-settlement priority,
  squared-distance ordering over fortifications as the fallback, the explicit
  `lordshall` lookup, upgrade-level scene resolution, and retention of the
  `9 s` result hold. No archive, commit, push, tag, or GitHub Release was
  created.

# 2026-07-18 remove automatic town-arena exit

- The fixed post-result timer was removed at the player's request. The stock
  `ArenaDuelMissionController` already marks the duel as finished, allows the
  normal leave request, and displays the native leave-key prompt. The mod no
  longer calls `Mission.EndMission` after a town result or owns any town-duel
  exit timer.
- The result conversation is still queued as soon as the winner is known, but
  `TryOpenPostBoutConversation` naturally waits while the arena mission exists.
  The player may therefore watch the knockdown and crowd reaction for as long
  as desired, press Tab to leave through the original mission flow, and only
  then enter the selected lord-hall response conversation.

# 2026-07-18 trigger the native large arena cheer on the result

- The stock `MissionAudienceHandler` already supplies the arena spectators and
  ambient crowd, but its largest reaction normally waits for mission shutdown.
  That did not match the manual-Tab flow, where the player can remain in the
  arena after the duel has been decided.
- The town-duel result callback now plays the stock
  `event:/mission/ambient/detail/arena/cheer_big` one-shot at the scene's
  authored `arena_sound` entity as soon as either fighter wins. No lord victory
  animation was added, and the mod still leaves mission exit entirely to the
  player's Tab input.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `D4FF7C3904E2533DF5FDBE28982229874A11A867273FFB5912FF60FA135C8E19`.
  Repository/live README hashes match at
  `2B1AB72FC4866457E0DB7D9AF05009F21D879C432DE1D09DE59B2BA17B2CCE59`
  for Chinese and
  `BF856714EFACF5EB7BCEE0124B90C39607DFF5A5812C966B1CE2765DE2529CD1`
  for English.
- Decompilation of the deployed client DLL confirms that
  `OnTownBoutEnded` calls `PlayArenaVictoryCheer`, which resolves the authored
  `arena_sound` entity and starts the stock `cheer_big` event before queuing
  the result conversation. No town-duel mission-end call was reintroduced; the
  player still leaves with Tab. No archive, commit, push, tag, or GitHub
  Release was created.

# 2026-07-18 exit-crash investigation and leave-message flood repair

- The latest reported exit failure was investigated from
  `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_33532.txt`, its
  watchdog log, and Windows Application events. Both town arena bouts in that
  run completed their result callbacks, accepted Tab, opened the real lord-hall
  conversation, and finalized their arena scenes normally. The later crash
  occurred after a separate field sparring mission had opened with `433`
  agents and the game was closed while that bout was still unfinished.
- Windows recorded a native access violation in `TaleWorlds.Native.dll` at
  offset `0x74b3f1`, with no managed exception or Grey Warden stack. The same
  `0x74b1f0`, `0x74b34a`, and `0x74b3f1` native shutdown-offset family appears
  repeatedly in Application events from earlier dates and builds. A town-only
  run at 07:28 followed the same arena and lord-hall path and exited normally,
  so the evidence does not support attributing the native crash to the town
  result callback or the newly added crowd cheer.
- One concrete mod-side defect was visible immediately before the latest
  failure: holding Tab while the field bout was unfinished called
  `OnEndMissionRequest` every frame and generated hundreds of identical
  quick-information messages. The field controller now simply refuses the
  premature leave request without allocating and queueing another message on
  every frame. The rule that an unfinished field bout cannot be abandoned is
  unchanged.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `DEBFDE1AC44AA22822EF07F557574E87560436D8CE92777ADC4DC1D7D0E2699E`.
  Repository/live README hashes match at
  `8ABCF7D461A0899196D9173280FB41736AC50353BEFC3342A1745B2588FA35B9`
  for Chinese and
  `C4F6D0535A536B2F07696D1CC726672546DB35CC6166A2149F9004090D0F2480`
  for English.
- Decompilation of the deployed client DLL confirms that the field
  `OnEndMissionRequest` now only returns `_canLeave` and no longer calls
  `AddQuickInformation`. No archive, commit, push, tag, or GitHub Release was
  created. The next test should finish a town bout, leave with Tab, close the
  result dialogue, and exit the game before starting a field bout; a separate
  test should hold Tab during an unfinished field bout and verify that the
  message no longer floods the log.

# 2026-07-18 native encounter-region field-rank planning

- The `433`-agent valley test exposed a structural error in the original rank
  planner. It used the midpoint of the two spawned army centres as the duel
  centre and concatenated every active formation into one lateral line. Seven
  formations capped at `80 m` each could request more than `500 m` of width;
  individual navigation clamps then stranded formations at unrelated slopes or
  valley mouths. The readiness gate required every formation to reach and stop
  at its target with a `90-110 m` front gap, so a single unreachable target
  held the flow until the old `150 s` fallback.
- Decompiled stock `BattleSpawnPathSelector` confirms that the engine already
  predicts this battle's encounter region. For patch scenes it converts the
  campaign encounter coordinate through `GetPatchSceneEncounterPosition` and
  uses the encounter direction to select the initial spawn path. The sparring
  controller now uses that native encounter position as the preferred duel
  centre. Only missions without patch encounter data fall back to the centre
  of the current `walk_area` boundary resolved on the player-to-opponent battle
  axes.
- The preferred point is not accepted blindly. The controller searches outward
  in `8 m` rings up to `80 m`, testing candidates inside the current mission
  boundary and on navigation mesh. A candidate must provide reasonable paths
  from both native army sides to their lines and from both lines to the centre.
  It also measures continuous navigable width on both sides of the battle axis
  and accepts only a symmetric corridor wide enough for the ceremony.
- After the reachable centre is fixed, the two rank baselines are derived at
  `50 m` on either side. Each baseline's actual continuous width is measured.
  Formation widths are kept when space permits; when terrain is narrow, width
  is distributed by the square root of formation size and applied through the
  stock custom form order, increasing depth instead of sending ranks into
  walls. The duel boundary uses the same verified symmetric corridor rather
  than always claiming the full configured width.
- The selected native encounter centre remains fixed after the armies settle;
  the old recalculation from their actual front edges was removed because a
  stalled formation could drag the duel back toward an unsuitable choke point.
  Formation fallback was reduced from `150 s` to `45 s`. If the opposing lord
  still cannot finish the final approach within `15 s`, the controller locks
  him at the nearest position he actually reached and exposes the interaction
  instead of logging forever.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module, all `17` XML files parse, and the normal-client module has
  no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum entry.
  Client and editor DLLs match at SHA-256
  `E8F01E0365FF5F29639CE814B54E32547677345851F1E16E5220566E079F0B6F`.
  Repository/live README hashes match at
  `6FD82577F5FBE16D9DBE88C080B3902E7CEFE0B66745F3BB75F14383ABB346F0`
  for Chinese and
  `490D953E792AFB30A23DA6D00BAD78EB6D6F92B334F034749E54CFD6E3B51E6D`
  for English.
- Decompilation of the deployed client DLL confirms the native encounter-region
  lookup, boundary fallback, concentric reachable-centre search, two `50 m`
  rank offsets, terrain-width allocation through `FormOrderCustom`, adaptive
  duel width, the `45 s` formation fallback, and the `15 s` opposing-lord
  nearest-position fallback. No archive, commit, push, tag, or GitHub Release
  was created.

# 2026-07-18 shared-navigation-route field-rank correction

- The next valley test disproved the preceding interpretation of
  `Mission.GetPatchSceneEncounterPosition`. The engine only uses that patch
  coordinate to score authored spawn paths and select a pivot in
  `BattleSpawnPathSelector.FindBestInitialPath`; it is not a predicted field
  battle destination. The failing run logged native army centres at
  `(389.7,511.8)` and `(424.2,853.3)`, but the patch coordinate
  `(330.6,841.8)` caused the search to choose `(386.0,873.8)`, beside the
  attacker deployment rather than between the armies.
- That false centre provided only `24 m` of measured width. Seven active
  formations were compressed into roughly `16 m` after edge clearance, and
  the two fronts were still `275.3 m` apart when the `45 s` march fallback
  fired. The opposing lord then stopped behind an obstacle and the old
  `15 s` fallback exposed conversation while he remained `5.2 m` from the
  requested point. This confirms the player's visual report and rules out
  formation speed as the primary cause.
- Decompiled stock deployment code confirms that `DefaultDeploymentPlan`
  places both initial armies along the selected authored spawn path, while
  `NavigationPath` exposes the actual navmesh route between their deployed
  positions. The controller now builds that shared route directly with
  `Scene.GetPathBetweenAIFaces` and treats its arc-length midpoint as the
  preferred encounter region. It searches forward and backward only along
  that route, so a valley bend or obstacle changes the candidate route instead
  of leaving the candidate on an inaccessible straight line.
- Each candidate derives both rank centres at `50 m` of route distance from
  the meeting point and derives the battle axis from those two centres. The
  candidate is rejected unless both ranks, the meeting point, and every final
  formation centre are on navmesh and reachable from their actual formation
  origins. The measured symmetric corridor must remain usable at the player
  line, meeting point, and opponent line; the selector prefers the widest
  valid candidate near the route midpoint.
- Formation targets are no longer independently clamped toward an invalid
  destination. Once a complete layout passes validation, the exact validated
  positions are ordered as one layout. The later automatic gap correction was
  removed because it could move a validated line back into a wall. The
  opposing lord's meeting point is likewise required to have a real path from
  his settled rank; it is no longer replaced by a silently truncated point.
- The first Release compilation exposed only two uses of the modern `^1`
  index syntax, which is unavailable to this `net472` build; replacing them
  with explicit final indices produced a successful final build with `0`
  errors and the existing `44` nullable warnings. All `24` deployable source
  files are SHA-256-identical to the live module and all `17` XML files parse.
  The live normal-client module has no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry.
- Client and editor DLLs match at SHA-256
  `CE12F5CD22C755FA6E28137504BFDA2E3658801BAC8E53FDDB03340A689F2E5A`.
  Repository/live README hashes match at
  `424A18445804794C45FD1B88D6EA3634F9AF7DB14664AB25D5C8A632CE88BA76`
  for Chinese and
  `CF82640FDF145F5461CB67DE325D66A5244C463A03F6FE4C33BBB324E2565E3A`
  for English. Decompilation of the deployed client DLL confirms
  `GetPathBetweenAIFaces`, route-midpoint candidate search, explicit player
  and opponent rank centres, full-layout reachability checks, and the
  opposing-lord route requirement. It contains neither the old
  `GetPatchSceneEncounterPosition` centre lookup nor `CorrectFormationGap`.
  No archive, commit, push, tag, or GitHub Release was created.

# 2026-07-18 symmetric field advance and shared rank baseline

- The first shared-navigation-route test still selected an unacceptable
  candidate. The route between the native army centres was `351.8 m`, so its
  arc-length midpoint was `175.9 m`, but the width-first score selected offset
  `71.9 m` at `(408.6,581.2)`. This left the defender rank only about `21.9 m`
  from its start while the attacker rank had roughly `229.9 m` to advance.
  The player's report that the friendly army barely moved, the enemy advanced
  much farther, and the opposing lord stopped near the friendly line is
  therefore fully explained by the logged coordinates.
- Width may no longer outweigh symmetry. The selector now starts at the exact
  navigation-route midpoint and permits only a very small `8 m` correction
  when the midpoint itself cannot host the complete layout. The log records
  each side's requested advance distance and their imbalance explicitly. The
  two rank baselines remain `50 m` on either side of the selected centre,
  preserving the requested `100 m` fighting space.
- The enemy cavalry, infantry, and ranged formations were separated in depth
  because the former planner added a different forward offset derived from
  each formation's current depth and front edge. That compensation has been
  removed. Every formation on a side now receives a target on the same lateral
  baseline; only its left/right offset differs.
- Decompiled `LineFormation.GetLocalPositionOfUnit` confirms that a
  formation's `OrderPosition` is the centre of its front rank and later ranks
  extend backward along negative local Y. Consequently a reduced
  `FormOrderCustom` width naturally increases depth without moving cavalry or
  infantry away from the common front line. Candidate width requirements now
  use only the minimum front width needed for each active formation plus gaps
  and edge clearance; they no longer reject the balanced midpoint merely
  because the full preferred frontage does not fit.
- March readiness now estimates the moving front-rank anchor from the
  formation median plus half its current depth instead of reading the already
  assigned order coordinate. Per-formation logs record formation class, unit
  count, start, target, requested distance, assigned width, and any remaining
  distance at timeout. The march fallback was extended to `75 s` for the much
  longer but symmetric advances expected on this valley route.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module and all `17` XML files parse. The live normal-client module
  contains no editor-only directory, ZIP, or checksum entry. Client and editor
  DLLs match at SHA-256
  `185C8534DF632BCECA814225698E0E263C2BBF5B5BA3E82D0F631DEC7EBD8A0C`.
  Repository/live README hashes match at
  `E7645BEDDDAC2C5A41AC4E8E89074543197CDE8A2D00F8233540CB22F1836319`
  for Chinese and
  `4DC0E272EDD1167880E2A1502C2F0B500C7C3187CAB64E619F9BA013F8CAA3DE`
  for English.
- Decompilation of the deployed client DLL confirms the `8 m` maximum centre
  correction, `75 s` march timeout, player/opponent advance and imbalance
  logging, shared formation targets, reduced `FormOrderCustom` widths,
  front-anchor readiness checks, and minimum-only corridor-width validation.
  `GetFormationFrontOffset` is absent. No archive, commit, push, tag, or GitHub
  Release was created.

# 2026-07-18 fixed midpoint and no terrain-width cancellation

- The next test never reached formation movement. Both attempts on
  `battle_terrain_031` logged the correct `351.8 m` deployment route, then
  threw `no complete and sufficiently wide formation layout was found` during
  `NativeSpawning`. The user-facing terrain-cancellation message and immediate
  `EndMission` were therefore caused by the planner's remaining hard width
  gate, not by an unusable battle scene.
- The field layout is now deliberately independent of terrain width. Its
  centre is always the exact arc-length midpoint of the route between the two
  native deployments. The rank baselines are always taken exactly `50 m` of
  route distance on either side. There is no nearby-candidate search and no
  corridor-width acceptance test capable of changing or rejecting those
  points.
- Width measurement now has one purpose only: selecting each formation's
  `FormOrderCustom` frontage. All same-side formations keep the same front-rank
  baseline. If the measured frontage is narrow, each formation is compressed
  to its minimum front width and the stock line arrangement extends backward,
  away from the duel ground. Lateral target points are navigation-clamped
  locally and logged, but this cannot cancel the bout.
- A missing navmesh route now falls back to the direct native deployment axis
  instead of ending the mission. This fallback preserves the same midpoint
  and `50 m` rank-offset rule.
- No field-initialization exception is shown to the player. The obsolete
  `gwp_sparring_spawn_failed` localization entry and the quick-information call
  were removed completely; diagnostics remain in the engine log only.
- The quit after the forced cancellation produced a native access violation
  in `TaleWorlds.Native.dll` at offset `0x74b3f1`. The managed log completed
  mission teardown and reported no living managed objects. Windows recorded
  the identical native offset at `07:49` and `09:21`, so it is not a managed
  Grey Warden stack. The field controller nevertheless now explicitly removes
  its focusable-object interaction callback in `OnEndMission`, rather than
  relying solely on the provider's later finalization. More importantly, the
  terrain-width failure no longer drives the newly opened mission through an
  immediate exception-and-EndMission cleanup path.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable files are SHA-256-identical to the
  live module and all `17` XML files parse. Client and editor DLLs match at
  SHA-256
  `6B78B687781B278B4AC11A774E513CEA5171E68C0A1E0FAB30576C3CA46787FB`.
  Repository/live README hashes match at
  `1A337836FE5A72993E6FCED74E3E5D7F56A202FF8720F3ED6B876867C6AD7232`
  for Chinese and
  `7D86E688B5FDBA40CB243326AB4A3220625C322EFA621F7A97C3E2BFE9BB77C8`
  for English. A repository-wide search finds no remaining
  `gwp_sparring_spawn_failed`, terrain-cancellation text, or generic sparring
  exception prompt. No archive, commit, push, tag, or GitHub Release was
  created.

# 2026-07-18 map-author route centre and strict shared rank

- The user confirmed that the route-authored solution should apply to every
  field map. `Mission.GetInitialSpawnPath()` exposes the exact `spawn_path`
  selected by the stock battle setup for the current encounter. The field
  controller now reads its authored points instead of building a generic
  shortest navigation route between the two armies.
- Each native army centre is projected onto that authored polyline. The duel
  centre is the arc-length midpoint between the two projections, not the
  midpoint of the entire scene path. The two rank centres are sampled on that
  same path exactly `50 m` toward their respective armies, preserving the
  requested `100 m` duel space while giving both sides comparable travel.
  Reversed author paths are handled by reversing the sampled point list after
  projection. A scene without a valid initial author path falls back to the
  direct line between the native deployments and never cancels the bout or
  displays an exception message.
- This replaces the rejected `GetPathBetweenAIFaces` approach. That API found
  a reachable shortest route, but on `battle_terrain_031` its midpoint was on
  the valley side rather than the map author's low road. The selected
  `spawn_path_01` stays at roughly `z=6–12 m`; projection of the observed army
  centres placed its midpoint at approximately `(465.9,675.6,z6.6)`, matching
  the low, flat combat area and nearby stock tactical markers.
- Formation targets no longer run through an independent navigation clamp
  that can move cavalry, infantry, or ranged troops forward or backward by
  dozens of metres. Each side has one immutable front-rank centre and all of
  its formations differ only by lateral offset. If a requested lateral point
  is unavailable, the point is progressively compressed toward that same
  rank centre. `FormOrderCustom` narrows the frontage and the stock line
  formation adds ranks behind it; no terrain-width check can reject the bout.
- Runtime diagnostics now record whether the author path or direct fallback
  was used, authored and sampled path lengths, both projection points and
  offsets, projection distances, selected centre height, exact rank centres,
  advance balance, line width, and any lateral-only formation compression.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module and all `17` XML files parse. The live normal-client module
  contains no `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum
  entry. Client and editor DLLs match at SHA-256
  `98513B9A95F07DD3721AE2AEF1FBCB0182E4EF61A97E1749B38A3360FB956CA4`.
  Repository/live README hashes match at
  `DE8B1D6D185BC88072C963E71844C3BD2E15F252C048C146A4935CC9DACE8FBD`
  for Chinese and
  `1F440DFA20B6CE250CC4A405CA01A59E12E9EAE9C662560EB1BDB794AE47D8E4`
  for English.
- ILSpy decompilation of the deployed client DLL is stored at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\deployed_author_path\GreyWardenPolicePurity.GreyWardenFieldSparringMissionController.decompiled.cs`.
  It confirms `Mission.GetInitialSpawnPath`, authored point extraction, both
  polyline projections, midpoint and fixed `100 m` gap calculation,
  lateral-only `CreateFormationRankPosition`, and `FormOrderCustom`. It has no
  runtime `GetPathBetweenAIFaces` call. No archive, commit, push, tag, or
  GitHub Release was created.

# 2026-07-18 fixed 200 m arena and low-ground rank sampling

- The latest `rgl_log_40152.txt` proved that the author-path midpoint was
  correct, but `GetUsableLineWidth` was still coupled to the duel boundary.
  On `battle_terrain_031` the measured corridor width was only `24.0 m`, and
  the installed boundary therefore logged `width=24.0`. This was the direct
  cause of the visibly narrow arena.
- Arena size and formation frontage are now separate concepts. The native
  `walk_area` boundary always uses `DuelArenaHalfWidth=100`, preserving a
  fixed `200 m` crosswise battlefield. Its length remains bounded by the two
  rank fronts, which are planned `50 m` on either side of the meeting point.
  Terrain width can no longer shrink the duel boundary.
- After the map-author path identifies the route midpoint and battle axis, the
  controller samples the complete `200 m` perpendicular line at `2 m`
  intervals and chooses the lowest navigable, reasonably connected point as
  the actual meeting location. It then moves exactly `50 m` toward each army,
  samples a new `200 m` perpendicular line for each side, and independently
  chooses the lowest reachable point as that side's shared front-rank centre.
  Lateral movement does not alter the planned `100 m` projection between the
  two fronts.
- Formation frontage is capped separately at `100 m`. If the current desired
  formation widths fit, they remain natural; when troop demand exceeds the
  available terrain or the cap, `FormOrderCustom` consumes the usable width
  and the stock line formation adds depth behind the shared front line. The
  lateral target compressor now also verifies connectivity to the selected
  rank centre, preventing a target on an isolated navmesh island.
- Runtime logs now print the base point, selected low point, lateral offset,
  and height for the meeting, player-rank, and opponent-rank lines, followed
  by the fixed `200 m` arena width and separate usable formation frontage.
- The final Release build completed with `0` errors. All `24` deployable
  source files are SHA-256-identical to the live module and all `17` XML files
  parse. The live normal-client module has no `Assets`, `AssetSources`,
  `RuntimeDataCache`, ZIP, or checksum entry. Client and editor DLLs match at
  SHA-256
  `8008E7A9FD5B345E5F5685FE6AE893C6330D50A58440651D237E53AB01CCC880`.
  Repository/live README hashes match at
  `64761C3151D1F0CAF408B315ABA33E0154A528C3CD82ADAFB511F5275962C84E`
  for Chinese and
  `C97903F6421A7EC3C1CD32E60D3954B4B55B36B7A590975660FDA69B7C625DBF`
  for English.
- ILSpy output for the deployed client DLL is stored at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\deployed_fixed_200m\GreyWardenPolicePurity.GreyWardenFieldSparringMissionController.decompiled.cs`.
  It confirms constants `DuelArenaHalfWidth=100`,
  `MaximumFormationFrontWidth=100`, and `TerrainLineSampleStep=2`, all three
  `FindLowestPointOnBattleLine` calls, formation-width measurement capped at
  `50 m` per side, and a runtime boundary log of exactly `width=200`. No
  archive, commit, push, tag, or GitHub Release was created.

# 2026-07-18 native obstacle-aware formation frontage

- Screenshots and `rgl_log_7812.txt` showed that the fixed `200 m` duel
  boundary worked, but both armies still formed into narrow, deep blocks near
  rocks. In the largest test the three low-point lines were selected as
  intended and the front gap settled at `99.0 m`, yet the shared corridor
  calculation reported only `24 m`; a later test reported `16 m`, with the
  enemy formations receiving only `14 m` total frontage. The opposing lord's
  blocked advance was a consequence of that excessive depth, not a separate
  lord-movement defect.
- The rejected implementation measured from the low point toward each side,
  stopped at the first unavailable navmesh sample, doubled the shorter side,
  and then compressed every formation centre back toward the low point. A
  single rock or small navmesh gap could therefore discard open ground on the
  other side and reduce a hundred-metre formation plan to a few metres.
- Stock code confirms that this preprocessing duplicated and defeated the
  engine's own formation placement. `LineFormation` maintains ordered and
  available unit-position indices and obtains the per-slot world-position
  table through `Formation.BatchUnitPositions`. `IFormation.GetIsLocalPositionAvailable`
  calls `Mission.IsFormationUnitPositionAvailableMT` for every local slot.
  Unavailable slots inside a rectangular formation are therefore omitted or
  filled around by the native arrangement rather than requiring the entire
  frontage to end at the first obstacle.
- Formation planning now keeps the selected low point as the rank reference,
  calculates natural widths from the active formations, and caps their
  combined frontage at `100 m`. It no longer calls a continuous-terrain width
  measurement, scales the layout to the shorter side, or compresses individual
  formation centres toward the low point. Each formation receives its full
  geometric centre and `FormOrderCustom` width; Bannerlord then evaluates the
  complete width-and-depth rectangle and distributes units around rocks and
  other unusable slots. Excess troop demand increases depth only.
- The only retained validity requirement is the shared low-point rank origin,
  which is already chosen from a navigable point. A formation's laterally
  offset geometric centre is deliberately not pre-clamped. `MovementOrder`
  handles an unusable formation origin with the stock alternate-position path,
  while the width remains unchanged, preserving the same behavior as a player
  dragging a formation across obstructed ground.
- Natural frontage now uses each line formation's stock `MaximumWidth`, which
  represents its one-rank width at the current native interval. If the sum is
  below `100 m`, small forces remain naturally narrower. If it exceeds the
  limit, all active formations share the complete `100 m` frontage in
  proportion to those natural widths; the result is no longer limited by
  their earlier, already-deep `Formation.Width` values. After each movement
  order is applied, the stored arrival target is refreshed from the engine's
  actual `Formation.OrderPosition`, so a stock alternate origin cannot cause a
  false march timeout.
- The final Release build completed with `0` errors and the existing `44`
  nullable warnings. All `24` deployable source files are SHA-256-identical to
  the live module and all `17` XML files parse. The live normal-client module
  has no editor-only directory, ZIP, or checksum entry. Client and editor DLLs
  match at SHA-256
  `473C67F45319C2A7F39D5498528E24B3459318EE601E10FB2E467F0D2A5B6DD2`.
  Repository/live README hashes match at
  `0F9464F91F7272703CA6252FB4A6D30D9223D580391AAD25EA5F0D57194AB6EC`
  for Chinese and
  `6715E7AEAE81DCA0A2CEBEAA3C3B62C942A3861CDDD90CA8BCF352FFD6AE1412`
  for English.
- ILSpy output for the deployed client DLL is stored at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\deployed_native_obstacle_rank\GreyWardenPolicePurity.GreyWardenFieldSparringMissionController.decompiled.cs`.
  It confirms the `100 m` limit, `Formation.MaximumWidth`-based allocation,
  unchanged `FormOrderCustom` widths, native order-position refresh, and the
  fixed-`200 m` low-line selection. It contains no `GetUsableLineWidth`,
  `MeasureNavigableLineSide`, or lateral-scale logic. No archive, commit,
  push, tag, or GitHub Release was created.

# 2026-07-18 final v1.4.7-r3 replacement release

- The user accepted the completed town/field sparring system as the actual
  `v1.4.7-r3`. The earlier Git tag and GitHub Release with the same name were
  an incomplete intermediate publication. This formal task intentionally
  replaces that tag, release description, ZIP, and checksum rather than
  creating `r4`.
- The player-facing Chinese and English READMEs were rewritten against the
  real public baseline, GitHub release/tag `v1.4.7-r2`. They now contain one
  concise formal `2026-07-18 v1.4.7-r3` entry covering only player-visible
  differences: full English/CNs support; the troop and mission-local combat
  mastery rebalance; complete native town-arena and formation-based field
  sparring; authored-route, low-ground, fixed-width and native obstacle-aware
  field formation behavior; the player alternative-attack fallback exception;
  and the encyclopedia, returning-patrol, and village-defense text fixes.
  Intermediate `r3/r4/testing` wording and step-by-step test history remain
  absent from the shipped README.
- Contact information now distinguishes personal QQ `157652226` from the
  public QQ discussion/download group `981323752`. The group is described as
  a place for discussion, feedback, and file downloads in both READMEs.
- A final Release build succeeded with `0` errors. Repository and live module
  contain `24` matching deployable source files; all `17` XML files parse;
  client and editor binaries match. The deployed client DLL SHA-256 is
  `473C67F45319C2A7F39D5498528E24B3459318EE601E10FB2E467F0D2A5B6DD2`.
- The protected asset packages remain byte-identical:
  - `gwp_inherited_legacy_assets.tpac`:
    `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - `gwp_black_gold_shield.tpac`:
    `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
- The formal archive was rebuilt from the verified live runtime at
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7.zip`
  with its sibling `.sha256`. It contains one top-level `GreyWarden` directory
  and `28` runtime files: both asset packages, client binaries, GUI,
  ModuleData, ModuleSounds, Shaders, `SubModule.xml`, and both player READMEs.
  It contains no editor workspace, editor binary, PDB, source asset, nested
  archive, checksum, or diagnostic file. Size: `350,379,743` bytes. SHA-256:
  `925B3D2B9CFAF92A6BFDF29172A9642247E839778018A8218C7EAD5E0493C4FE`.
- The new archive was extracted under
  `C:\tmp\gwp-r3-final-extract\GreyWarden` and compared file by file against
  the staging tree at `C:\tmp\gwp-r3-final-package\GreyWarden`: `0` missing,
  `0` hash mismatches, `0` extra files, and `0` forbidden entries. The final
  Modules-directory archive hash matches the verified temporary archive.
- This entry is the source of truth for the replacement `main` commit,
  force-updated annotated `v1.4.7-r3` tag, and updated GitHub Release assets.

# 2026-07-19 clean-shutdown native crash diagnosis

- The latest session was reported as started through the desktop shortcut
  `C:\Users\lucif\Desktop\骑砍中文站Mod管理器.lnk`, whose verified target is
  `C:\Users\lucif\AppData\Local\Programs\modmaster\骑砍中文站Mod管理器.exe`.
  Game process `27920` ran from `04:26:52` to `04:57:40`; gameplay and mission
  teardown completed normally. `rgl_log_27920.txt` reached `Start Game Final Cleanup`,
  deleted the game and managed interface, and reported `There are no living
  managed objects` before logging stopped. `rgl_log_errors_27920.txt` contains
  no managed exception or Grey Warden error.
- Windows Application Error event `1000` then recorded a real native access
  violation in the official executable: `Bannerlord.exe` / game build
  `v1.4.7.117484`, faulting module `TaleWorlds.Native.dll`, exception
  `0xc0000005`, offset `0x000000000074B3F1`. WER event `1001` archived report
  ID `4319394d-6688-45bc-bc7f-c49a4d437382` under
  `C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_Bannerlord.exe_5ef6f7d0c155a37c4ed93439c7161f7f5191c8_ebd6fe1f_a8b9f016-7f7f-4ca8-bf54-055dce82cb3b`.
- The same native module and exact `0x74B3F1` offset occurred at `10:19:34` on
  2026-07-18. That earlier `rgl_log_40152.txt` also reached the identical clean
  managed-interface shutdown and reported no living managed objects. Nearby
  native cleanup offsets `0x74B13F` and `0x74B34A` were also observed on
  2026-07-16. This is therefore a repeatable final native-cleanup failure, not
  a field/town sparring state-machine exception or a newly introduced managed
  object leak.
- The watchdog arguments match the manager-generated command already isolated
  on 2026-07-16: `/anticheat` is forced and the official modules are ordered
  `Native, SandBoxCore, CustomBattle, Sandbox, StoryMode, BirthAndDeath,
  FastMode`. The manager itself does not need to inject a DLL for that launch
  command to change shutdown behavior.
- This recurrence is consistent with the earlier controlled comparison: two
  manager-style GreyWarden-only runs crashed after `Managed Interface deleted`,
  while two official-style runs using the official module order without
  `/anticheat` exited without a WER crash. The established unresolved variable
  remains the manager's module ordering and/or forced `/anticheat`, not the
  sparring implementation. No further user test or Grey Warden code change was
  requested for this recurrence; retain it as accumulated evidence.
# 2026-07-19 permanent AI criminal ledger and split deterrence

- Replaced the capacity-limited pending AI `CrimePool._pool` and the separate
  deterrence dictionary with one canonical `CrimeRecord` per non-player lord,
  keyed by stable `Hero.StringId`. Each record stores the latest case metadata,
  the oldest unresolved-case time, permanent total crime count, permanent total
  Grey Warden arrest count, open/closed state, direct deterrence, clan-shared
  deterrence, and the few timestamps needed for recovery. Save growth is
  therefore bounded by the number of lords rather than the number of offenses.
- Police tasks now store only `TargetCrimeId` plus live workflow flags. Their
  `TargetCrime` property resolves the canonical ledger record, eliminating the
  former duplicate embedded crime copy. Failed or displaced tasks call
  `ReopenCase` and do not increment the permanent crime count. This keeps the
  small task table because it represents active police operations rather than
  historical data.
- Removed the `PoliceClanMemberCount` admission cap and the crime monitor's
  `IsAccepting` early exits. Every distinct detected raid, villager attack, or
  caravan attack can increment the responsible lord's permanent count even if
  the same lord already has an unresolved case. Dispatch scans unassigned open
  cases by oldest `OccurredTime`, using police distance only as the tie-break.
- `HeroPrisonerTaken(PartyBase, Hero)` is now the authoritative arrest event.
  A capture increments the permanent arrest total only when the captor is a
  Grey Warden clan party, regular patrol, or delayed enforcement patrol and the
  prisoner already has a real criminal record. This avoids treating a mere
  battle defeat as an arrest and is independent of map-event cleanup ordering.
- Direct and clan-shared deterrence are stored separately but share the existing
  total cap and crime-desire multiplier. A real arrest adds direct deterrence
  based on the permanent arrest count; the new direct gain has priority at the
  cap and each eligible clan member receives half of that new gain as shared
  deterrence. Recovery subtracts once from the combined total and scales both
  components proportionally. Reaching zero clears only temporary deterrence;
  permanent crime and arrest totals remain in the save.
- Deterrence conversation source is selected from the larger effective
  component, with direct winning ties. Responses are split into personal-arrest
  and clan-event families, then chosen from honorable/merciful, calculating,
  valorous, hostile, or neutral attitudes. The old uniformly fearful wound and
  nightmare lines are no longer used. English source text and Simplified
  Chinese entries were added for every new line.
- The encyclopedia button now presents a permanent criminal record together
  with direct deterrence, clan deterrence, combined deterrence, arrest totals,
  crime totals, suppression multipliers, last enforcement, status, and
  location. Player-facing Chinese and English READMEs were updated in the same
  change.
- Old `gwp_cp_*` and `gwp_det_*` save layouts are intentionally not migrated,
  per the user's explicit decision that old-save compatibility is unnecessary
  at the current pre-adoption stage. New canonical keys use `gwp_ledger_*`,
  `gwp_l_*`, and `gwp_lt_*`.
- Final Release build completed with `0` errors and the pre-existing nullable
  warnings only. The deploy step copied the runtime module and editor binary.
  Post-deploy verification found `24` deployable files with `0` missing/hash
  mismatches, `17` XML files with `0` parse failures, matching client/editor
  DLLs, matching Chinese/English READMEs, and `0` forbidden live root entries
  (`Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum). The deployed
  client DLL SHA-256 is
  `2115F1677BEA1DA93524CCE514D83F14E11C1E9EB4D014D8728B5432061BBEDA`.
- ILSpy `10.1.1.8388` successfully decompiled the deployed client DLL into
  `.codex_tmp/deployed-ai-ledger-final` (`83` C# files). Decompiled-output probes
  confirmed the new `gwp_ledger_count` save path, `HeroPrisonerTaken`
  subscription, permanent `TotalCrimeCount`, split
  `DirectDeterrencePoints`, and the new personality dialogue keys.

# 2026-07-19 nearest-open-case dispatch adjustment

- Confirmed that `CrimeRecord.HasOpenCase` is the lightweight operational
  marker: permanent crime/arrest totals remain after it is cleared. A real
  Grey Warden capture clears the marker through `CrimePool.RecordArrest`.
- Changed `CrimePool.GetNearest` and `GetNearestNonPlayer` from oldest-case-first
  ordering to pure distance ordering across valid, unassigned records whose
  `HasOpenCase` marker is set. Each newly idle police party therefore selects
  the nearest currently pursuable unresolved offender from its own position.
- Removed the now-unused oldest-then-distance selector. Crime occurrence times
  remain in the canonical ledger for history and future presentation, but no
  longer control dispatch priority.
- Updated both player-facing READMEs to describe nearest-offender dispatch.
- Release build after the distance-priority adjustment succeeded with `0`
  errors. Repository/live verification again found `24` deployable files with
  `0` differences and `17` valid XML files with `0` failures; client/editor
  DLLs and both READMEs match. Current deployed DLL SHA-256:
  `BB4519B95901D48956B7075241688BD0472D94F48ABED4845DD296933420B24B`.
- ILSpy decompilation of the deployed `CrimePool` was saved at
  `.codex_tmp/CrimePool-nearest-final.cs` and confirms that both police dispatch
  entry points now call the distance-only `SelectNearest` ordering.

# 2026-07-19 co-captured witness deterrence

- Tightened `HeroPrisonerTaken`: an AI lord now gains a permanent Grey Warden
  arrest and direct deterrence only when that lord's canonical ledger has
  `HasOpenCase == true` at the moment of police capture. A lord with only old,
  closed criminal history is treated like any other non-offender in that
  battle and does not receive another arrest or direct deterrence.
- Added a non-serialized `PoliceCaptureBatch` keyed by the active `MapEvent`.
  It records actual heroes captured by Grey Warden/patrol parties, separates
  open-case offenders from no-open-case witnesses, and records each offender's
  newly added direct deterrence divided by two. The batch is discarded when
  `MapEventEnded` fires and is never written to the save.
- At map-event completion, every co-captured eligible witness receives the
  half-strength amount as `SharedDeterrencePoints` only. This changes neither
  `TotalCrimeCount`, `TotalArrestCount`, nor `HasOpenCase`, and the witness's
  clan is not traversed, so there is no second-generation propagation.
- The actual offender's existing first-generation clan shock remains. A
  co-captured witness belonging to that same clan is skipped by the witness
  pass for that offender because the clan pass already granted the identical
  half-strength amount; this prevents double credit for the same arrest.
- Multiple open-case offenders captured in one battle each contribute one
  half-strength witness event. All additions remain subject to the common
  nine-point cap. `RegisterPoliceArrest` already gives direct deterrence
  priority: new direct points replace shared points at the cap before the
  combined total can exceed nine.
- Updated both player-facing READMEs. No new localization keys were required;
  witness responses intentionally use the existing clan/shared-deterrence
  dialogue family.
- Final Release deployment completed with `0` errors. A first verification
  rebuild was launched without permission to write the external game folder;
  MSBuild could compile the assembly but failed while replacing the live
  client DLL. Re-running the same Release build with the required game-folder
  permission immediately restored and deployed the client and editor DLLs.
  This was a filesystem permission failure, not a compiler or gameplay-code
  failure.
- Post-deploy verification found `24` deployable source files with `0` missing
  files or hash mismatches, `17` XML files with `0` parse failures, matching
  client/editor DLLs, matching Chinese/English READMEs, and `0` forbidden live
  root entries (`Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or
  checksum). The deployed client DLL SHA-256 is
  `2DF60090A1DDD012E78CBE567C183918256CB338498D3E5EEF7888F62E3B076C`.
- ILSpy decompiled the deployed `PoliceAIDeterrenceBehavior` into
  `.codex_tmp/PoliceAIDeterrenceBehavior-witness-final.cs`. The output confirms
  the `HasOpenCase` gate, `PoliceCaptureBatch`, `MapEventEnded` batch handling,
  same-clan duplicate suppression, and witness-only calls to
  `RegisterSharedFamilyDeterrence`.

# 2026-07-19 layered AI deterrence dialogue and player identity

- Replaced the previous fixed-priority deterrence greeting with a four-layer response model. The selected line now combines the dominant deterrence source (`Personal` or `Family`), current combined deterrence tier (`0.25-3` low, `>3-6` medium, `>6-9` high), a personality-weighted voice, and whether the player has accepted Grey Warden recruitment.
- Direct deterrence is the personal source and shared deterrence is the family/witness source. The larger current effective component selects the family of lines; direct wins an exact tie. Both components continue to use their existing proportional decay before dialogue selection, so the displayed attitude follows current rather than historical peak deterrence.
- Personality no longer locks a lord to the first matching trait. Positive Honor, Calculating, Mercy, and Valor select the honorable, calculating, merciful, and valorous voices; negative Mercy selects the cruel voice. Each active trait uses weight `1 + 2 * trait level`, while neutral always keeps weight `1`. This makes stronger traits more likely without making them deterministic, and lets mixed personalities produce several believable responses.
- Player identity is read from the existing saved `gwp_recruitment_accepted` state exposed by `PlayerBountyBehavior.IsRecruitedByGreyWardens`; the Grey Warden clan id is also recognized for future direct membership. Outsider lines advise or warn the player and never imply that the player made the arrest. Recruited-player lines recognize the black-robed bounty-hunter role and address how the player should use Grey Warden authority. Membership wording deliberately does not assume the player is currently wearing the black outfit, because the saved recruitment identity persists when equipment changes.
- Added `GwpAiDeterrenceDialogueCatalog.cs` with six source/tier introductions, 36 source/tier/personality cores, and 36 player-role/tier/personality audience clauses. This produces 72 complete response combinations while keeping authorship and localization maintainable. English source text and all 78 Simplified Chinese localization entries are present. Obsolete uniformly fearful `gwp_ai_deterrence_*` wound/nightmare strings were removed.
- The deterrence greeting remains a 10% replacement during an ordinary one-to-one lord conversation and remains disabled in immediate `CapturedLord` and `FreeOrCapturePrisonerHero` prisoner-decision contexts. The random trigger and selected intro/response are now cached for the conversation, because Bannerlord evaluates both dialogue nodes separately; without caching, the second node could reroll a different personality or fail after the first line had already appeared.
- The first Release build exposed one new compiler error: `MBRandom` was unresolved in `GwpAiDeterrenceState.cs`. The cause was the missing `TaleWorlds.Core` import; adding that namespace fixed it. The full rebuild then succeeded with `0` errors and only the existing 44 nullable warnings. A final incremental Release build after README updates succeeded with `0` warnings and `0` errors.
- Final deployment verification found `24` deployable source files with `0` missing/hash mismatches, `17` XML files with `0` parse failures, identical client/editor DLLs, identical Chinese/English source and live READMEs, and `0` forbidden live root entries (`Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum). Deployed client DLL SHA-256: `8CED48CCA26963D2EF5F27F1892AA07B81541AE646734B97555C5F1A87A61EC7`.
- ILSpy `10.1.1.8388` decompiled the deployed classes into `.codex_tmp/GwpAiDeterrenceState-layered-final.cs`, `.codex_tmp/GwpAiDeterrenceDialogueCatalog-layered-final.cs`, and `.codex_tmp/PoliceAIDeterrenceBehavior-layered-final.cs`. Probes confirmed the `3/6` tier boundaries, weighted selector, recruitment-state role check, Warden high/honorable key, 10% chance, and per-conversation cached output.
# 2026-07-19 official five-axis deterrence dialogue test build

- Verified the current game build directly rather than inferring its personality
  model from the encyclopedia display. The installed
  `TaleWorlds.CampaignSystem.dll` defines five non-hidden personality traits,
  each ranging from `-2` to `2`: `Honor`, `Valor`, `Mercy`, `Generosity`, and
  `Calculating`. The official Simplified Chinese names are 荣誉、胆气、善恶、胸怀、谋略.
  `CampaignUIHelper.GetHeroTraits()` returns all five, while
  `EncyclopediaHeroPageVM` omits any trait whose value is zero. A particular
  hero can therefore show four dimensions in the encyclopedia even though the
  underlying system has five.
- Replaced the incomplete personality selector with ten signed response voices:
  high/low Honor, Valor, Mercy, Generosity, and Calculating. Selection weight is
  the absolute value of each nonzero native trait, so a `+2` or `-2` direction
  is twice as likely as a `+1` or `-1` direction. The neutral voice is available
  only when all five values are zero. The existing dominant personal/family
  source, low/medium/high deterrence tier, and outsider/recruited-player layers
  remain intact.
- The regenerated bilingual catalog contains `6` source/tier introductions,
  `66` source/tier/voice cores, and `66` player-role/tier/voice clauses, for
  `138` localized keys. This covers all ten official positive/negative trait
  poles plus the all-zero fallback across both deterrence sources and all three
  intensity tiers. The Simplified Chinese XML contains every referenced key.
- For this explicit test build, changed `DeterrenceGreetingChance` from `0.1f`
  to `1f`. Any otherwise eligible ordinary one-to-one conversation with at
  least `0.25` effective deterrence now passes the greeting roll. Main-hero,
  Grey-Warden-hero, immediate captured-lord, and prisoner-decision exclusions
  remain unchanged, and the result is still cached once per conversation.
- Fixed premature localization resolution in `GreyWardenAdoptionLogEntry`.
  Previously `GwpText.Get()` converted the template to a string before
  `HERO.LINK` and `VILLAGE` were assigned, producing a sentence with both
  values blank. The entry now creates the `TextObject` first, assigns both
  variables, and only then lets the engine resolve it. Applied the same fix to
  deterrence introductions so `HERO_NAME` is not erased before assignment.
- Final Release build succeeded with `0` warnings and `0` errors after the
  README update. Deployment verification found `24` runtime deployable files
  with `0` missing/hash mismatches after excluding the intentional `Assets` and
  `AssetSources` editor-recovery trees, `17` XML files with `0` parse failures,
  identical client/editor DLLs, matching Chinese/English source and live
  READMEs, and no forbidden editor directory, ZIP, or checksum in the live
  module. Deployed DLL SHA-256:
  `2C169C242FA99F305961FECB8E7A6FECA004BF5032769760999462CA6F37D725`.
- ILSpy `10.1.1.8388` decompiled the four deployed classes into
  `.codex_tmp/deployed-deterrence-test-final`. Probes confirmed all five native
  trait reads, all ten signed voice poles, the always-passing `<= 1f` test roll,
  the new catalog poles, and deferred `HERO.LINK`, `VILLAGE`, and `HERO_NAME`
  variable assignment. No Git commit, push, tag, package, or release was made.

# 2026-07-19 persistent righteous-kill reputation progress

- Confirmed the previous positive-reputation path used `playerKillCount / 10`
  independently in each qualifying victory. It awarded one point per complete
  ten personal kills in that battle and discarded every remainder at battle
  end. The same isolated calculation existed both for ordinary good-deed
  battles and for helping Grey Wardens apprehend a criminal.
- Added `PlayerBehaviorPool.GoodDeedKillProgress`, constrained to `0..9`.
  `AccumulateGoodDeedKills` combines the saved remainder with the current
  qualifying battle's personal kills, returns one reputation point per complete
  ten, and retains only the remainder. Thus `6 + 3 + 1` across three qualifying
  victories grants one point, while `27` with no prior remainder grants two and
  retains seven.
- Both positive-reputation paths now use this common accumulator: defeating
  bandits, rescuing villagers or caravans, defending a village from a raid, and
  helping Grey Wardens defeat an offender. Non-qualifying battles, sparring,
  and criminal-side battles do not add to or clear this progress.
- Persisted the remainder under `gwp_good_deed_kill_progress` in the existing
  player behavior save block. New games and `ClearAll` reset it to zero; loading
  restores and clamps it to `0..9`. This adds one integer per save rather than a
  battle history.
- The negative-reputation paths were deliberately left unchanged. Criminal
  actions and battles still settle their existing loss independently per event
  or battle; losses neither consume nor reset the saved righteous-kill remainder.
- Updated both player-facing READMEs. No localization key was added because the
  existing reputation-award notifications remain unchanged and appear only
  when at least one full ten-kill unit is converted into reputation.
- Final Release build succeeded with `0` warnings and `0` errors. Live
  verification found `24` runtime deployable files with `0` hash mismatches,
  `17` XML files with `0` parse failures, identical client/editor DLLs,
  matching Chinese and English READMEs, and no live editor-only directory, ZIP,
  or checksum. Deployed DLL SHA-256:
  `2280184ABAC0008FB005FF25F7F881882D0C07AB077984D42F3645093C29A51F`.
- ILSpy `10.1.1.8388` decompiled the deployed `PlayerBehaviorPool`,
  `GwpRuntimeState`, and `PlayerBehaviorMonitor` into
  `.codex_tmp/deployed-persistent-reputation-final`. The output confirms the
  `/ 10` and `% 10` accumulator, the save key, both positive accumulation call
  sites, and the removal of per-battle `playerKillCount / 10` calculations. No
  commit, push, archive, tag, or release was created.


# 2026-07-19 CourierMessenger hero-page compatibility

- Controlled tests with GreyWarden, `CourierMessenger v1.2.2`, and its four
  prerequisites established that new campaigns start and reload, GreyWarden's
  encyclopedia controls work, and Courier's injected button appears but remains
  disabled. The user repeated the test after the first compatibility build in
  both the existing test save and another new campaign; the button was still
  disabled. Old-save migration is explicitly outside the requested scope.
- The exact original conflict remains proven: Courier injects
  `EncyclopediaHeroPageInject.xml`, but declares
  `[ViewModelMixin("RefreshValues")]` only for the exact native
  `EncyclopediaHeroPageVM`. GreyWarden previously registered the derived
  `[EncyclopediaViewModel(typeof(Hero))] GwpEncyclopediaHeroPageVM`, so Courier's
  exact-type lookup did not initialize its button properties and command.
- The first attempted bridge copied Courier's mixin type into UIExtenderEx's
  dictionary under `GwpEncyclopediaHeroPageVM`. This was insufficient and is now
  removed. UIExtenderEx also patches each registered VM constructor and refresh
  method; changing only its dictionary never installed those patches for the
  derived GreyWarden page. The missing success marker in the 10:51 test log was
  not by itself reliable because `Debug.Print` messages are not generally
  emitted into `rgl_log`, but the repeated disabled-button test proves the
  bridge did not produce a live mixin instance.
- The replacement-page architecture has now been removed instead of adding
  another Courier-specific workaround. GreyWarden no longer declares any hero
  `EncyclopediaViewModel` and no longer supplies a
  `GwpEncyclopediaHeroPageVM` subclass. A Harmony postfix on the native
  `EncyclopediaHeroPageVM(EncyclopediaPageArgs)` constructor attaches only
  GreyWarden's `DeterrenceButtonText`, `DeterrenceButtonHint`, and
  `ExecuteOpenDeterrenceDetails` bindings to that individual native VM. The
  existing GreyWarden prefab still displays the record button, while the page's
  runtime type remains exactly native for Courier and other exact-type mixins.
- `GwpNativeViewModelExtension` creates an instance-local copy of Bannerlord's
  native binding dictionaries before adding the three GreyWarden members. It
  uses only the already bundled Harmony and TaleWorlds runtime; there is no
  compile-time or load-time reference to CourierMessenger, UIExtenderEx,
  ButterLib, or MCM. GreyWarden therefore retains zero external prerequisites.
  Courier is neither detected nor invoked by GreyWarden and continues to own
  its availability rules, price, hint, click event, and campaign behavior.
- Release build succeeded with zero errors. Final deployed DLL version is
  `1.4.7.0`, SHA-256
  `3379B6E1AFEF560421A87B945C94ABE2BCC52A6B9FF0F518637287196AFC7EE0`.
  Mirror verification found `24` deployable files with `0` missing/hash
  mismatches, `17` XML files with `0` parse failures, identical client/editor
  DLLs, matching source/live Chinese and English READMEs, and no live
  `Assets`, `AssetSources`, `RuntimeDataCache`, ZIP, or checksum artifacts.
- ILSpy decompiled the deployed build into
  `.codex_tmp/deployed-native-hero-page`. The deployed type list contains
  `GwpEncyclopediaHeroPageExtension`, its native-constructor patch, and
  `GwpNativeViewModelExtension`; it contains neither
  `GwpEncyclopediaHeroPageVM` nor the removed Courier compatibility bridge.
  In-game compatibility still requires the next user test; static validation
  proves the native-page architecture and deployment, not Messenger's runtime
  result.
- No Git commit, push, archive, tag, or release was made in this iteration.

# 2026-07-19 formal v1.4.7-r4 release

- The user verified in game that the native hero-page refactor fixes the
  GreyWarden/CourierMessenger conflict: GreyWarden's record control and
  Messenger's formerly disabled encyclopedia button now work together.
  GreyWarden still has no external prerequisite requirement.
- The repository, module metadata, and existing public releases all target
  Bannerlord/GreyWarden `1.4.7`; the spoken `1.4.6` reference was therefore
  treated as voice-transcription drift. This publication is tagged
  `v1.4.7-r4` and compares against the actual preceding public release,
  `v1.4.7-r3`.
- Both player READMEs were rewritten as a single concise `v1.4.7-r4` entry.
  Earlier release sections were removed. The new entry describes only visible
  differences from `r3`: permanent AI crime/arrest history; separate open-case,
  personal-deterrence, clan-deterrence, witness-capture, and nearest-offender
  handling; personality/source/intensity/player-role deterrence greetings;
  cross-battle righteous-kill reputation progress; corrected adoption text;
  and native-page Messenger compatibility. The current-playable summary and QQ
  group `981323752` remain.
- Release build succeeded with zero errors. The deployed client DLL version is
  `1.4.7.0`, SHA-256
  `3379B6E1AFEF560421A87B945C94ABE2BCC52A6B9FF0F518637287196AFC7EE0`.
  Repository/live verification found `24` deployable files with `0`
  missing/hash mismatches, `17` XML files with `0` parse failures, identical
  client/editor DLLs, matching source/live Chinese and English READMEs, and no
  forbidden editor directory, nested ZIP, or checksum inside the live module.
- The formal player archive is
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r4.zip`
  with its sibling `.sha256`. It contains one top-level `GreyWarden` directory
  and `27` runtime files. It excludes `Assets`, `AssetSources`,
  `RuntimeDataCache`, editor binaries, PDBs, source FBX/PNG files, logs, dumps,
  nested archives, and checksums. Size: `349,741,341` bytes. SHA-256:
  `E3073DE8B81A38395A930260BE3A3B8C2AACE12F3B786F5A9BC20284E5A4E3C1`.
- The final archive was extracted to `C:\tmp\gwp-r4-extract\GreyWarden` and
  compared file by file with `C:\tmp\gwp-r4-package\GreyWarden`: `27` files
  on each side, `0` missing/hash mismatches/extras, and `0` forbidden entries.
  Protected TPAC hashes remain:
  - `gwp_inherited_legacy_assets.tpac`:
    `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`
  - `gwp_black_gold_shield.tpac`:
    `2A572A2FD5914EF7EE84920F765CA3919CFA64D54D74764F318D3F9AD466E33B`
- GitHub release retention is now explicit: for the same Bannerlord/mod version,
  retain only the newest two public releases. After publishing `v1.4.7-r4`,
  keep `v1.4.7-r3` and `v1.4.7-r4`; delete the older `v1.4.7` and
  `v1.4.7-r2` GitHub Releases and their release tags. Milestone tags such as
  `bannerlord-v1.4.7` are not public release records and remain untouched.
# 2026-07-19 release archive filename rule

- Every formal player archive must preserve the complete public release
  identifier in its filename, including the revision suffix. Required pattern:
  `GreyWarden-v{game/mod-version}-r{revision}.zip`; examples:
  `GreyWarden-v1.4.7-r3.zip` and `GreyWarden-v1.4.7-r4.zip`.
- Never shorten a formal revision archive to `GreyWarden-v1.4.7.zip`. The
  sibling checksum must use the same complete basename plus `.sha256`, and its
  text must name that exact ZIP. Before reporting a package complete, verify the
  ZIP filename, checksum filename, checksum text, release tag, and README
  version all carry the same revision identifier.
- The current local r4 archive and checksum were renamed accordingly; their
  bytes and SHA-256 remain unchanged.


# 2026-07-19 Grey Warden native desire integration

- Scope: replaced Grey Warden strategic-map command injection with a final-registered `GreyWardenPartyDesireBehavior`. It participates in Bannerlord v1.4.7's `PartyThinkParams` score auction after native and existing mod listeners have produced their candidates. No Harmony patch to the engine's think loop was required.
- Case duty is represented by `AiBehavior.GoAroundParty`. This works before a police war exists, so the assigned party can approach a neutral offender; the existing enforcement hourly distance check still declares war at the configured range, after which the native tactical AI can engage. Player targets retain their existing dialogue-before-war path.
- Resource arbitration has three states. Ready parties receive a stable police-duty floor. Low-resource parties keep the case but reduce its duty score below a boosted settlement visit. Critical parties temporarily add no duty candidate and give an eligible town visit the strongest score. Once food, strength, and wounded ratios recover, the retained task naturally wins again.
- Native `GoToSettlement` and `PatrolAroundPoint` candidates remain available. Raid, siege, army, generic war-target, and other incompatible kingdom duties are zeroed only for managed Grey Warden parties. The visit path therefore continues to drive native market food purchases, recruitment, healing, loot sales, and prisoner sales.
- Temporary map roles use expiring desire requests rather than movement commands: approach, escort, or visit. This covers player pickets, recruitment heralds, enforcement-delay patrols, bounty escorts, captive escorts, village relief travel/stay, and return/disband travel. Requests are refreshed by their owning behavior and disappear after eight hours if not renewed.
- Removed all Grey Warden strategic uses of `SetDoNotMakeNewDecisions(true)`, `SetMoveEngageParty`, `SetMoveEscortParty`, `SetMoveGoToSettlement`, and `SetMoveModeHold`. The desire layer only clears legacy locks, resets initiative, and requests a rethink; it never writes a map destination.
- Removed the per-member daily `1000`-denar salary call. `PoliceResourceManager` no longer generates grain or fills a party to its size limit after a task or in a town. Existing compatibility entry points now only request an AI rethink and never create resources or issue movement orders.
- Formal lord parties now use the native lord-party spawn roster and native recruitment. The existing six-hour purification remains the sole conversion step: any recruited non-Grey-Warden regular becomes `gwrecruit`. Ship assignment was not changed in this pass because the user explicitly deferred the wider economy redesign and scoped the immediate resource removal to money, food, and free troop refills.
- Static validation: `dotnet msbuild ... /t:Compile /p:Configuration=Release` completed with zero compiler errors. Source audit found no remaining Grey Warden strategic `SetDoNotMakeNewDecisions(true)` or direct engage/escort/settlement/hold movement call. `GreyWardenDesertersCampaignBehavior` retains its independent bandit patrol command and battle-mission formation orders remain untouched because neither controls Grey Warden police parties.
- Runtime validation still required in game: neutral approach, near-distance war declaration, temporary supply diversion and case resumption, genuine market spending, native recruitment plus six-hour purification, loot/prisoner sale, player dialogue/capture escort, village relief, temporary patrol return, and save/reload during both pursuit and recovery.
- No commit, tag, archive, or GitHub release was created for this development build.

- Final local compile artifact: `GreyWardenPolicePurity/obj/Release/GreyWardenPolicePurity.dll`, size `466432` bytes, SHA-256 `8EA585F12E8B063787EF43E9D0C95379E024548939BF12C5FBC67138E87E6E74`. ILSpy confirms the deployed-intent type and `PartyThinkParams.AddBehaviorScore` call are present.
- Live deployment is not complete. The managed environment rejected the required elevated `dotnet build` because its approval/usage quota was exhausted. The attempted ordinary build could compile but could not write outside the repository. A subsequent read showed the live normal-client module currently has no `GreyWardenPolicePurity.dll` and no root README/SubModule files visible, while the editor directory still contains the previous `464896`-byte DLL. Do not launch an in-game test until an unsandboxed `dotnet build GreyWardenPolicePurity\GreyWardenPolicePurity.csproj -c Release --no-restore` succeeds and repository/live hashes are rechecked.


## 2026-07-19 pursuit-continuity correction

- Review of Bannerlord v1.4.7 `MobilePartyAi` confirmed the concern raised during handoff. `GoAroundParty` is not a permanent direct chase: `GetGoAroundPartyBehavior` may choose a defensive/interception point around a faster target, while ordinary initiative AI only converts a nearby enemy to `EngageParty` after its own local strength/priority evaluation. The task itself was retained, but relying on `GoAroundParty` alone did not provide a sufficiently explicit guarantee that a declared police target would remain continuously followed.
- Added a separate `Pursue` intent. Before war, police still use `GoAroundParty` to approach a neutral offender without illegal hostility. After the existing distance rule declares war, the same retained task changes to native `EscortParty`; Bannerlord's `GetFollowBehavior` continuously updates the destination to the target party's current position and therefore does not abandon the assignment merely because the offender is faster.
- When a pursued offender enters a settlement, the intent temporarily falls back to `GoAroundParty` rather than attempting to enter a hostile settlement. The existing sheltered-target gate watch, declaration, and expulsion path remains responsible for that state.
- `PoliceMobilePartyAIModel.GetBestInitiativeBehavior` now supplies a narrow close-range guarantee only when the currently selected long-term behavior is the matching pursuit `EscortParty`. Within the native initiative radius, and only while the two factions are at war and the morale/navigation attack checks pass, the original engine receives `EngageParty` for the assigned offender with a score above unrelated nearby enemies. It does not write a movement command and it cannot override a selected town-recovery desire.
- Enforcement-delay patrols and post-refusal player pickets now request the same `Pursue` intent. The existing two-day persistent-war scan still spawns one delay patrol per tracked offender; both the primary party and relief party keep following until battle, target invalidation, peace, or a critical-resource diversion.


## 2026-07-19 pursuit rule clarification

- Final user rule: only speed disadvantage is forbidden from cancelling or lowering the long-term police pursuit. Native strength, nearby ally/enemy power, morale, navigation, and other tactical engagement checks remain authoritative.
- Removed the provisional close-range `GetBestInitiativeBehavior` override because its fixed high `EngageParty` score would have bypassed the native strength comparison. The custom `PoliceMobilePartyAIModel` again delegates ordinary engagement scoring to Bannerlord.
- The final split is deliberate: a declared case selects persistent native `EscortParty` as its long-term tracking behavior, so a faster offender cannot make the task disappear; `MobilePartyAi.GetBestInitiativeBehavior` remains native, so an outmatched Warden follows without attacking. When a relief party arrives or the power balance changes, native initiative can select `EngageParty` and start the battle.


## 2026-07-19 native-desire final build and deployment

- The earlier “live deployment is not complete” entry above records the failed
  restricted-environment attempt and is now superseded. With the required
  filesystem access available, the final `Release --no-restore` build completed
  with `0` errors. Its `46` warnings are the existing nullable diagnostics plus
  `NU1900` because NuGet vulnerability metadata was unavailable; none prevented
  compilation or deployment.
- Final pursuit semantics match the clarified rule exactly. The desire layer
  emits `GoAroundParty` before police war and persistent `EscortParty` after
  war. No custom `GetBestInitiativeBehavior` override exists, so only speed is
  excluded as a reason to abandon long-term tracking; native strength, nearby
  support, morale, navigation, and other tactical checks still decide whether
  the tracking party actually changes to `EngageParty`.
- Static source audit found no managed Grey Warden strategic use of
  `SetDoNotMakeNewDecisions(true)`, direct engage/escort/settlement/hold movement
  commands, the removed salary symbol, generated food, or free troop filling.
  `git diff --check` reported no whitespace error.
- The build synchronized repository `_Module` files to
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`.
  Final verification compared `24` deployable source files: `0` missing and `0`
  hash mismatches. All `17` live XML files parsed successfully. Client and
  editor DLLs are identical with SHA-256
  `E4B1250E306D648B5EB99239F4A44CF9B407FC8EF65898A005E9B03746CA94B5`.
  The live module contains no `Assets`, `AssetSources`, or `RuntimeDataCache`
  directory and no nested archive.
- ILSpy decompilation of the deployed DLL confirms the desire type writes
  `PartyThinkParams` scores using engine enum values `8` (`GoAroundParty`) and
  `14` (`EscortParty`). The deployed `PoliceMobilePartyAIModel` contains only
  its existing `ShouldConsiderAttacking` override and no initiative override.
  Runtime behavior still requires the focused in-game pursuit/power/resource
  tests described in the handoff; no commit, tag, archive, or release was made.


## 2026-07-19 native-first allowlist and fallback-case correction

- The first desire integration was still too custom. It retained native
  `PartyThinkParams`, but imposed its own food/strength/wounded states, raised a
  chosen town to custom `7.5`/`12` floors, assigned police pursuit `6.5`/`7.25`,
  and separately modified idle patrol scores. The user's in-game observation
  that a shrinking party continued pursuing instead of resupplying showed that
  this architecture did not satisfy “native desires minus forbidden desires,
  plus a fallback police desire.” Those custom resource and patrol arbitration
  paths are now removed rather than retuned.
- Local decompilation of installed Bannerlord `v1.4.7`
  `AiVisitSettlementBehavior` is the authoritative basis for the replacement.
  Native `GoToSettlement` already combines food and food consumption, wounded
  ratio, party-size ratio, available wage budget, volunteers, leader/party
  money, sellable non-food items and mounts, prisoners, settlement food stock,
  distance, and settlement safety into the visit score. Buying food,
  recruitment, healing, selling loot, and selling prisoners therefore share
  one strategic settlement desire and execute through their existing campaign
  behaviors after arrival; they are not separate Grey Warden destinations.
- `GreyWardenPartyDesireBehavior` is now a final-registered allowlist filter.
  For managed Grey Warden parties it preserves every score generated by native
  `GoToSettlement`, `MoveToNearestLandOrPort`, `Hold`, and `None`. Native
  `PatrolAroundPoint` is preserved only when the party has no case or temporary
  police duty. Raid, siege, defence, army/join, arbitrary enemy pursuit, and all
  other kingdom-military candidates are set to zero. Native short-term fleeing,
  local power comparison, morale, and navigation initiative are outside
  `PartyThinkParams` and remain untouched.
- All police duties now enter the same auction at the single fallback score
  `1.0`. A healthy party with no stronger native need can pursue its case; a
  native settlement visit elevated by food, wounds, missing troops, loot, or
  prisoners can win without the mod detecting or estimating those needs. The
  case record remains assigned while the party visits town, so the same
  fallback candidate reappears after native needs subside. Assigned pursuit
  remains `GoAroundParty` before war and `EscortParty` after war, preserving the
  rule that speed cannot erase long-term tracking while native strength checks
  still decide engagement.
- Repeated requests for the same temporary intent now refresh only its expiry
  and do not force another rethink. The former every-hour rethink in the main
  case loop and sheltered-target loop was removed. Legacy AI unlocking and
  genuine task-state changes may request the next native hourly auction, but no
  code now calls `SetInitiative`, sets an aggressive destination, or clears a
  live native short-term flee/engage decision.
- `CrimePool.BeginTask` now enforces the assignment invariant at its data
  boundary: one police party has at most one task and a crime already assigned
  to another party cannot be assigned again. `PoliceMobilePartyAIModel` also
  rejects proactive Grey Warden attacks against parties other than the current
  case/temporary pursuit target or a bandit. This prevents an enforcement war
  against a faction from turning its unrelated lords, villagers, or caravans
  into extra police targets.
- Important economy diagnostic for the next in-game test: in native `v1.4.7`,
  `MobileParty.PartyTradeGold` returns `LeaderHero.Gold` for a lord party, while
  `Clan.Gold` returns only `Clan.Leader.Gold`. The clan-screen wealth category
  therefore does not prove that a non-leader Grey Warden commander personally
  has the `>100` denars required by the native food-visit scoring branch. No
  money was injected in this pass; if the party chooses town correctly but
  cannot buy food, inspect that individual commander's gold as a separate
  economy issue rather than raising the pursuit or resupply scores again.
- Final `Release --no-restore` build completed with `0` errors and only the
  offline NuGet vulnerability-metadata warning. ILSpy of the deployed DLL
  confirms fallback constant `1.0`, pursuit enum values `8`/`14`, the native
  allowlist (`0..2`, `13` only without a duty, and `17`), the attack-target
  gate, and the bidirectional task uniqueness check. It contains no resource
  threshold method or initiative reset.
- Live mirror verification compared `24` deployable files with `0` missing and
  `0` hash mismatches; all `17` XML files parsed; client/editor DLLs match; no
  editor-only directory or nested archive is present. Deployed DLL SHA-256:
  `5D7BF58A54C67020C937DF6BEBF9C98F107801D5439BB86643C946162C761BA7`.
  No commit, tag, archive, or release was created.


## 2026-07-20 Grey Warden case-ledger encyclopedia interface

- Added a second Grey-Warden-only button to the clan encyclopedia page. The
  existing war/adoption-details button remains unchanged; the new `案件总卷`
  button opens a dedicated modal Gauntlet overlay rather than placing an
  unbounded ledger inside the normal encyclopedia scroll area.
- `GwpCaseArchiveVM` reads `CrimePool.LedgerRecords` directly. This is the
  permanent, save-backed record collection and therefore includes both open
  and closed records. The ledger is intentionally one aggregate record per
  offender, not one row per individual criminal act. Rows are ordered by
  `LastCrimeTime` descending, then by record key for deterministic ties.
- Each row exposes the offender, open/closed state, latest offence time, current
  case-open time, latest offence type and victim, total recorded offences and
  arrests, saved map location, current assignee, and task stage. Assignment is
  resolved from `CrimePool.ActiveTasks` by `TargetCrimeId`, which reflects the
  same one-police/one-case relationship used by enforcement. Open records with
  no matching task are explicitly shown as `无人承办`; closed records remain
  visible and are marked as having no active tracker.
- The header summarizes total/open/assigned/unassigned/closed counts. A
  `刷新案卷` command rebuilds the view from live campaign state without closing
  the encyclopedia, allowing assignment and stage transitions to be observed
  during testing. The overlay is scrollable and blocks input to the underlying
  encyclopedia until closed.
- New runtime surface: `GUI/Prefabs/GwpCaseArchive.xml`; modified surface:
  `GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaClanPage.xml`.
  All new player text has English fallback strings and Simplified Chinese
  entries. Both player READMEs record the feature under `2026-07-20
  v1.4.7-r5-dev`.
- Validation: final `Release --no-restore` build completed with `0` errors and
  `46` existing nullable/offline-NuGet warnings. ILSpy of the deployed DLL
  confirms the clan command calls `GwpCaseArchiveScreen.Show`, and the ledger VM
  enumerates `CrimePool.LedgerRecords`, sorts by `LastCrimeTime`, joins current
  tasks by `TargetCrimeId`, and supplies the refresh command.
- Live deployment verification compared `25` deployable source files with `0`
  missing and `0` hash mismatches. All `18` repository and live XML files parse
  successfully; all `33` new localization references exist with no duplicate
  localization IDs. Client/editor DLLs are byte-identical, size `474624`,
  SHA-256
  `249E17C67A81D0E4EE3E448FB00ADE40BB05AA16223382AA39F99CFF367B37BB`.
  The live README matches the repository, the live case prefab matches its
  source, and the live module contains no editor-only directories or nested
  archives. No commit, tag, archive, or release was created.
- Focused in-game test: open Encyclopedia -> Clans -> Grey Wardens, confirm both
  top-right buttons appear, open `案件总卷`, verify newest-first ordering and
  scrolling, compare each open case's assignee with the corresponding map
  party, then leave the window open through an assignment change and press
  `刷新案卷`. After an arrest, confirm the record remains present but changes to
  `已结案`; create another offence for the same lord and confirm the same row is
  reopened and moved to the top rather than duplicated.


## 2026-07-20 case-save reload crash and ledger scrolling correction

- Player reproduction: start a new campaign, allow at least one AI case to be
  recorded, save and exit, then reload that save. The load raised an error
  before entering the campaign. The save itself completed successfully; this
  was a deterministic initialization-order crash during deserialization, not a
  corrupt or incomplete save write.
- Primary evidence is the local Windows Error Reporting dump
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.45916.dmp`.
  WinDbg `!analyze -v` reports `System.ArgumentNullException: Value cannot be
  null` with the managed stack `System.Linq.Enumerable.FirstOrDefault ->
  Hero.FindFirst -> CrimeRecord.get_OffenderHero -> CrimePool.Clean ->
  CrimePool.SyncData -> PoliceEnforcementBehavior.SyncData ->
  CampaignBehaviorDataStore.LoadBehaviorData -> Campaign.OnInitialize`.
  Independent dump
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.34736.dmp`
  contains the same exception and stack, ruling out a one-off load failure.
- Root cause: `CrimePool.SyncData` rebuilt the primitive case rows correctly,
  but immediately called `Clean()` while `Campaign.OnInitialize` was still
  constructing global campaign collections. A non-empty case ledger caused
  `CrimeRecord.OffenderHero` to call `Hero.FindFirst`; its internal enumerable
  source was still null at that exact phase. Empty ledgers did not enter the
  failing path, which is why the bug appeared only after cases existed.
- Correction: the loading branch now limits itself to reconstructing saved
  records and tasks. `CrimeState.Clean()` runs at the start of
  `OnSessionLaunched`, after Bannerlord has completed campaign initialization.
  `CrimeRecord.OffenderHero` additionally catches the specific early-load
  `ArgumentNullException` and leaves the lazy reference unresolved so the same
  lookup can succeed later. No keys or stored values were changed, so the
  player's already-created affected save should load directly after updating;
  a new campaign is not required.
- The first `GwpCaseArchive.xml` placed the list itself directly under the
  `ScrollablePanel` while using the surrounding region as its clip target.
  Although the first rows rendered, the scroll panel had no correctly linked
  internal scrolling canvas and therefore could not translate the list.
  Rebuilt it to match the native encyclopedia topology: the scroll panel owns
  `CaseListClip`, that clip owns the cover-children `CaseList`, and the panel
  references `InnerPanel="CaseListClip\CaseList"`, `ClipRect="CaseListClip"`,
  and its sibling vertical scrollbar. Mouse wheel and scrollbar movement now
  operate on the complete list rather than only the initially visible rows.
- Validation: final `Release --no-restore` build completed with `0` errors.
  ILSpy of the deployed DLL confirms `CrimePool.SyncData` no longer calls
  `Clean`, the lazy hero getter contains the `ArgumentNullException` guard, and
  `OnSessionLaunched` performs the deferred cleanup. Static prefab validation
  confirms the new inner-panel/clip/list relationship and cover-children list.
  All `18` repository and live XML files parse successfully.
- Live mirror verification compared `25` deployable files with `0` missing and
  `0` hash mismatches. Client and editor DLLs are byte-identical, size `474624`,
  SHA-256
  `D05FB25D38340D98280BB3D41F233750B58BAB6EE0C06F72EBC49F757597B87B`.
  Repository/live README and case-prefab hashes match; no editor-only directory
  or nested archive exists in the live module. No commit, tag, archive, or
  release was created.
- Required focused runtime retest: load the existing
  `Ironman3cJKGr9YHkab.sav` directly, confirm the campaign reaches the map and
  the saved case rows/assignees remain present, then save/reload it once more.
  Open the clan `案件总卷` with enough rows to overflow and verify both mouse
  wheel and dragging the right scrollbar can reach the oldest record.


## 2026-07-20 open-case-only ledger and numeric-history separation

- This section supersedes the earlier case-ledger description that treated
  `CrimeRecord` as a permanent combined row and instructed testers to expect a
  closed row to remain visible. The required model is now two independent
  stores: `_ledger` contains only current open cases and their event details;
  `_history` contains only per-hero cumulative crime/arrest counts and the
  numeric personal/clan deterrence state used by the hero encyclopedia.
- `CrimeRecord` no longer owns cumulative counts or deterrence fields.
  `HeroCrimeStats` deliberately has no crime type, occurrence time, last-crime
  time, map position, victim, offender-party ID, assignment, or open/closed
  flag. This prevents a closed case from surviving indirectly merely because
  its offender still needs a permanent numeric history.
- Closing an AI case through `CrimePool.EndTask`, removing an unassigned
  pending case, ending the player hunt, or cleaning a dead offender now removes
  the case row from `_ledger`. Failed enforcement remains recoverable:
  `PoliceTask` caches the current `CrimeRecord` object before removal and
  `ReopenCase` puts that same live case back into `_ledger` without increasing
  the crime count. Thus failure/reassignment does not lose an unresolved case,
  while genuine closure deletes all of its event details.
- Every detected AI crime increments `HeroCrimeStats.TotalCrimeCount`; a real
  Grey Warden arrest increments `TotalArrestCount` and updates deterrence in
  the same numeric record. The hero encyclopedia therefore keeps the numbers
  the user expects even after the current case row disappears.
- Save compatibility is explicit. Loading first reads the legacy `gwp_l_*`
  rows and extracts their old count/deterrence fields into `_history`, but adds
  only rows whose saved `open` flag is true to `_ledger`. It then reads the new
  `gwp_history_count` / `gwp_h_*` numeric store when present; those new rows
  replace the duplicated migration values. Consequently an old save keeps its
  accumulated numbers and open cases, silently discards closed-case details,
  and writes only the split format on its next save.
- `案件总卷` now filters to open cases, summarizes current/assigned/unassigned
  counts, and no longer displays closed state or cumulative history in each
  case row. The clan-page hint, English fallback strings, Simplified Chinese
  localization, player READMEs, and `docs/grey-warden-setting.md` all describe
  the same current-only behavior.
- Validation: final `Release --no-restore` build completed with `0` errors and
  `46` existing nullable/offline-NuGet warnings. ILSpy of the deployed DLL
  confirms `EndTask` removes non-player cases, `ReopenCase` re-inserts failed
  cases, legacy loading calls `MergeLegacyHistory` but admits only
  `HasOpenCase` rows, the new `gwp_history_count` / `gwp_h_*` keys are present,
  and `GwpCaseArchiveVM` contains no closed-case or cumulative-count display.
- Live mirror verification compared `25` deployable files with `0` missing and
  `0` hash mismatches; all `18` XML files parsed; all `31` case-screen/clan
  button localization references have Simplified Chinese entries with no
  duplicate IDs. Client/editor DLLs are byte-identical, size `476672`, SHA-256
  `EEF941E19D2F2C8F86D0A6790882A400D0C9DE6982822A03A654D296DEF0AD80`.
  Repository/live README files match; no editor-only directory or nested
  archive exists in the live module. No commit, tag, archive, or release was
  created.
- Focused in-game retest: load `Ironman3cJKGr9YHkab.sav`; confirm only its
  still-open cases appear and their assignees survive. Let one AI lord commit a
  new tracked offence, note the hero-page crime total, save/reload before
  resolution, then let the Grey Wardens arrest and close the case. After
  refreshing the ledger the row must disappear, while the same hero page keeps
  the crime total and increased arrest total. Save/reload once more and confirm
  the closed row does not return. Also let a pursuing Warden lose once and
  confirm that unresolved case is reopened/reassigned rather than deleted.


## 2026-07-20 assigned-case patrol suppression correction

- Player observation: the case ledger showed a Grey Warden as the assigned
  tracker, but the same map party still displayed/continued a patrol action.
  Raising the police fallback score would violate the native-first design and
  could again starve food, healing, recruitment, loot, or prisoner needs, so
  this was corrected at the allowlist and target-resolution boundaries rather
  than by increasing the `1.0` duty score.
- `ResolveIntent` previously searched `MobileParty.All` only by the case row's
  stored `OffenderPartyId`. A lord's party can be destroyed/recreated or change
  after captivity, leaving that ID stale even though `CrimeRecord.Offender`
  can resolve the hero's live party. The desire layer now uses the live
  `TargetCrime.Offender` resolver, which also refreshes the stored party ID.
- The native filter now treats `CrimePool.HasTask(party.StringId)` as an
  assigned duty even during the short interval in which a target party cannot
  be resolved. With a duty, `PatrolAroundPoint`, `Hold`, and `None` are all
  suppressed; `GoToSettlement` and `MoveToNearestLandOrPort` remain allowed,
  and native short-term flee/strength logic remains outside this filter. Thus a
  Warden may still buy food, recruit, heal, sell cargo/prisoners, or recover
  navigation, but cannot choose ordinary patrol/idle instead of an assigned
  case once those needs no longer win.
- The change does not force an immediate destination. Taking a case requests
  Bannerlord's next hourly rethink; the pursuit remains a native auction
  candidate rather than a direct movement order. A just-assigned party may
  therefore retain its old status until that hourly decision occurs, but it
  cannot select patrol again while the task exists.
- Player READMEs record the visible correction. Final `Release --no-restore`
  build completed with `0` errors and `46` existing nullable/offline-NuGet
  warnings. ILSpy of the deployed DLL confirms the independent `HasTask` gate
  and live `TargetCrime.Offender` resolution. Live verification found `25`
  deployable files and `0` hash mismatches; client/editor DLLs are identical,
  size `476160`, SHA-256
  `BD2A5E2BFA895031C002FD59D7F91C5C5154862C2E484C1CE1F7F62D08541F62`.
  No commit, tag, archive, or release was created.
- Focused retest: assign a currently patrolling Grey Warden in `案件总卷`, let
  one campaign hour pass, and confirm the party changes to approaching or
  following the offender. If it first visits a settlement for a real native
  need, allow that visit to complete and confirm it returns to the same case
  rather than resuming patrol. Repeat with an offender whose party was recently
  destroyed/recreated or released from captivity to verify live target
  resolution.


## 2026-07-20 native application-shutdown crash after successful save

- Player reproduction: choose save-and-exit, return to the initial menu, then
  close Bannerlord. Windows recorded
  `C:\Users\lucif\AppData\Local\CrashDumps\TaleWorlds.MountAndBlade.Launcher.exe.31280.dmp`
  at `2026-07-20 01:15:38 +10:00`.
- This was not a save failure. `rgl_log_31280.txt` reports the save starting at
  `01:15:30.856` and `Successfully saved` at `01:15:32.504`. The resulting
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\Game Saves\Ironman3cJKGr9YHkab.sav`
  is `5,342,092` bytes, timestamped `01:15:32`, with SHA-256
  `75AB58DF8AE0DDA8BD0D3911A511004FC2F38D1021D232BC2065DB4C3AF81EC2`.
- The crash occurred only during final native application teardown. The log
  had already returned to the initial menu, deleted the campaign game, deleted
  resources, completed `Pre Finalizing Managed Interface`, reported `There are
  no living managed objects`, and printed `Managed Interface deleted`.
  WinDbg then identifies an invalid-pointer read (`0xc0000005`) at
  `TaleWorlds.Native.dll+0x74b1f0`; the stack is native-only and contains no
  GreyWarden managed frame. Full local analysis is preserved at
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\crash-31280-analysis.txt`.
- This signature predates the current case-ledger and party-desire revisions.
  Archived WER report
  `C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_TaleWorlds.Mount_e978be8b478a106fd93641f8edccc0dc8de319ed_7140fc22_ba6e017d-275e-4c7e-b707-98f8e000306c\Report.wer`
  records the same fault module, exception code, and exact offset at
  `2026-07-18 07:03:29 +10:00`. Therefore the latest desire correction did not
  introduce this crash, and the evidence does not justify a speculative
  managed-state or serialization change.
- The observed timing does leave a practical native teardown race as the most
  useful next check: the player closed the application about `1.7` seconds
  after the initial menu activated and its background video began. Retest by
  loading the saved game, saving and exiting again, waiting at least ten
  seconds on the initial menu, and then closing the application. If it still
  crashes, retain the new dump and compare its native offset; the exact same
  offset would confirm the recurring shutdown fault, while a managed stack or
  different offset would be a separate issue.
- No gameplay/runtime source, README, build, or live module was changed for
  this diagnosis. This avoids claiming an unproven fix for a crash that occurs
  after Bannerlord has already destroyed the managed interface.


## 2026-07-20 staged long-range case approach and strict logistics priority

- Player observation superseding the first patrol-suppression correction: an
  assigned Grey Warden no longer selected ordinary patrol, but also did not
  begin travelling toward a far-away offender. The required priority order is
  now explicit: native food/recruitment/healing/sale/prisoner/ship needs first;
  case duty only when none of those needs owns a viable native candidate;
  ordinary patrol/idle is unavailable while a duty exists.
- Decompiled local `v1.4.7` source is preserved under
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\.codex_tmp\vanilla-ai`.
  `AiEngagePartyBehavior` searches only locatable parties around the acting
  party, using `EncounterModel.NeededMaximum*Distance... * 45` as its radius.
  It therefore cannot originate a normal cross-continent enemy pursuit.
  `AiPartyThinkBehavior` handles `PatrolAroundPoint`, `GoToSettlement`,
  `EscortParty`, and `GoAroundParty`, but has no winning-candidate branch for
  `GoToPoint` or `EngageParty`. The native hostile chase candidate produced by
  `AiEngagePartyBehavior` is in fact `GoAroundParty`.
- Long-range pre-war approach now inserts a point candidate at the offender's
  current known `CampaignVec2`, using `PatrolAroundPoint` solely as the native
  desire resolver's supported point-movement carrier. It does not rely on the
  offender being inside the acting party's locator/search radius. Navigation
  capability is resolved for land/naval travel. Once the existing enforcement
  layer observes the offender inside `WarDistance` and marks the task at war,
  the duty switches to `GoAroundParty`; the native short-term layer still owns
  strength, fleeing, and actual engagement.
- The point carrier is not ordinary patrol. Native
  `SetPartyAiAction.GetActionForPatrollingAroundPoint` compares the existing
  behavior and navigation type but does not compare the newly selected point,
  so a moving offender's refreshed location would otherwise be ignored after
  the first auction win. `GwpLocationDutyRefreshPatch` updates the point only
  when the location duty has already won the native auction and the known
  location moved by more than `0.25`; it never creates a destination outside
  that auction. `GwpLocationDutyBehaviorTextPatch` replaces the misleading
  stock patrol label with “travelling toward the last known position” for this
  state only. The Simplified Chinese localization is
  `gwp_location_duty_travelling`.
- Logistics priority is structural rather than another score adjustment.
  `HasNativeLogisticsNeed` recognizes the same observable categories used by
  `AiVisitSettlementBehavior`: low food days with purchasing money, materially
  wounded parties, prisoners, affordable vacancies for recruitment,
  meaningful sellable cargo, and damaged ships. While such a need exists,
  native `GoToSettlement` candidates remain intact. If any protected
  `GoToSettlement` or `MoveToNearestLandOrPort` candidate is present after
  filtering, the Grey Warden behavior adds no police candidate at all that
  hour; therefore neither the location approach nor hostile chase score can
  outrank the protected native action. Without a logistics need, routine
  settlement visits and ordinary `PatrolAroundPoint`/`Hold`/`None` candidates
  are suppressed so the assigned case becomes the true fallback.
- `EscortParty` remains only for actual escort duties. It is no longer used as
  hostile pursuit after war, avoiding friend-follow semantics on an enemy.
  Existing one-case-per-Warden ownership, automatic declaration range,
  strength-aware non-engagement, and support-party behavior are unchanged.
- Player-facing Chinese and English READMEs describe the staged approach,
  strict native logistics precedence, and corrected map status text.
- Validation: final `Release --no-restore` build completed with `0` errors and
  `46` existing nullable/offline-NuGet warnings. The build copied source module
  data and the assembly into both live client and editor module locations.
  All `18` repository XML files parse successfully. Live-mirror verification
  compared `25` deployable files with `0` missing and `0` hash mismatches.
  Client and editor DLLs are byte-identical, size `479744`, SHA-256
  `5745F65870DB34ECCBE9A688AFDB7A5123C3233B007002D550691771D93BD830`.
  ILSpy of the deployed DLL confirms the point candidate, protected-native
  early return, logistics detector, `GoAroundParty` war stage, and point-refresh
  patch. Repository/live Chinese README, English README, and Simplified Chinese
  localization hashes match. No commit, tag, archive, or release was created.
- Focused runtime retest: assign a case whose offender is on the far side of
  the continent. With a healthy, supplied, full-strength, low-cargo Warden,
  allow one native AI rethink and verify its map text becomes “正在前往…最后出现
  的位置” and its route advances toward the offender. Move the offender for
  several hours and verify the destination refreshes rather than stopping at
  the first snapshot. Then test separate Wardens with low food, recruitable
  vacancies, significant wounds, sellable loot/prisoners, or a damaged ship:
  each must finish the appropriate native settlement visit before resuming the
  same case. Finally allow a non-player offender inside `3` map units; verify
  war is declared and the Warden changes to continuous hostile chase.


## 2026-07-20 real point movement, assigned-only ledger, and AI score telemetry

- Player runtime observation disproved the previous point-carrier conclusion:
  the map label changed to the case-tracking text, but the party still did not
  travel toward the offender and continued the old patrol movement. The same
  session did not provide enough evidence to distinguish a missing native
  resupply candidate from a candidate that was later filtered or lost, so the
  resupply cause must now be decided from recorded scores rather than another
  speculative weight change.
- The exact movement cause is confirmed in the local `v1.4.7` decompile.
  `AiPartyThinkBehavior.PartyHourlyAiTick` has a winner branch for
  `PatrolAroundPoint`, but `MobileParty.RecalculateShortTermBehavior` has no
  matching `PatrolAroundPoint` branch. Calling
  `SetPartyAiAction.GetActionForPatrollingAroundPoint` can therefore set the
  displayed/default patrol state without creating the required point-to-point
  short-term movement. Updating the patrol point alone could never repair this.
- `GwpLocationDutyRefreshPatch` now translates only a winning Grey Warden
  approach carrier at the native action boundary. Its prefix replaces the
  stock point-patrol action with `SetMoveGoToPoint(position, navigationType)`
  and skips the stock call. It does not run while a settlement/logistics
  candidate wins and does not issue movement outside the completed native
  desire auction. The tracking status override now requires the resulting
  `GoToPoint` default behavior, so the UI can no longer claim location travel
  while the party remains in a patrol default state.
- `GwpAiDiagnostics` is diagnostic-only and does not enter save data or AI
  decisions. Every loaded campaign session resets
  `C:\Users\lucif\Documents\Mount and Blade II Bannerlord\GreyWarden-AI-Diagnostics.log`.
  For each managed Grey Warden party it records hourly state, raw native
  scores, filtered final scores, active police intent, whether logistics was
  protected, the police candidate added, the resolved default/short-term
  behavior, real target settlement/party/point, food, food change, calculated
  food days, party/leader/clan money, lord/patrol and faction eligibility,
  strength/recruitment ratio, wounds, prisoners, case ID, war stage, offender,
  and distance. The point-action translation also writes
  an explicit `POINT_WINNER_TO_GOTOPOINT` row.
- `tools\Watch-GreyWardenAI.ps1` tails that file while Bannerlord runs. Optional
  `-Party <id-or-name>` limits output, `-Once` prints a snapshot and exits, and
  `-Tail <count>` controls retained rows. PowerShell parser validation passes;
  before a campaign with this assembly is loaded, its expected `-Once` result
  is the explicit “diagnostic log does not exist yet” error.
- The Case Ledger now enumerates only `CrimePool.ActiveTasks`, resolves each
  task's current open `TargetCrime`, deduplicates by case ID, and sorts those
  assigned cases by latest offence time. Unassigned open records remain in the
  internal pool for future assignment but are intentionally hidden from this
  testing UI. Button hint, empty state, summary, Simplified Chinese strings,
  and both player READMEs were updated accordingly.
- Validation: `Release --no-restore` completed with `0` errors and `47`
  existing nullable/offline-NuGet warnings. ILSpy of the deployed assembly
  confirms `SetMoveGoToPoint`, assigned-task enumeration, and raw/final score
  logging. All `18` repository XML files parse. Live comparison found `25`
  deployable files and `0` missing or hash mismatches. Client/editor DLLs are
  byte-identical, size `488448`, SHA-256
  `790C93C2486A62EA854AF4893FB43661DD43D84253DCBA470FB47AF361437B47`.
  Repository/live Chinese README hashes both equal
  `CD905DD5BA4217C0DA10968F4FFA8A5877292BE320D8F31E7DE62E4A70017C20`;
  English README hashes both equal
  `67C2A1BC1C2F9E89DE4CF9D7497D10D0415E1B37D0A707F425CA3266032B263B`.
  No commit, tag, archive, or release was created.
- Focused runtime retest: load a campaign, identify one assigned case in the
  assigned-only ledger, and let at least one AI decision hour pass. A healthy
  party should produce an `AUCTION` row whose final top candidate is the
  approach carrier, followed by `ACTION ... POINT_WINNER_TO_GOTOPOINT` and a
  `RESOLVED` row with `default=GoToPoint`; its map position must then converge
  on `targetPoint`. Continue until food falls below the native threshold. The
  corresponding `AUCTION` row will show whether a raw `GoToSettlement` exists,
  whether `logisticsProtected=True`, and whether that candidate remains in
  `finalScores`; the next correction, if needed, must address the first stage
  where this chain actually breaks.


## 2026-07-20 confirmed desire-event ordering fault and final-auction hook

- The first instrumented runtime test produced decisive evidence rather than
  another ambiguous map label. The session log contains `75` `AUCTION` rows;
  all `75` report `rawScores=[]`, and the whole file contains `0`
  `POINT_WINNER_TO_GOTOPOINT` action rows. Every Grey Warden had a resolved
  `default=PatrolAroundPoint` and continued toward the same old patrol point
  around `town_EN5`, even though the early snapshot showed the correct offender
  coordinate as the sole police candidate. This proves that the police handler
  executed before the native score producers, then later native patrol scores
  overwrote its work before `AiPartyThinkBehavior` selected the winner.
- Local `v1.4.7` decompilation explains the ordering. `MbEvent<T1,T2>` inserts
  every `AddNonSerializedListener` at the head of a singly linked list and
  invokes from the head, so listener order is LIFO. Registering the Grey Warden
  behavior “last” therefore made its `AiHourlyTick` listener run first, not
  last. The prior registration-order assumption was false; consequently the
  filter never saw native food, recruitment, healing, sale, prisoner, patrol,
  or idle scores, and the diagnostic `finalScores` field was only a premature
  snapshot rather than the actual final auction.
- The behavior no longer subscribes directly to `AiHourlyTickEvent`.
  `GwpFinalDesireAuctionPatch` is a postfix on
  `CampaignEventDispatcher.AiHourlyTick`, which returns only after all event
  receivers and all native score listeners have completed. The postfix calls
  `GreyWardenPartyDesireBehavior.ProcessFinalDesires` immediately before
  control returns to `AiPartyThinkBehavior` and its winner loop. Raw diagnostics,
  identity filtering, protected native logistics, suppression of patrol/idle
  while assigned, and addition of the fallback police candidate now all operate
  on the genuinely complete score collection.
- This preserves the required priority model without a forced destination:
  native score producers still create their own settlement and survival
  desires; the Grey Warden layer removes forbidden kingdom/criminal activity,
  protects valid logistics candidates, and adds the case only as the fallback.
  If the final case point wins, the already deployed action-boundary patch then
  translates that winner to real `GoToPoint` movement. The next runtime log
  must therefore show non-empty `rawScores`, a filtered `finalScores`, followed
  by `ACTION ... POINT_WINNER_TO_GOTOPOINT` and `default=GoToPoint` for a
  supplied assigned Warden.
- On session launch, every currently managed Grey Warden party is marked for
  one immediate native rethink. This clears the practical test delay from an
  old save whose party still holds the previous patrol winner; it does not set
  a destination or bypass the corrected auction.
- Player README wording now states that the corrected filtering occurs after
  all native needs have been scored. This supersedes the earlier claim that
  adding the behavior last was sufficient.
- Validation: the final source-changing `Release --no-restore` build completed
  with `0` errors and `47` existing nullable/offline-NuGet warnings. ILSpy
  confirms the deployed assembly patches
  `CampaignEventDispatcher.AiHourlyTick`, calls `ProcessFinalDesires` from its
  postfix, and no longer subscribes that processor to `AiHourlyTickEvent`.
  All `18` XML files parse. Live comparison found `25` deployable files and
  `0` missing or hash mismatches. Client/editor DLLs are byte-identical, size
  `488960`, SHA-256
  `2A31B39F6510F37E2CCDD2C041FC35C4C5F5766E60D171E588828A9D791E15A4`.
  Repository/live Chinese README hashes both equal
  `B2477E6CDE86DA2CF331F5F32FE7C2DA1428F4CA364FC93DB7FF87E59C8E929F`;
  English README hashes both equal
  `2F826B365B8A40BD724F834D1E4F0F5AE5F4FC1DC45DEF88E057D1521D23DAB1`.
  No commit, tag, archive, or release was created.


## 2026-07-20 founder-only desire and treasury diagnostics

- The first successful final-auction runtime log confirms that native desires
  and the case fallback now coexist. Across `244` recorded auctions the
  diagnostic contains `27` `POINT_WINNER_TO_GOTOPOINT` actions; assigned
  Wardens also independently selected recruitment and settlement visits.
  Consequently the former all-patrol symptom is resolved rather than merely
  relabelled in the UI.
- The reported leader stop is reproducible in the existing log for
  `gw_leader_0_party_1` (梵蒂). At campaign hour `624935.38`, the open intent is
  still `Approach:lord_1_37_party_1`, but the native auction contains
  `GoToSettlement@castle_village_ES4_2=0.3840` and the same score for
  `village_ES1_4`. Because `logisticsProtected=True`, the assigned-case
  fallback is deliberately not added. Patrol candidates reach `3.0900` in the
  raw list but are correctly reduced to zero while the case is assigned. The
  selected village is already at the party's exact position, so the resolved
  map behavior remains `Hold` rather than issuing another movement order.
- This is a protected native settlement visit, not a return to patrol and not
  loss of the case. The same row shows `221/221` men, `0` wounded, `0`
  prisoners, `21.49` days of food, and `91,934` clan gold. Those values rule
  out low food, recruitment, healing, and prisoner delivery under the current
  protection predicate. The remaining possible predicates are sale of mounts
  or other cargo, or repair of a damaged ship; cargo sale is the most likely
  explanation, but the old log did not record the exact matched predicate and
  therefore cannot prove which of those remaining cases won.
- Diagnostics now deliberately include only parties led by the six founders
  `gw_leader_0` through `gw_leader_5`; later children, temporary patrols, and
  support parties no longer flood the trace. Every auction now includes an
  explicit `logisticsReason` value (`low_food`, `wounded`, `prisoners`,
  `recruitment`, `sell_mounts`, `sell_cargo`, `repair_ship`, or `none`). Party
  state retains `party/leader/clan` money separately; `clanGold` is the shared
  Grey Warden family treasury requested for economic observation.
- `tools\Watch-GreyWardenAI.ps1` now prints the founder-only scope and identifies
  `clanGold` as the family treasury before tailing the log. This turn changes
  diagnostics only; it does not alter AI scores, logistics thresholds, money,
  or player-visible gameplay, so no new player README entry was added.
- Validation: the `Release` build completed with `0` errors and `48` existing
  nullable/offline-NuGet warnings. The watcher parses successfully. Deployed
  assembly strings confirm the founder scope and exact logistics-reason
  instrumentation. All `18` repository XML files parse. Live comparison found
  `25` deployable files and `0` missing or hash mismatches. Client/editor DLLs
  are byte-identical, size `489472`, SHA-256
  `F3411665B4B1C1E62E1AFE4EB34A4944C803A86A3B2FB960DE771ED3FEBDE4C3`.
  No commit, tag, archive, or release was created.
- Focused retest: start or reload a campaign with this assembly and let the
  stopped founder reach the next six-hour auction. The new `AUCTION` row will
  state the exact `logisticsReason`. If the reason remains `sell_mounts` or
  `sell_cargo` while the founder repeatedly holds at the same village and the
  relevant inventory value never falls, the next gameplay correction should
  make only genuinely serviceable settlement visits protected, rather than
  weakening food, recruitment, healing, or case priorities globally.


## 2026-07-20 temporary-party economy audit and serviceable settlement filter

- The two one-use party types retain their dedicated lifecycles. Player
  pickets (`gwp_patrol_*`) continue to approach/pursue only the player, escort
  after victory when applicable, return after settlement or resolution, and
  are destroyed on arrival. Interception support parties (`gwp_enf_delay_*`)
  continue to pursue the recorded fast offender, mark themselves returning
  after their target/battle/war reason ends, request the recorded return town,
  and are destroyed within three map units or immediately after entering a
  settlement. The final-auction desire layer carries these existing explicit
  intents; founder-only diagnostics merely stopped logging them and did not
  remove their intents.
- Local Bannerlord `v1.4.7` decompilation confirms both temporary types use
  `CustomPartyComponent`, not `WarPartyComponent`. `DefaultClanFinanceModel`
  charges the leader party and entries in `Clan.WarPartyComponents`, caravans,
  and garrisons; it does not enumerate these leaderless custom parties. Their
  troop wages therefore do not reduce Grey Warden clan gold. Their independent
  party purse is now also explicitly initialized to zero for clarity and to
  prevent it from being mistaken for part of the family economy.
- The food side was not previously correct. `CustomPartyComponent` initializes
  only the troop roster, while `DefaultMobilePartyFoodConsumptionModel`
  considers these active, leaderless, non-bandit custom parties food-consuming.
  At the same time `AiVisitSettlementBehavior` rejects a leaderless party of
  the non-kingdom/non-minor Grey Warden faction, and
  `PartiesBuyFoodCampaignBehavior` requires a non-null leader. Thus a spawned
  picket/support party had no initial food and no native way to buy any.
- Both temporary types now receive twenty days of grain once, immediately
  after their roster is filled, calculated from the native base consumption of
  one food unit per twenty men per day. The provision is created for that
  one-use party and never charged to clan gold; unused food disappears with the
  party. Session launch also repairs an old-save temporary party only when its
  inventory contains no food, so repeatedly loading does not refill partially
  consumed rations.
- The stopped-founder hypothesis was corrected using the native source rather
  than retained as speculation. `PartiesSellLootCampaignBehavior` sells lord
  loot only when `settlement.IsTown`; it never sells at a village. At a town it
  sells only the amount covered by the town's current gold and does not contain
  any explicit “wait here until treasury refresh” state. The previous Grey
  Warden filter detected a cargo-sale need globally but then protected every
  native `GoToSettlement` candidate, allowing a generic village score
  (`0.3840`) to defeat the slightly lower town score even though the village
  could not perform the sale. This—not an empty village treasury—explains the
  observed hold at `castle_village_ES4_2`.
- Protected settlement candidates are now service-specific: food may select a
  town or village; recruitment a town or village but not a castle; healing and
  prisoner delivery require a fortification; loot/mount sale requires a town;
  ship repair requires a port. If the native auction contains no settlement
  able to satisfy the matched need, the assigned case fallback is restored in
  that auction instead of letting an unrelated settlement suppress police
  work. Player READMEs were updated with the temporary-party exception and the
  village-sale stall fix.
- Validation: the final `Release --no-restore` build completed with `0` errors
  and `47` existing nullable/offline-NuGet warnings. ILSpy of the deployed DLL
  confirms the twenty-day temporary provision, session repair, and
  `IsSettlementAbleToServeLogistics` filter. All `18` XML files parse. Live
  comparison found `25` deployable files and `0` missing or hash mismatches.
  Client/editor DLLs are byte-identical, size `489984`, SHA-256
  `0F8BEDE950A5BC05E0DBE45C611D1BD70B976D6AE6A900105CF9D282B95B85CF`.
  Repository/live Chinese README hashes both equal
  `3085FFE735F891DA461B110C8BA21DD7DCAF176B38209FBC41056F6A7465414F`;
  English README hashes both equal
  `2FDD40926E66BA444DE284251643346143D17192CF1679DAC240588E44ADE826`.
  No commit, tag, archive, or release was created.
- Focused retest: reload the current save. The leader's next auction should either
  preserve a town-only `GoToSettlement` candidate for `sell_mounts`/
  `sell_cargo`, or resume `ApproachPoint` if no serviceable town candidate
  survives. A newly spawned or old zero-food temporary party should show zero
  independent gold and approximately twenty days of food; it should retain its
  player/offender pursuit and existing return-destroy sequence.


## 2026-07-20 native-auction boundary correction: case outranks patrol only

- User correction: the mod must not classify Bannerlord's food, recruitment,
  trade, healing, prisoner, repair, safety, or other native desires and then
  decide which settlements are acceptable. The Grey Warden addition is only a
  persistent case duty whose score is slightly above the native patrol desire.
  The service-specific settlement filter described above was therefore an
  overreach and is superseded by this section.
- `GreyWardenPartyDesireBehavior` no longer contains
  `HasNativeLogisticsNeed`, `IsSettlementAbleToServeLogistics`, a settlement
  allowlist, or any manual cargo/food/wound/prisoner/recruitment/ship threshold.
  It also no longer suppresses `PatrolAroundPoint`, `Hold`, `None`, or any other
  native candidate. The final native score list is copied for diagnostics but
  every original tuple and score remains untouched.
- For a party with a current case or temporary duty, the layer reads only the
  highest positive native `PatrolAroundPoint` score. The added duty score is
  the next representable `float` above that patrol score. If no positive patrol
  candidate exists, the duty receives the minimal fallback `0.03`. Thus there
  is no possible score between patrol and duty: any original candidate truly
  above patrol is at least tied with the added duty and wins because the native
  tuple occurs earlier and Bannerlord replaces its winner only on a strictly
  greater score. `Hold` and `None` are not specially suppressed.
- Duty candidates are always appended with `AddBehaviorScore`. Even if a native
  producer already created an identical behavior and target, the mod does not
  call `SetBehaviorScore` or mutate that native tuple. Remote approach still
  uses a point-patrol carrier which is translated to real `GoToPoint` only if
  the added case candidate wins; declared-war pursuit remains native
  `GoAroundParty`, preserving native flee and strength decisions.
- Diagnostics now state `nativeScoresPreserved=True`, round-trip-precision
  `patrolCeiling`, the
  dynamically calculated `dutyScore`, and `dutyAdded`. A direct invariant check
  is possible: every entry in `rawScores` must remain in `finalScores` with the
  same behavior, target, and score; the only extra entry is the one duty
  candidate. The former logistics classifications and idle-suppression fields
  are absent because the mod no longer makes those decisions.
- The temporary-party economy result remains valid and independent: leaderless
  custom pickets and interception support parties remain outside clan wage
  accounting, keep zero independent gold, receive one fixed twenty-day ration
  at creation (or once for an old zero-food save), and retain their dedicated
  pursue/return/destroy intents. This is a spawn/lifecycle rule, not a
  replacement for native lord desires.
- Player READMEs now describe the exact boundary: case duty is above patrol,
  but no original candidate is removed, reduced, overwritten, reclassified, or
  routed to a mod-selected settlement.
- Validation: the final `Release --no-restore` build completed with `0` errors
  and `47` existing nullable/offline-NuGet warnings. ILSpy of the deployed
  assembly confirms `GetPatrolCeiling`, next-representable-float duty scoring,
  and `AddBehaviorScore`; it contains
  no `SetBehaviorScore`, idle suppression, or manual logistics/settlement
  filter in this behavior. Deployed diagnostics contain
  `nativeScoresPreserved=True`, `patrolCeiling`, and `dutyScore`. All `18`
  repository XML files parse. Live comparison found `25` deployable files and
  `0` missing or hash mismatches. Client/editor DLLs are byte-identical, size
  `487936`, SHA-256
  `8842261D15D436F30E4584C17A1FD84453CE24730EC5AD798079283CCDD1AFF4`.
  Repository/live Chinese README hashes both equal
  `0223C7F2E0CACEE7D01F8B45806E5073FCE662F9AD79D90E4D399BBC5C1B5BE2`;
  English README hashes both equal
  `9EFF8C86923C586F9703D0C427107912A1F3D7972197140E4BABC3FCC8556DAD`.
  No commit, tag, archive, or release was created.
- Focused retest: reload the current save and let one founder with an assigned
  case reach the next auction. The new row should show
  `nativeScoresPreserved=True`, a `patrolCeiling` equal to the greatest raw
  patrol score, a `dutyScore` just above that value, and an added case tuple.
  Every raw tuple must still appear unchanged in `finalScores`. If another
  native desire has a higher score, accept its target and action as Bannerlord's
  own decision; when only ordinary patrol would win, the founder must instead
  resume the same case.


## 2026-07-20 暮光连续办案与兵员流失实测诊断

- 本次运行日志包含 `1564` 行，其中暮光（`gw_leader_5_party_1`）有
  `287` 行状态、动作和竞价记录。她在战役小时 `625207.05` 结清
  `lord_1_41` 案件，`625208.06` 即被分配新案件
  `CharacterObject_1592`。代码侧原因与日志一致：
  `PoliceResourceManager.IsReady` 当前无条件返回 `true`，空闲警察扫描时
  只检查其没有现案，因而没有结案冷却、恢复阶段或兵力准备条件。
- 暮光并未挨饿或因无钱无法补粮。观察段内粮食天数约为 `39.26` 至
  `68.34` 天，部队钱袋为 `5000` 至 `6914`，家族金库由 `46918`
  降至 `28906`。因此本轮兵员下降不能归因于断粮，也没有证据表明
  补粮欲望被案件错误删除。
- 原版进城欲望确实存在，但没有胜出。暮光的最高原版巡逻分约
  `2.55` 至 `2.64`，案件候选按当前规则取巡逻分的下一个浮点数；而
  她的最高 `GoToSettlement` 通常只有 `0.38` 至 `0.85`，全段最高
  仅 `1.0167`。即使已有 `15` 至 `19` 名俘虏、兵力比降至约
  `0.29`，原版仍把这些进城候选排在巡逻之下。因此当前实现严格兑现
  “案件略高于巡逻”，但日志推翻了“补兵、交俘等原版需求必然高于
  巡逻”的前提：案件继承了巡逻的高基准，也就一并压过了这些较低的
  原版维护欲望。
- 暮光并非在该段反复领取多个领主案。`CharacterObject_1592` 从
  `625208.06` 一直持续到日志末尾。沿途看似“马上又去打别人”的行为
  来自原版短期交战：日志记录她先后以 `EngageParty` 攻击
  `deserters_1`、`deserters_91930`、`looters_91957` 和
  `looters_1862`。`GreyWardenPartyDesireBehavior.IsAuthorizedAttackTarget`
  当前对任何 `target.IsBandit` 都直接放行，所以所有灰袍领主都会在办案
  途中攻击野怪，而不仅是规划中负责逃兵或劫匪的专职角色。
- 兵员变化也与交战而非饥饿相符：新案接手前后由 `73` 降至 `63`；
  追踪途中多次与逃兵、劫匪交战后逐步降至 `50`。在
  `625309` 对案件目标宣战并进入持续 `EngageParty` 后，兵力由 `50`
  降至 `25`，伤员由 `0` 增至 `24`，目标距离固定约 `0.50`；这是正在
  结算的地图战斗所造成的连续伤亡。日志结束时战斗尚未结算，因而尚未
  观察到战后下一轮原版竞价。
- 结论：问题不是“原版欲望没有接入”，而是两项现行规则共同造成：
  （1）空闲即刻接新案，没有任何恢复窗口；（2）案件按最高巡逻分定价，
  而原版的补兵、交俘和一般进城分并不保证高于巡逻。此外，全部灰袍均
  可被短期 AI 分流去打野怪，加速了非专职角色的损耗。
- 后续修正需要先由设计层确定边界。若仍坚持完全保留原版候选，最小
  方案应围绕“结案后的原版恢复窗口”和“按岗位限制途中野怪交战”处理，
  而不是伪造粮食或直接强制进城。若继续让案件严格继承最高巡逻分，则
  必须接受原版中低于巡逻的招兵、交俘和一般进城欲望不会中断案件。


## 2026-07-20 接案期间仅压低巡逻欲望与家族资金流向核验

- 用户最终确定的欲望边界是不安排结案恢复流程，也不由模组判断何时补粮、
  招兵、疗伤、交易或选择哪个聚落。灰袍没有案件时不得介入原版欲望；接到
  案件后只压低 `PatrolAroundPoint`，案件追踪仅略高于压低后的巡逻，其余
  原版候选和分数原样竞争。该设计取代上一节提出但未实施的“恢复窗口”。
- `GreyWardenPartyDesireBehavior.ProcessFinalDesires` 现在先保留完整原版竞价
  快照；存在有效案件或临时职责时，仅将高于 `0.03` 的原版
  `PatrolAroundPoint` 候选封顶为 `0.03`。这个值不是自定的后勤阈值，而是
  Bannerlord 1.4.7 `AiPartyThinkBehavior` 对巡逻/进城行为使用的最低执行
  阈值。案件候选取该巡逻上限的下一个可表示 `float`，因此能作为真正可执行
  的保底职责，同时任何高于最低门槛的补给、招兵、疗伤、交易、交俘、安全
  等原版欲望都会自然排在案件前面。没有职责时不改任何候选，也不添加案件
  候选。
- 诊断字段相应改为 `originalPatrolCeiling`、`suppressedPatrolCeiling`、
  `suppressedPatrolCount`、`dutyScore` 和
  `nativeNonPatrolScoresPreserved=True`。状态行另增 `dailyWage`、
  `wageLimit` 与 `unpaidWages`，下一轮测试可以直接核对六支常驻部队每日
  工资、原版动态工资上限和欠薪比例。
- 对 2026-07-20 08:34 会话日志的资金复核覆盖战役小时 `625200.94` 至
  `625327.62`（约 `5.28` 天）。族长资金即家族资金，由 `46918` 降至
  `28906`；六名创始人的可支配现金合计约由 `76425` 降至 `59654`，净少
  `16771`。这不是隐藏的模组扣款。
- 原版 `DefaultClanFinanceModel` 已给出精确流向：灰袍为 6 级、无封地、
  非王国的 AI 家族，每日只有 `6 × 80 = 480` 第纳尔无地家族补贴；族长
  部队工资直接由族长/家族资金支付。其余领主部队先从各自
  `PartyTradeGold`（对领主队伍就是领主个人金钱）扣工资，若扣完低于原版
  下限 `5000`，家族资金再补回 `5000`。因此日志里约珥等人长期停在
  `5000` 并不代表没花工资，而是家族每天替他们填平工资缺口。
- 六次原版每日结算与日志完全对上：家族金库净变化依次为 `-3359`、
  `-2468`、`-4229`、`-2356`、`-1986`、`-2098`；加回每日 `480`
  补贴，说明金库当日承担的“族长队工资＋其他领主补回 5000 的转账”为
  `3839`、`2948`、`4709`、`2836`、`2466`、`2578`。这些数不是六队
  完整工资，因为其他领主高于 5000 的部分会先由个人账户支付。把同一结算
  前后六人的现金合计起来、抵消家族内部转账后，六队当日总工资约为
  `6002`、`4096`、`4932`、`4770`、`3134`、`3883`。也就是说当前常态
  被动收入只有 `480/日`，而六队工资约 `3100～6000/日`，仅靠被动收入
  必然持续亏损。
- 日结算以外的资金变化来自队伍自己的原版行动。族长凡蒂的队伍钱袋就是
  家族金库，因此她招兵、买粮或交易会直接改变 `clanGold`；日志在
  `625250` 附近资金减少 `437` 的同时增员 8 人、`625256` 附近减少
  `1074` 的同时增员 7 人，符合原版招募/采购。其他领主先使用个人钱；
  圣铎曾随俘虏增长收入 `2084`，暮光收入 `1914` 和 `472`，晨曦收入
  `7053`，说明战斗战利品收入确实进入各自钱袋，但不足以稳定覆盖六队工资。
- 当前轮只修正欲望边界和补充工资诊断，不新增补钱、免薪或被动收入。经济
  结论是后续经济系统的设计依据：若不希望家族最终破产，需要独立增加可感知
  的稳定收入或调整常驻队伍开支，而不是误判为原版补给欲望没有生效。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线
  NuGet 警告。部署后共核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致
  为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小
  `488960`，SHA-256 为
  `FD34AE899A850E163F9A28EA61362619683EC372634680A4518F335ACD8BD8C6`。
  中英文 README 仓库/实机哈希分别一致，SHA-256 为
  `ECCFB00DE8B6FED8D5EBB0D63D2A0806DA8BF771D338EAC3AF9C0236E0963D41`
  与 `32C62FD9A7BE5E99EE621DF79CF5AE4354A3342EF94054F8527973F9A72B5D63`。
  ILSpy 对实机 DLL 的反编译确认存在仅限巡逻的 `SetBehaviorScore(...,
  0.03f)`、以 `Math.Max(0.03f, patrolCeiling)` 取下一浮点值，以及单独的
  `AddBehaviorScore` 案件候选。未创建提交、标签或发布包。
- 下一轮实测应在有案和无案的创始人之间对照：有案竞价应显示原始巡逻约
  `2～3`、压低后巡逻 `0.03`、案件为紧邻其上的浮点数；原版
  `GoToSettlement` 只要高于该值就先胜出。无案竞价的
  `suppressedPatrolCount` 必须为 `0`。同时使用新增的 `dailyWage`、
  `wageLimit`、`unpaidWages` 继续观察家族资金是否进入欠薪阶段。


## 2026-07-20 当前罚款去向与灰袍可用收入边界

- 代码核验确认，现有玩家罚款**不会进入灰袍领主个人钱袋或家族金库**。
  `PoliceResourceManager.CollectFine` 与 `CollectFineGoldOnly` 都只调用
  `Hero.MainHero.ChangeHeroGold(-goldTaken)`，没有对应的
  `GiveGoldAction`、灰袍领主 `ChangeHeroGold(+...)` 或
  `PartyTradeGold += ...`。因此金币在当前实现中被直接销毁。
- 直接向常驻灰袍领主认罚的路径在
  `PoliceEnforcementBehavior.Dialogue.OnEnforcementPayAcceptedConsequence`
  调用 `CollectFine`；谈判界面的 `GwpBribeBarterable.Apply` 是空实现，只
  用于表达接受条件，并不转账。战败押送后的领主罚款与临时纠察队罚款分别
  调用 `CollectFineGoldOnly`，结果同样只是扣除玩家金币。故“交给领主”和
  “交给巡逻队”当前没有经济去向上的区别。
- `CollectFine` 在金币不足时调用 `ConfiscateItems`，只从玩家
  `ItemRoster` 删除物品并把物品基础价值计入“已缴数”；物品没有加入承办
  领主、临时队或家族库存，也没有按估值给家族加钱，因而同样被销毁。
- 当前灰袍实际可获得的收入只有：（1）无地、非王国、6 级 AI 家族的原版
  `480/日` 基础补贴；（2）常驻领主打赢案件目标、劫匪或逃兵后获得的原版
  战利品、俘虏赎金及进城出售收入；（3）非族长领主个人钱袋超过 `10000`
  后，原版家族财务每日抽取“超出部分的十分之一”进入族长/家族金库。
  玩家悬赏奖励与村民声望奖励目前直接给玩家生成金币，也没有从灰袍金库
  扣款。
- 相比普通 NPC 家族，灰袍按既定限制不能依赖封地税收、村庄税、城镇关税、
  王国预算补助、统治家族收入、雇佣兵工资、贡金与战争协议收入，也没有已
  配置的工坊或商队；警察身份又排除了劫掠村庄、攻击村民/商队等掠夺收入。
  因此现有固定 `480/日` 与行动战利品无法稳定承担六至十五支常驻队伍。
- 尚未实施的推荐经济结构是统一使用“灰袍司法公库”（运行时可直接复用
  `Clan.Leader.Gold`，无需再造一套货币）：所有玩家罚金和罚没品拍卖价进入
  公库；成功结案获得商会、受害聚落或旧帝国治安契约的办案拨款；再由旧帝国
  治安公产/商路保护捐金提供稳定基础收入。若要求货币守恒，可从受保护城镇的
  `TradeTaxAccumulated` 提取极小比例，而不是凭空发固定人头工资；灰袍加入
  玩家国家后则可由玩家王国 `KingdomBudgetWallet` 承担国家警察拨款。该方案
  目前只是设计建议，未改代码、README、实机 DLL 或存档结构。


## 2026-07-20 司法公库罚款入账与结案归因核验

- 用户确认直接复用族长钱包作为“灰袍司法公库”，符合原版结构：
  `Clan.Gold` 本来就是 `Clan.Leader.Gold`，非族长领主的原版部队盈余也会
  逐步上交到这里。因此没有新增第二套金钱或存档字段。
- `PoliceResourceManager.CollectFine` 和 `CollectFineGoldOnly` 已改为通过
  `GiveGoldAction.ApplyBetweenCharacters` 把玩家实缴金币转入灰袍族长钱包。
  常驻领主与临时纠察队调用的是同一入口，临时队只代收、不持有罚款。金币
  不足时被 `ConfiscateItems` 移除的物品视为统一拍卖，其现有估值通过
  `CreditJudicialTreasury` 进入族长钱包。族长不可用的异常兜底仍只扣玩家，
  避免罚款流程因数据损坏而中断。
- 为未来“灰袍成功结案奖励 5000”核验现有结案路径时发现一个真实漏洞：
  `PoliceEnforcementBehavior.OnMapEventEnded` 过去只要求承办警察参加并赢得
  一场战斗；非玩家案件没有核对罪犯是否也参加，因此承办人途中打赢无关
  劫匪也可能错误结案。现已新增 `WasTaskOffenderInEvent`，按实时引用、案卷
  保存的罪犯部队 ID 和英雄 ID 三重核验参战方。只有罪犯与承办警察同场且
  承办警察在胜方，才属于常驻灰袍成功结案；承办警察失败则案件重新入池。
- 犯罪目标被完全无关的王国、领主或野怪击败时，承办警察不在该场战斗，
  不会进入上述成功分支；后续 `UpdateTasks` 发现目标失效后只清理案件，不应
  获得未来办案拨款。一次性追截支援队属于灰袍自身，其胜利由
  `DelayPatrolWonBattle` 与输家 ID 明确核验，未来可视为灰袍成功完成。当前
  只修复归因基础，尚未发放每案 `5000`。
- 关于案件保底分：`0.03` 不是任意接近零的值，而是 Bannerlord 1.4.7
  `AiPartyThinkBehavior` 对 `PatrolAroundPoint`/`GoToSettlement` 的最低执行
  阈值；降得更低会出现案件即使赢得竞价也难以真正换成行动。对 08:34 测试
  日志全部原版竞价重新统计，原版 `GoToSettlement` 的最低正分为 `0.0386`，
  共 `1918` 个候选中没有一个低于或等于 `0.03`；原版巡逻最低为 `1.5641`。
  因而该实测中案件 `0.030000...` 低于所有原版进城欲望，不会压过补给。
  但不能把一次存档的最小值当成全局定理，新诊断增加
  `minimumPositiveNonPatrolScore` 与 `nonPatrolAtOrBelowDutyCount`，可直接
  发现未来版本/场景是否出现低于案件的非巡逻欲望，而无需凭感觉改分。
- 村庄的 `Village.Hearth` 是“户数/人口与生产规模”，不是村民钱包。
  若按每户每日 `0.1` 计算后再从 `Hearth` 扣除同等数值，数学上等于每天
  消灭约 10% 的家庭，会迅速摧毁全大陆村庄。正确的守恒实现应当是用
  `Hearth × 0.1` 计算当日治安公产应缴额，但从每村原版已有的
  `Village.TradeTaxAccumulated`（村庄贸易税池）扣款并转入司法公库，且以
  税池现有余额为上限；`Hearth` 只作为计费基数，不应减少。该每日收入尚未
  实装，等待确认扣款载体后再加入，玩家统一后的国家拨款也未提前设计。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有警告。部署后
  `25` 个 `_Module` 运行文件全部一致，`18` 个 XML 全部可解析；客户端与
  编辑器 DLL 字节一致，大小 `490496`，SHA-256 为
  `65635ADE224EA99683E0C444DA78C839E735D0F025024B85BFAB4E2F8B1C1EAD`。
  中英文 README 仓库/实机 SHA-256 分别为
  `57FFD966AE47721F8E760CB2A28410D885F9C9A89C1597BE593E5740DE0EF0A3`
  和 `AB73151FFAA4367717D2B7D000263F306F6E954177308F5DD2B330EE66B01C6A`，
  均完全一致。ILSpy 对实机 DLL 确认金币转账、罚没品公库入账、罪犯参战
  核验以及两个新增欲望诊断字段均存在。未提交 Git。


## 2026-07-20 司法公库稳定收入、胜案拨款与失败删案

- 用户最终确认司法公库的三项当前期收入规则：（1）常驻灰袍领主确实击败
  自己承办案件的目标，公库增加 `5000`；（2）追截支援队确实位于胜方并
  击败仍在案件池中的目标，公库增加 `5000`；（3）全大陆每个村庄每日按
  当日 `Village.Hearth` 每户一第纳尔缴纳保护费。灰袍战败、外部势力或
  野怪代为击败目标、目标自然失效、无案可结等路径均不得获得胜案拨款。
- `PoliceResourceManager.SuccessfulCaseReward` 固定为 `5000`，所有拨款只经
  `CreditSuccessfulCaseCompletion` 进入族长钱包所代表的司法公库。没有把
  奖励放入通用 `CrimePool.EndTask`，因为该入口同时用于战败、失效、取消和
  行政调度。常驻领主拨款只在 `OnMapEventEnded` 已通过
  `WasTaskOffenderInEvent` 三重参战核验且承办灰袍位于胜方后调用；玩家案件
  在灰袍取得本场胜利并开始押送时只计发一次，不在押送结束时重复计发。
- 追截支援队仍先由 `DelayPatrolWonBattle` 确认支援队属于胜方，再逐一核对
  败方部队。`ResolveTrackedOffenderDefeatByDelayPatrol` 现在只有在确实结束
  一项活动任务或删除一项未分派开放案件时才将 `resolvedCase` 置为真，并且
  每名罪犯/单一开放案件只拨款一次；支援队打赢无案目标不会凭空领款。
- 普通 AI 领主案件的执法失败语义已按用户要求改为“失败即从当前案件池删除”。
  `PoliceEnforcementBehavior.UpdateTasks` 在常驻部队失活、首领被俘/死亡时
  直接 `EndTask`，不再 `Reassign`；`OnMapEventEnded` 在承办灰袍战败或该
  部队于目标参战事件中消失时同样只 `EndTask`；`CrimePool.Clean` 清理消失
  承办部队时也改为 `EndTask`。案件的时间、地点、罪名等当前记录被删除，
  但 `HeroCrimeStats.TotalCrimeCount` 长期数字不回退，罪犯日后再次犯罪仍会
  生成一宗新案。玩家通缉使用单独的长期追捕记录，仍维持既有的战败后继续
  通缉规则，不把一次警察战败解释为清除玩家全部通缉状态。
- `ReopenCase` 没有被全局删除：玩家案件挤占普通案件、同一警察被明确改派、
  村庄救济等行政移交仍需要保留旧案。其注释已经明确它不再是战败默认路径，
  防止以后误把所有 `EndTask` 都恢复入池。
- `PoliceResourceManager.OnDailyTick` 新增
  `CollectDailyVillageProtectionContributions`：逐个读取 `Village.All` 的
  当前 `Hearth`，按 `floor(Hearth)` 累计公库收入，然后把该村户数改为
  `max(10, Hearth × 0.99)`。因此恰好 `100` 户的村庄本日贡献 `100`，结算
  后变为 `99` 户；小数户数按现值衰减，收入只取完整户数。保底 `10` 与原版
  `Village.DailyTick` 的户数下限一致。没有排除被劫掠村庄，也没有动
  `TradeTaxAccumulated`，因为用户明确要求全地图村庄直接以户数缴费并承担
  `1%` 户数损耗。
- 用户提出“有案时让巡逻分等于全部原版欲望中的最低值，再让案件分略高于
  最低值”。该方案本轮没有实装，因为 Bannerlord 的欲望拍卖选择**最高分**，
  不是只检查案件是否高于最低分。例如原版候选为 `0.04 / 0.40 / 0.80`，
  案件即使取 `0.040001`，仍会永远输给 `0.80`；动态跟随最低值只改变了
  巡逻和案件在队尾的顺序，不能保证案件获得执行机会。下一步若要同时避免
  “永久不办案”和“办案压死补给”，应采用会随等待时间逐渐提高、并在案件
  实际胜出后重置的职责紧迫度，或明确划分原版维护行动与办案行动的时间窗；
  在用户确认前不擅自重排原版补给、招兵、交易、疗伤和逃跑欲望。
- 最终 Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/
  离线 NuGet 警告。自动部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希
  不一致为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，
  大小 `491008`，SHA-256 为
  `FD5FCF97B74544382BC6907B755B8A3FA3352CBF9C5E81FAD45C5491FC99341B`。
  中英文 README 仓库/实机哈希分别一致，SHA-256 为
  `71EEE85A11BC53B04A0A8E5594616F96AAF30BEAF1EB2A88BA8E257EE42D68FC`
  与 `9B1219306AE8FFA3F22A377C656B392874FAFCB3882188C7525729DB8B2D34CB`。
  ILSpy 对实机 DLL 确认存在 `SuccessfulCaseReward = 5000`、两条胜案拨款调用、
  `Village.All` 日结算及 `Hearth × 0.99`，并确认失败路径不再调用
  `Reassign`。`docs/grey-warden-setting.md` 的当前玩法章也已同步司法公库、
  原版欲望接入和失败删案现状。未创建 Git 提交。


## 2026-07-20 原版欲望分布复核与案件基准分建议

- 重新统计文档目录下 `GreyWarden-AI-Diagnostics.log` 的 2026-07-20 08:34
  会话，共有 `112` 次六名
  创始人原版欲望竞价。该日志产生于“案件继承最高巡逻分”的旧实现，不能
  直接代表当前 `0.03` 案件分的胜负结果，但其 `rawScores` 是未修改的原版
  候选，适合确定原版分数尺度。
- 原版 `PatrolAroundPoint` 共 `2157` 个候选，单项范围 `1.5641～3.09`；
  每次竞价的最高巡逻分中位数约 `2.5576`。这证明巡逻与后勤不在同一常用
  分数带，案件继承巡逻分必然长期压过普通进城需求；现行“有案只把巡逻压到
  `0.03`”的方向是正确的。
- 原版 `GoToSettlement` 共 `1918` 个候选，单项范围 `0.0386～19.5966`；
  每次竞价的最高进城分范围 `0.3505～19.5966`，中位数 `0.5376`，90 分位
  约 `2.0441`。全部 `112/112` 次竞价都至少有一个进城候选高于 `0.03`，
  因此当前案件若永久固定在紧邻 `0.03` 的分数，按最高分拍卖确实可能一次
  都无法胜出；让案件只略高于“最低原版欲望”同样无效。
- 日志显示原版进城分具有可用的自然分层：状态正常时最高进城分多在
  `0.35～0.85`；需要交付大量俘虏、恢复低编制或处理一般维护时会接近
  `1.0～2.0`；真正紧急的缺粮和大量伤员恢复会跃升到 `3.7～19.6`。
  具体例子包括梵蒂/约珥在只剩约 `4～5` 天粮时达到 `10.99～19.60`，弥瑟
  有 `24～40` 名伤员时达到 `3.72～13.12`。这些高分原版需求应继续无条件
  压过办案。
- 建议下一版先采用最小、可解释的固定案件基准，而不是立刻增加复杂状态机：
  有案时仍只把巡逻压到 `0.03`；案件候选取 `1.0` 的下一个可表示浮点值；
  其他全部原版候选不改。按本次日志回放，最高进城分大于 `1.0` 的竞价为
  `27/112`，这些时刻由原版维护行动胜出；其余 `85/112` 次由案件压过普通
  低分进城访问并获得执行机会。这个比例正好实现“原版需求强时先维护，原版
  需求低时去办案”，且不需要识别某个 `GoToSettlement` 究竟是在买粮、招兵、
  卖货、交俘还是疗伤。
- 初版不建议同时加入随时间无限增长的案件分。固定 `1.0` 已能利用原版分数
  自己判断维护强度；若后续实测仍出现某支部队因长期维持 `1.0～1.2` 而永不
  出警，再增加“连续等待若干小时后缓慢升至 `1.5`、案件实际胜出后归零”的
  有上限老化机制。先固定基准再观察，可以避免一次同时引入两个变量，也不会
  让案件最终上涨到压过 `3.7～19.6` 的饥荒和重伤恢复需求。


## 2026-07-20 案件固定中间权重实装

- 用户确认采用固定分界，但要求案件分“比一稍微低一点”。
  `GreyWardenPartyDesireBehavior` 新增 `AssignedDutyScore = 0.99f`；只要存在
  有效案件或临时警务职责，远距定点接近、宣战后追击、真实护送与职责性进城
  都统一以 `0.99` 加入最终原版欲望拍卖。旧调用传入的 `priority` 参数继续为
  兼容签名而保留，但不允许不同调用方重新放大职责分数。
- 有职责时，原版 `PatrolAroundPoint` 仍只被封顶到 `0.03`；除巡逻外的
  `rawScores` 不删除、不改分。低于 `0.99` 的普通定居点访问会输给案件；
  高于 `0.99` 的补给、招兵、交易、疗伤、交俘、修船及其他原版维护候选仍
  正常胜出。无职责时既不压巡逻，也不添加 `0.99` 候选。
- 已删除按压低后巡逻分取“下一个可表示浮点值”的 `GetDutyScore` 与
  `NextRepresentableFloat`。诊断日志继续输出 `originalPatrolCeiling`、
  `suppressedPatrolCeiling`、`dutyScore`、原版最低非巡逻分以及低于案件分的
  原版候选数量；新实测中有案部队应稳定显示 `dutyScore=0.99`，而不是
  `0.030000...` 或继承原巡逻的 `2～3`。
- 最终 Release 构建通过，`0` 错误；增量构建仅保留 `1` 个离线 NuGet
  漏洞元数据警告。自动部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希
  不一致为 `0`；`18` 个 XML 全部可解析。客户端与编辑器实机 DLL 字节一致，
  大小 `490496`，SHA-256 为
  `A8322799DFF6E1B0605CD643EFD2DAA2D03A7A2D2B5FB89B2099A2CB4193651B`。
  中文 README 仓库/实机 SHA-256 均为
  `AEBF00036CACA87CE8842B38F95C1F0C42027DD5E041F53910AB42AD29588E60`；
  英文 README 仓库/实机 SHA-256 均为
  `CCB9E98F82556EE70D4286A42D5F15F2355A353FAF779133D0E651AF6BCC7C2B`。
- ILSpy 对实机 DLL 的反编译确认存在 `AssignedDutyScore = 0.99f`、
  `AssignedPatrolScoreCeiling = 0.03f`、最终欲望竞价中的固定职责分和巡逻
  封顶，并确认旧 `GetDutyScore` / `NextRepresentableFloat` 已不存在。未创建
  Git 提交、标签或发布包。


## 2026-07-20 固定案件分实测：司法公库暴涨与圣铎驻村招募

- 最新诊断会话覆盖战役小时 `625327.84～625556.36`，约 `9.52` 个战役日。
  灰袍家族资金从 `28906` 增至 `882193`，净增 `853287`。日志中出现 `9`
  次约十万规模的日结算跳涨，观测增量合计 `886043`、平均约 `98449/日`；
  同期只有两次可明确识别的 `+5000` 胜案拨款。由此可确认暴富并非办案收入，
  而是当前 `floor(Village.Hearth)` 全大陆逐村日缴规则把全地图约十万户的
  户数几乎一比一转成了每日金币。六支常驻队最新工资合计约 `6070/日`，即使
  再计招兵、买粮和家族内部补款，也远低于约十万的保护费，因此公库必然快速
  变成巨富。每日跳涨由约 `105283` 逐步降至约 `95649`，也与每村同时执行
  `Hearth × 0.99` 的人口衰减相符。当前轮只完成诊断，尚未擅自更改用户此前
  确认的每户一第纳尔与每日衰减规则。
- 用户所称“圣泽”按日志对应创始人 `圣铎`。他从战役小时约 `625448` 起停在
  `castle_village_K8_1`（埃泽努尔）的精确地图坐标 `(719.985, 262.71)`；
  `CurrentSettlement` 虽显示为空、默认行为显示 `Hold`，但原版
  `MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation` 会把
  位于村庄图标上的队伍当作当前在村庄，原版 `RecruitmentCampaignBehavior`
  因而仍会每小时调用 `CheckRecruiting`。这不是灰袍冻结或无法进入聚落。
- 圣铎确实一直在招兵：驻留该村期间兵力由约 `78` 增至 `93`，最近状态为
  `93/约175`（`sizeRatio=0.531`）；个人钱袋随招募多次由 `5000` 下降到
  `4830/4660/4490/4320/4116`，并由原版家族财务在日结算时补回最低周转金。
  `foodDays=19.16`、`dailyWage=753`、`wageLimit=10000`、`unpaidWages=0`，
  家族资金又超过八十八万，所以饥饿、现金、工资预算和欠薪都没有禁止招募。
- 他没有一次拿走玩家界面中可见的全部兵，是原版 AI 招募的明确限制。原版
  `RecruitVolunteersFromNotable` 每小时对每名要人至多招一人，还要先通过随机
  起始槽位和随当前编制比例变化的随机判定。`DefaultVolunteerModel` 又按与
  要人的关系、聚落阵营和买方身份限制可用槽位：灰袍与普通异阵营村庄要人
  在零关系下通常只能使用前两个槽位，而玩家可能因关系、同阵营或难度奖励
  看到更多可招槽位。因此界面中后排仍有许多兵，不代表圣铎可以直接招走；
  他会消耗前两格的可用兵，然后在村庄等待这些格子按日刷新。
- 欲望日志也证明他是在主动选择补兵而非案件失效：驻村期间埃泽努尔的原版
  `GoToSettlement` 分长期约为 `1.78～3.31`，持续高于固定案件分 `0.99`，
  所以原版补员需求获胜并让他留在村庄；与此同时其兵力仍缓慢上升。这个结果
  正是当前“案件只压过普通巡逻和弱访问、不压过高分原版维护”的规则。若不
  希望领主为了补到高编制而在单一村庄停留多日，后续应单独决定是否接受原版
  慢速招募，不能把它误判为案件欲望没有落实。
- 本轮没有修改运行代码、README 或实机模组，也没有重新构建、部署或创建
  Git 提交；只增加了上述诊断结论与原版反编译依据。


## 2026-07-20 村庄保护费降档与灰袍—要人恒定满关系

- 用户根据上一轮实测确认将保护费从每户每日 `1` 第纳尔降为 `0.1`，但保留
  每村每日 `Hearth × 0.99`、最低 `10` 户的既有损耗规则。
  `PoliceResourceManager.CollectDailyVillageProtectionContributions` 现先以
  `double` 汇总全大陆 `Hearth × 0.1`，最后统一向下取整入司法公库；没有逐村
  向下取整，避免大量小数尾款因村庄数量被反复吞掉。按 10:05 诊断会话约
  十万总户数估算，新日收入应由约十万降到约一万。已有存档中已经进入公库的
  钱不倒扣，下一次日结算起才使用新费率。
- 新增 `GreyWardenNotableRelationsBehavior` 并注册为战役行为。读档、会话启动、
  每日结算会遍历灰袍家族全部存活成员与 `Settlement.All` 的全部存活要人，把
  基础关系设为 `100`；新要人生成、新灰袍成员生成和灰袍后继者成年时也会立即
  补齐。该状态不新增独立存档字段，直接使用原版 `CharacterRelationManager`
  的双英雄关系数据，所以旧档无需迁移结构。
- 为落实用户要求的“强制拉满、不会变化”，仅靠每日回填不够。新增两个窄范围
  Harmony 前缀：（1）任何 `CharacterRelationManager.SetHeroRelation` 写入若
  配对恰好是一名灰袍家族成员与一名 `Hero.IsNotable`，写入值强制改为 `100`；
  （2）`ChangeRelationAction.ApplyInternal` 遇到同一受保护配对时直接确认满值
  并截断动作，防止实际关系没下降却仍广播负关系通知。其他英雄关系、灰袍与
  普通领主关系、玩家与要人关系均不受影响。
- 原版 `DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero` 已复核：关系
  `100` 提供最高关系档，最终槽位数封顶为 `6`；即使灰袍与聚落不同阵营，乃至
  临时处于战争关系，关系加成在计算中仍足以抵消异阵营惩罚并保持六格。因而
  圣铎一类灰袍 AI 不会再因零关系只能使用前两格而长时间等待它们刷新。原版
  每小时招募随机判定、现金门槛、工资预算、编制上限及每名要人每次至多招一人
  等其余规则没有修改。
- 最终 Release `--no-restore` 增量构建通过，`0` 错误，仅有 `1` 个离线 NuGet
  漏洞元数据警告。部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致
  为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小
  `493568`，SHA-256 为
  `5CDBB31CA637C5FAE4BFA19FAEC71C8A34D262DB85356FC4D8756D399BEF0650`。
  中文 README 仓库/实机 SHA-256 均为
  `BE6D1030C1CE3B837C2549A2B5B61CE048DDAB81C51E6D6ABE468364BA7ACDE1`；
  英文 README 仓库/实机 SHA-256 均为
  `F68AF5827337DA2160F415FA840AFB1E58063C3D5901CBADC957BBF9243AC905`。
- ILSpy 对实机 DLL 确认保护费使用 `Hearth × 0.1`、人口仍使用
  `Hearth × 0.99`，新关系行为已经注册，并存在关系写入与关系动作两个受保护
  前缀。中英文玩家日志与 `docs/grey-warden-setting.md` 已同步。未创建 Git
  提交、标签或发布包。


## 2026-07-20 无英雄临时执法队关闭欲望并单次直攻

- 用户明确要求无英雄临时部队不要持续接收移动命令，也不要像领主一样参与
  欲望思考。范围只包括无 `LeaderHero` 的临时纠察队与追截支援队，且只在
  已锁定敌对目标的 `Pursue` 阶段生效；常驻灰袍领主的固定 `0.99` 案件分、
  原版实力判断及全部非巡逻欲望均不改。
- `SetDirectAttackIntent` 现在首次锁定目标时只调用一次
  `SetMoveEngageParty`，随后设置 `SetDoNotMakeNewDecisions(true)`。任务每小时
  刷新时若目标未变，只延长运行时意图有效期，不再重复写入移动命令。
- `AiPartyThinkBehavior.PartyHourlyAiTick` 前缀在该直攻锁存在期间直接跳过整个
  小时思考回合，因此原版和模组评分器都不会为这支临时队生成巡逻、补给、
  逃避、实力衡量或其他候选，也不会让空竞价的后续解析覆盖首次攻击命令。
  小时维护只检查目标是否仍有效，不再重发攻击命令。
- 目标失效、任务清理或支援队转入返程时，`ReleaseDirectAttackLock` 会解除
  `DoNotMakeNewDecisions` 并允许下一小时重新思考；因此返回驻地和销毁流程仍
  可使用既有的访问意图，不会被战斗阶段的锁永久冻结。
- 最终 Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线
  NuGet 警告。自动部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致
  为 `0`；`18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小
  `495104`，SHA-256 为
  `9AE957FF2E92984AB21F38DF0EF6760819D6012814046524A064FCA073880E08`。
  中文 README 仓库/实机 SHA-256 均为
  `E52B6AEDFAFD172E530E6B0A267FF32BE3B449868CAE7F3208EC410ABF5CEAF0`；
  英文 README 仓库/实机 SHA-256 均为
  `17BA5D8DF6F45940C5D1434D9D45815B6DA4E609F7DA393E4065B54666308A61`。
  实机 DLL 字符串核验存在 `HasDirectAttackLock` 与 `StartDirectAttack`，且旧的
  `ForceDirectAttack` 已不存在。未创建 Git 提交、标签或发布包。


## 2026-07-20 无英雄临时队接触后立即和平的 Git 对比诊断

- 用户实测新直攻锁后，追截支援队会冲上去接触目标，但没有持续完成交战，
  随即散开、和平并撤退。本轮按要求只对比和诊断，没有修改运行代码、README
  或实机模组，也没有重新构建部署。
- Git `6bd3871`（后续一直保留到当前基线 `a51f9d9`）中的无领主追截支援队
  完整战斗配置不是单独的 `SetMoveEngageParty`，而是连续执行：
  `SetDoNotMakeNewDecisions(true)`、`SetInitiative(1f, 0f, 999f)`、
  `SetMoveEngageParty(target)`。当前直攻层只恢复了冻结与 `EngageParty`，漏掉
  `SetInitiative`。因此当前并没有完整复原用户所说的旧版良好行为。
- Bannerlord 1.4.7 实机 `MobilePartyAi` 反编译确认，`SetInitiative` 三个参数
  分别写入 `_attackInitiative`、`_avoidInitiative` 与恢复时间。原版
  `DefaultMobilePartyAIModel` 的进攻评分直接乘 `AttackInitiative`，逃避评分与
  逃避距离直接使用 `AvoidInitiative`。旧值 `1f, 0f, 999f` 的含义正是保持正常
  进攻主动性、把逃避主动性降为零并长期维持；它不是欲望分，也不会恢复领主式
  补给、巡逻或战略思考。
- 第二个放大问题是既有战后收束仍然过宽：
  `HandleDelayPatrolBattleEnded` 只要看到任何追截支援队参与某次已结束的
  `MapEvent`，就无条件 `MarkDelayPatrolReturning`；随后
  `TryResolveDelayPatrolWarTargetImmediately` 在当前案由已经被战斗结算清掉时
  立刻 `SetNeutral` 并把同目标的支援队全部标记返程。该逻辑在旧 Git 中已经
  存在，但旧版的零逃避主动性使队伍更稳定地把接触推进到明确胜负；当前漏掉
  主动性配置后，一次短促或非决定性接触也会被这条无条件收束链解释成任务结束，
  于是玩家看到“撞一下、散开、立即和平”。
- 结论：当前症状不是欲望重新开启。最直接的回归是直攻层漏掉旧版
  `SetInitiative(1f, 0f, 999f)`；和平与撤退则由既有的“任何支援队战斗结束均
  返程”和“无剩余案由立即和平”继续完成。下一轮修复应先完整恢复旧版三件套，
  同时把支援队返程/和平限制为案件目标已经真正战败或任务确实失效，而不是仅凭
  任意一次 `MapEventEnded`。


## 2026-07-20 无英雄临时队持续完成战斗任务

- 按用户确认的最终 AI 边界实现：常驻灰袍领主仍只在有任务时把普通
  `PatrolAroundPoint` 封顶到 `0.03`，并添加固定 `0.99` 案件候选；其他全部
  原版欲望、分数、聚落选择和战力判断不改。无领主临时纠察/追截支援队则没有
  战略欲望和自主强弱判断，只执行明确任务。
- `StartDirectAttack` 恢复 Git `6bd3871` 的完整战术配置：先
  `SetInitiative(1f, 0f, 999f)`，再 `SetMoveEngageParty`，最后冻结新决策。
  这保持正常进攻主动性、把逃避主动性降为零，且不生成巡逻、补给或逃跑欲望。
- `HandleDelayPatrolBattleEnded` 不再见到任意 `MapEventEnded` 就无条件返程。
  现在只有目标部队失活或 `NumberOfHealthyMembers <= 0` 才视为真正战败；目标
  仍有可战人员时保留直攻锁，并只登记一次战后续攻。续攻会在地图事件完全清除
  后补发一次 `EngageParty`，不是每小时持续重发命令。
- 支援队胜利后的案件清理与 `5000` 办案拨款同样增加“目标真正战败”门槛；
  敌方撤退、脱离或非决定性交锋不会删案、发款或触发和平。常驻领主胜利结案
  也使用同一可战人员门槛，防止目标撤退却被错误认定为已抓获。
- `PoliceAntiWarDeclaration` 在战后恢复和平前新增现有合法战争理由核验。只要
  同一阵营仍有活动案件、悬赏或玩家纠察理由，就维持战争；最后一项理由真正
  消失后，原有自动和平流程才会执行。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线 NuGet
  警告。部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致为 `0`；
  `18` 个 XML 全部可解析。客户端与编辑器 DLL 字节一致，大小 `496640`，
  SHA-256 为
  `6BFAD5823F18300C47FA08B5C663CA38049D44C5BC1003E61C41A49BA6D1A661`。
  中文 README 仓库/实机 SHA-256 均为
  `5A71E2D5FE85010F390EC4F1B953240B7B5ADD41F18FD562DEF8F06844A42E49`；
  英文 README 仓库/实机 SHA-256 均为
  `349EC09F359DCA4078015FF7F92A4DAE5A00DCB23914056A7B6071854F5B57F9`。
- ILSpy 对实机 DLL 确认直攻入口存在 `SetInitiative(1f, 0f, 999f)`、
  `DirectAttackRefreshPending` 与一次性战后续攻；案件和支援队胜利结算均检查
  `NumberOfHealthyMembers <= 0`；战后和平前存在 `HasLegitimateWarReason`
  守卫。源码同时复核领主侧仍只有 `AssignedDutyScore = 0.99f` 与
  `AssignedPatrolScoreCeiling = 0.03f` 两项欲望干预。未创建 Git 提交、标签
  或发布包。


## 2026-07-20 最新 AI 脚本输出与两类部队执行链复核

- 最新 `GreyWarden-AI-Diagnostics.log` 截止本地时间 `11:55:36`，大小约
  `8.6 MB`。日志中 `gwp_enf_delay_*`、`gwp_patrol_*`、`leader=-` 与
  `DIRECT_ATTACK_STATE` 均为 `0` 条，但这不能证明无领主队伍没有生成：
  `GwpAiDiagnostics.ShouldTraceFounderParty` 明确只允许六名创始英雄 ID 写入，
  所有无 `LeaderHero` 的临时队都会被日志过滤。现有脚本只能直接验证创始领主，
  不能观察临时纠察队或追截支援队。
- 玩家声望 `-1～-10` 时，每日检查若当前没有纠察队且不在贿赂保护期，会从
  玩家最近城镇生成一支 `gwp_patrol_*`。它无 `LeaderHero`，规模为声望绝对值
  乘当前 `PatrolSize=10`。敌对前用 `Approach` 接近玩家并触发谈判；玩家拒绝且
  宣战后才进入无欲望直攻锁。声望降至 `-11` 以下则撤回临时纠察队，改由正式
  灰袍领主接管通缉。
- 追截支援队不是每小时或每案立即生成。每两日检查一次已经进入
  `WarPursuit`、非玩家目标且战争仍有效的案件；按敌对阵营收集所有仍被追踪的
  罪犯部队，每个尚无活动支援队的罪犯生成一支 `gwp_enf_delay_*`。生成点优先
  取承办灰袍附近城镇，否则取目标附近城镇；固定 `50` 人，约六成重步兵、四成
  弓手，无 `LeaderHero`，保存来源案件、目标部队、敌对阵营与返程城镇后立即
  进入无欲望 `EngageParty`。
- 无领主队的边界按阶段不同：追截支援队的敌对执行阶段完全跳过小时思考，
  使用 `SetInitiative(1,0,999)`、单次 `EngageParty` 和决策冻结；非决定性战斗
  后只补发一次续攻。临时纠察队在尚未宣战、需要和平接触玩家时仍用 `Approach`
  欲望；两类队伍任务结束返程时当前仍用 `Visit` 职责候选，而不是直攻锁。
- 正式领主案件在 `WarDeclared=false` 时解析为 `Approach`：每次原版竞价以目标
  当时坐标生成一个地点候选，案件候选胜出后转成真实 `GoToPoint`。它不是永远
  固定的城镇或一次性旧坐标；后续竞价会读取目标的新位置。非玩家罪犯距离低于
  `WarDistance=3` 时，小时任务更新调用 `DeclareWar`，案件阶段切为 `Pursue`。
- 宣战后案件候选是目标部队上的 `GoAroundParty=0.99`，普通巡逻仍压到 `0.03`，
  其他原版候选完全保留。它的含义是持续锁定、跟随和拦截目标，不是强制
  `EngageParty`。原版短期主动性继续根据局部战力、士气、附近敌军和导航条件
  选择真正接战或 `FleeToPoint`。最新日志多次显示 `war=True`、长期行为
  `GoAroundParty`、短期行为 `FleeToPoint`，正好证明案件仍在而原版强弱判断
  也仍然生效。
- 晨曦的日志从 `war=False`、目标距离约 `2.37` 进入后续 `war=True`，符合小时
  检查在三格内宣战；日志快照发生在移动之后，所以下一条显示的距离可重新超过
  `3`。约珥在宣战后目标一度远至八十多格，仍保持 `GoAroundParty` 和相同案件，
  说明宣战后的目标引用不会因距离拉大而丢失。玩家目标和躲入定居点的罪犯另有
  对话/门口守候分支，不完全走上述非玩家野外自动宣战流程。


## 2026-07-20 AI 诊断扩展至全部无领主灰袍部队

- 用户要求现有脚本除六名创始领主外，也必须能观测所有灰袍无领主 AI。
  `GwpAiDiagnostics` 的过滤器由 `ShouldTraceFounderParty` 扩展为
  `ShouldTraceParty`：继续记录六名创始英雄，并记录所有 `LeaderHero == null`
  且属于灰袍家族/阵营、临时纠察队前缀或追截支援队前缀的活动部队。其他王国、
  野怪和非创始灰袍领主不会因此混入日志。
- 每行前缀新增 `partyKind`：`founder_lord`、`leaderless_picket`、
  `leaderless_delay_support` 或 `leaderless_grey_warden`。状态字段新增
  `dutyIntent` 与 `directAttackLock`；无领主队没有 `PoliceTask` 时，`offender`
  和 `offenderDistance` 会回退读取职责层的运行时目标，因此能直接看到临时队
  正在接近、直攻或返程的对象。
- `Watch-GreyWardenAI.ps1` 的说明已同步，并新增可选 `-Kind` 过滤。例如
  `-Kind leaderless_delay_support` 只观察追截支援队，`-Kind leaderless_picket`
  只观察临时纠察队；原有 `-Party`、`-Once` 和 `-Tail` 继续可用并可组合。
- Release `--no-restore` 构建通过，`0` 错误、`47` 个既有 nullable/离线 NuGet
  警告。部署后核对 `25` 个 `_Module` 运行文件，缺失或哈希不一致为 `0`；
  PowerShell 对更新后的观察脚本解析成功。客户端与编辑器 DLL 字节一致，大小
  `497664`，SHA-256 为
  `2B4F6D20057D7158E91FBD90FBBD78AEA229A4B59E666470AB59111BFA59E67B`。
  ILSpy 对实机 DLL 确认 `ShouldTraceParty`、全部三种无领主 `partyKind`、
  `dutyIntent` 和 `directAttackLock` 均已部署。未创建 Git 提交、标签或发布包。


## 2026-07-20 双领主协办思路的原版战力核验

- 用户提出：承办领主宣战后若原版判断打不过，可临时调最近另一名灰袍领主
  前来共同进攻。该方向与原版 AI 机制兼容，但不能简单给两支队各自一个相同
  追击目标；若双方相距太远，它们会各自只用自身附近战力判断，并可能同时
  `FleeToPoint`。
- Bannerlord 1.4.7 实机 `DefaultMobilePartyAIModel.GetBestInitiativeBehavior`
  反编译确认，短期主动性先在约 `3 × GetEncounterJoiningRadius` 范围扫描相关
  部队，然后把同阵营、可参与同场事件的附近友军战力加入局部优势计算。因而
  第二名灰袍真正靠近主办领主后，原版确实会重新用合并后的附近战力比较敌方，
  并可能自然从逃避切换为 `EngageParty`；若目标是更大的军团，两人合力仍不足，
  原版继续不打也是正确结果。
- 建议实现为独立的“主办—协办”关系，不修改 `CrimePool` 的一案一主办约束。
  主办案件、结案归因和办案拨款保持唯一；协办领主通过临时外部职责以现有
  `0.99` 分跟随主办领主，原任务保留但暂停。进入原版共同参战范围后无需模组
  强制开战，由原版局部战力重算决定；主办案件结束、目标失效或协办失效时清除
  临时职责，协办自动恢复原任务。
- 触发不能只看一次 `FleeToPoint`，因为最新日志显示短期逃避目标有时是案件目标
  附近的另一支敌军。较稳妥的门槛应为：主办已经宣战、长期目标仍是案犯、处于
  接敌区域并连续若干小时由原版选择逃避；每案最多一名协办，避免六名领主全部
  被一个军团吸走或形成循环支援。协办选择应排除被俘、战斗中、押送玩家、悬赏
  护送和村庄救济中的部队，并优先最近的可用灰袍领主。
- 本轮只完成可行性与原版依据核验，没有实现双领主协办状态、没有改变玩家
  README，也没有为此再次构建部署。无领主诊断扩展是本轮唯一已部署的改动。


## 2026-07-20 六领主逐级协力任务与完整任务池

- 已实现案件绑定的领主协力机制。只观察六名创始领主的非玩家正式案件；必须
  同时满足案卷仍开放、承办关系仍指向当前领主、案件已进入 `WarPursuit`、目标
  部队仍有效且有健康兵员、双方仍实际交战、长期行为仍为该目标上的
  `GoAroundParty`，并在十二格内连续三个小时由原版选择逃避，才生成协力任务。
  单纯与同一国家交战、另一个案件尚未结束或旧目标残留均不能触发。
- 协力任务生成即强制分配给距主办人最近的可用创始领主，不进入普通等待领取
  流程。候选排除被俘、战斗中、玩家押送、悬赏护送、玩家案件和村庄救济；已经
  是协力主办人或协办成员的领主也不可被其他小队抢走。同一小时的竞争按稳定的
  创始部队 ID 顺序处理，先成功建立的关系立即占用双方，避免 A/B 循环求援。
- 协办人原有普通案件调用 `EndTask` 后立即 `ReopenCase`，完整退回公共案件池；
  不保存或恢复旧任务。协力结束后只解除 `EscortParty=0.99` 外部职责，该领主在
  同一小时后续正常调度中重新领取当时距离最近的未分配案件。
- 每个主办案件可形成一个多成员小队。已有成员必须全部进入主办人五格内，才会
  重新累计主办人的连续逃避；若集合后仍连续三个小时逃避，再逐次增加一名最近
  可用领主。成员只跟随主办人，不各自远程追击案犯；进入原版共同参战范围后，
  原版继续决定是否按合并附近战力接战。案件、目标、主办人或战争失效时整组
  解散，成员回到普通调度。
- 协力状态与每名成员的分配时间已加入存档。AI 诊断每行新增 `assistance`：可见
  `independent:blocked`、`leader:members/blocked/target` 或
  `member:leader/distance/target`；增援加入和释放另写
  `ASSISTANCE_ADDED/JOINED/RELEASED/DISBANDED` 动作行。协办人也被授权把来源
  案件目标作为合法短期攻击对象，但没有新增强制接战命令。
- 案件总卷改为完整任务池视图。普通开放案件、已分配普通任务和每名协办人的
  强制协力任务共同占用最多一百条容量并全部显示；所有已分配任务置顶，再列
  未分配案件，两组内部均按时间从新到旧。容量溢出时只移除最旧且无人承办的
  普通案件，不移除玩家通缉、已分配案件或协力任务。摘要显示总数、已分配数和
  等待数。
- 首次实现构建曾因案件总卷缺少 `System.Collections.Generic` 及
  `CampaignTime.Hours` 的 `double/float` 参数差异出现三个编译错误；补充引用并
  显式转为 `float` 后构建恢复通过。这是实现过程中的已解决错误，不是运行时
  缺陷。
- 最终 Release `--no-restore` 构建通过，零错误；新增协力存档代码的可空警告已
  收敛，剩余均为既有项目警告及离线 NuGet 漏洞源警告。构建自动同步 `_Module`
  后复核二十五个可部署源文件，实机目录缺失或哈希不一致均为零，仓库与实机
  `README.md` 哈希一致。客户端和编辑器 DLL 字节一致，大小 `514048`，SHA-256
  为 `46CA1EBF9B6C319B6175D7BB6325EF25D0502CCB0D0BBAE5CE30CE9073B4C3C8`。
  中文语言 XML、案件总卷 prefab XML 和观察脚本均通过解析；未创建 Git 提交、
  标签或发布包。


## 2026-07-20 完整任务池触发无领主支援队爆量的修复

- 用户实测发现地图上出现大量无领主灰袍支援队。读取最新
  `GreyWarden-AI-Diagnostics.log` 后确认，本次会话累计生成过 `77` 支
  `leaderless_delay_support`；生成呈严格的两日批次，最近七批分别一次生成
  `16、8、10、12、9、7、15` 支。最近一批在战役小时 `626720.68～626721.30`
  之间生成十五支，分别追击十五个不同目标，因此不是同一目标的重复状态日志，
  而是真实批量生成。
- 根因位于 `CheckPersistentWarTargetsEveryTwoDays`：它先从任意
  `WarPursuit` 任务得到敌对势力，再调用 `GetTrackedOffendersByFaction` 把该势力
  所有开放案件目标交给 `SpawnDelayPatrolsForOffenders`。旧案件池很小时问题不
  明显；案件总卷与池容量扩展到一百后，大量无人承办案件仍属于同一敌对势力，
  因而被另一宗已宣战案件错误带入生成范围。日志中最近批次的十五支各自对应
  不同 `CharacterObject_*` 或 `lord_*`，与该调用链完全一致。
- 生成口径已改为具体承办任务。`GetEligibleDelaySupportTasks` 只接受当前仍在
  `CrimeState.ActiveTasks`、案卷开放、目标有效且有健康兵员、非玩家、阶段为
  `WarPursuit`、战争仍有效的任务；每个任务只把自己的 `TargetCrime.Offender`
  交给生成器。无人承办的任务池案件不会再因同国另一宗案件宣战而生成支援。
  每个具体目标仍通过 `_delayPatrolStates` 保证最多一支活动支援队。
- `UpdateDelayPatrols` 每小时用相同资格集合复核现有支援队。旧版本批量生成、但
  已不对应一宗当前有效承办案件的队伍会标记 `Returning`，下一步
  `RequestVisit` 自动解除直攻锁并返城解散；因此旧存档无需重新开档或手工清队。
- 修复后同时活动的追截支援队数量只可能等于当前符合条件的正式领主
  `WarPursuit` 目标数。六名创始领主各自承办一案时理论上最多六支；正在执行
  协力任务的成员没有普通承办案件，因此实际通常低于六支。支援队战败而对应
  正式案件仍有效时，下一次两日检查仍可按原设计补发一支。
- 最终增量 Release 构建通过，零错误；输出只报告离线 NuGet 漏洞源警告。自动
  部署后复核二十五个可部署文件，实机缺失或哈希不一致均为零。客户端与编辑器
  DLL 均为 `514048` 字节，SHA-256 为
  `C4A6BF8E48EF5E92D7DB12B6A22E43F8D9494BE962E039C8E944785FB8309AC8`。


## 2026-07-20 无王国原版军团协力

- 用户否定了 `EscortParty=0.99` 的“多支队伍靠近后各自判断”方案，要求协办人
  真正加入主办人的原版军团；同时明确灰袍不会建国、领主数量不能限制为六名，
  未来收养成长的全部灰袍领主都必须纳入。
- Bannerlord 1.4.7 反编译确认 `Army(Kingdom, MobileParty, ArmyTypes)` 的
  `Kingdom` setter 对 `null` 安全，构造函数会把军团长写入 `LeaderParty.Army`；
  `member.Army = army` 会调用原版 `OnAddPartyInternal`，同家族的
  `CalculatePartyInfluenceCost` 明确返回零；原版 `AiArmyMemberBehavior` 随后为
  未合并成员生成高分 `EscortParty`，接触后 `Army.Tick` 或
  `AddPartyToMergedParties` 通过 `AttachedTo` 完成真实附着和同场参战。因此实现
  使用 `new Army(null, leader, ArmyTypes.Patrolling)`，没有创建隐藏王国，也没有
  自制军团对象。
- `PoliceEnforcementBehavior.Assistance` 已改为识别灰袍家族下所有有效
  `IsLordParty`，不再检查 `gw_leader_0..5`。触发条件仍是合法案件、有效目标、
  `WarPursuit`、实际战争、十二格内连续三小时由原版选择逃避。首次触发创建原版
  无王国军团，最近可用灰袍领主将旧案退回任务池后通过 `MobileParty.Army` 加入；
  到达原版军团接触距离后通过 `AddPartyToMergedParties` 附着。全员附着后若军团长
  再连续三小时逃避，则继续加入下一名最近可用领主，没有成员上限，直到没有
  可用领主。
- 军团长仍通过既有案件 `GoAroundParty=0.99` 参加最终欲望拍卖，其余原版补给、
  招兵、疗伤、交俘和安全需求不改；协办人不再获得模组自定义跟随欲望，只依赖
  原版军团成员行为。若一次原版进城行动曾为 Army 写入聚落目标，案件追击重新
  胜出时会清除该过期 `AiBehaviorObject`，避免军团每小时逻辑把短暂停留的军团长
  再拉回旧集合点。
- 案件目标真正被击败或主办方战败时立即
  `DisbandArmyAction.ApplyByObjectiveFinished`，其余案件/目标/战争失效由下一个
  小时清理执行相同原版解散；成员解除 `Army/AttachedTo` 后进入普通调度，按当时
  距离领取最近任务，不恢复被协力打断的旧任务。保存数据继续只记录主办、案件、
  目标、成员和分配时间；读档后若 Army 对象未随对象图恢复，会按这些 ID 重建
  原版无王国 Army 并重新加入成员。
- 原版 `Army.HourlyTick` 的 `CheckArmyDispersion` 会随机按“战争中是否存在有封地
  势力”解散军团；独立家族追捕无封地目标天然不满足。因此只对当前仍绑定合法
  协力组的无王国 Army 窄拦截 `NoActiveWar`。最初实现曾同时拦截
  `FoodProblem/CohesionDepleted/Inactivity/UnknownReason`，复核用户要求后确认范围
  过宽并已撤销：超过半数部队断粮、长期停滞、军团长失效以及原版 AI 主动取消
  均重新按原版解散。案件主动结束仍走原版 `ObjectiveFinished`。
- 原版同家族加入只保证影响力成本为零，并不会自动停止凝聚力变化；默认模型仍
  有基础日衰减和成员数量衰减。当前合法灰袍协力 Army 的
  `CalculateDailyCohesionChange` 结果被窄改为零，使同家族执法军团凝聚力保持现值，
  不需要花影响力维护；非协力军团的原版凝聚力公式完全不变。
- 收紧后的最终 Release `--no-restore` 构建通过，零错误，仅有离线 NuGet 漏洞源
  警告。自动部署后复核二十五个可部署文件，缺失与哈希不一致均为零，仓库与
  实机 README 一致。客户端和编辑器 DLL 字节一致，大小 `519168`，SHA-256 为
  `B80A43BB4F85FF2F090B76E8F1E9479E734042FF6A51183D3F78D20672525113`。


## 2026-07-20 三百余人军团仍不接战的日志诊断

- 最新 `GreyWarden-AI-Diagnostics.log` 显示弥瑟承办 `lord_1_5` 时先后召集暮光与
  约珥。军团建立早期 `armyMemberCount=3` 只表示三支部队已加入 Army，并不表示
  三支都已合并：弥瑟约一百五十余人、暮光一百二十余人已经 `AttachedTo`，约珥
  一百余人仍在三十至四十格外集合，因此当时短期判断实际可立即参战的是约二百
  八十人，而不是界面合计的三百八十余人。
- 约珥后来也成功附着；战役小时约 `626850～626860` 的军团状态为
  `members=2,attached=2`，三支合计约三百八十人。此时案件欲望没有被补给或其他
  欲望击败：最近可见的一次主办人拍卖中，模组加入的
  `GoAroundParty@lord_1_5=0.99` 排第一，最高原版进城候选仅约 `0.22`。问题不在
  长期欲望权重。
- 原版 `MobilePartyAi.GetGoAroundPartyBehavior` 的语义不是强制接战。只要它能在
  目标周围找到一个防守/环绕位置，就把长期 `GoAroundParty` 落实成短期
  `GoToPoint`；只有找不到该位置时才直接变成 `EngageParty`。全员附着后的日志
  正是 `default=GoAroundParty, short=GoToPoint, shortTarget=lord_1_5`，军团停在目标
  约 `5.95` 格处长时间不动。
- 原版短期主动性确实把已附着军团的 `Army.EstimatedStrength` 用作本方基础战力，
  因而不是“军团成员完全没有计入”。但它还会把扫描半径内同阵营敌军加入敌方
  局部战力，并用攻击/逃避主动性、距离、速度等共同决定是否用 `EngageParty`
  覆盖长期行为。军团形成前，弥瑟与已附着的暮光一直在逃避附近另一支敌军
  `lord_1_20`，而不是案件目标 `lord_1_5`，说明该区域存在额外敌方战力。全员
  附着后逃跑停止，却仍没有产生足以覆盖环绕行为的进攻分数。
- 当前协力升级条件只把近距离 `FleeToPoint` 认作“仍然打不了”。全员附着后短期
  行为变为 `GoToPoint`，`BlockedHours` 因此保持零，机制错误地认为阻塞已经解除，
  不会继续召集第四名领主。这是“三百余人停着但不继续加人”的直接逻辑缺口。
- 本轮仅完成日志与原版代码诊断，没有修改协力触发或追击行为，也没有重新构建
  部署。后续修正应至少把“全员附着、案件目标仍合法、近距离持续
  `GoAroundParty + GoToPoint/Hold` 且没有进入战斗”计为持续阻塞；若所有可用领主
  都已加入后仍保持该状态，再决定是否把最终阶段切换为一次原版
  `EngageParty`，以符合“全员集结后发动大战”的目标。
- 反编译还发现玩家与任何 Army 相遇时的 `army_encounter` 背景代码直接读取
  `Army.Kingdom.Culture`。已只对当前灰袍无王国执法军团使用原版
  `wait_fallback` 背景，避免玩家点击相遇菜单时空引用；普通王国军团菜单不改。
- 诊断范围从六名创始人扩展为所有灰袍领主及所有无领主灰袍部队。每行新增
  `armyLeader/armyMemberCount/attachedTo/armyKingdom`，协力摘要改为
  `armyLeader` 或 `armyMember` 并显示附着数量；动作日志使用
  `ASSISTANCE_ARMY_*`。案件总卷把任务类型显示为“强制军团协力”。
- 首轮与运行时保护增量 Release 构建均通过，零错误；最终构建仍只有既有可空
  警告和离线 NuGet 漏洞源警告。构建自动同步后复核二十五个可部署源文件，实机
  缺失与哈希不一致均为零；仓库和实机 `README.md` SHA-256 同为
  `0E5735F909F84F273E0719B04399EBF60F7E0CB1104D77DAA9F37375056F74ED`。
  客户端与编辑器 DLL 字节一致，大小 `519680`，SHA-256 为
  `576C165D2F1604E286759391AA2E706361F9A780A0AD89F313B36F56DC464312`；中文
  语言 XML 与案件总卷 prefab XML 均解析通过。未创建 Git 提交、标签或发布包。


## 2026-07-20 单承办人新档测试模式与攻城诊断增强

- 为隔离普通执法经济与调度过程，当前测试版把普通 AI 犯罪案件和玩家通缉案件
  都限定为灰袍家族当前族长的部队领取。其余灰袍领主不再从普通案件池接案，但
  仍是原版军团协力和村庄收养善后的合法强制候选；村庄善后候选主动排除族长，
  避免唯一普通承办人被收养任务占用。该规则没有限制后续成长领主的数量。
- 案件总卷的数据源补入村庄善后任务。等待分配的善后列在等待区；已分配任务与
  普通案件、协力任务一同置顶，并显示补给、赶路或驻村阶段及驻村剩余时间。
  普通案件、协力成员任务和收养善后共同占用一百条任务池/显示上限，新增强制
  任务时会优先裁掉最早且无人承办的普通案件。
- 为查明三百余人军团未加入攻城战的问题，AI 每行状态新增普通案件资格、任务
  流程、己方地图事件/攻城营地，以及罪犯的 active、健康兵员、战争状态、原版
  Army 军团长、AttachedTo、攻城营地领队、地图事件和双方是否处于同一事件。
  地图事件开始/结束还会输出事件类型、攻守方领队、聚落和全部参战部队 ID。
- 协力清理不再只留 `case_or_party_invalid` 这个笼统结果；解散前新增
  `ASSISTANCE_VALIDATION_FAILED`，逐项记录 task 是否存在、承办人是否匹配、
  WarDeclared、FlowState、案件是否开放、罪犯是否 active/有健康兵员、MapFaction、
  实际战争关系、案件 ID 是否匹配，以及目标当时的军团/攻城/地图事件状态。这样
  可直接判断攻城结算瞬间究竟是哪一项暂时失效。
- 对旧日志的进一步核对确认，`gw_leader_2_party_1` 的三支军团并非原版自行因
  粮食、凝聚力或无战争解散，而是模组小时清理在战役小时 `626860.52` 主动以
  `case_or_party_invalid` 解散。下一小时案件仍为 `lord_1_5`、`war=True` 且目标可
  解析，证明不是正常结案；高度疑似攻城/战斗结算切换中罪犯的 active、健康兵员、
  MapFaction 或战争关系短暂失效。旧日志没有逐项字段，需由本轮增强日志在新档
  复现后定案。
- 原版 `AiEngagePartyBehavior` 还明确跳过所有“在 Army 中但不是军团长”的敌方
  部队。因此若案件罪犯是攻城军团附属成员，长期 `GoAroundParty` 虽能跟到其
  周围，普通主动进攻扫描却不会把这个罪犯对象当成合法 `EngageParty` 目标；本轮
  新增的 `offenderArmyLeader/offenderAttachedTo/offenderSiegeLeader` 正是为了验证
  该高度可疑路径。本轮没有提前改写攻击对象或强制参战，先保留可验证证据。
- 最终 Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。中文语言 XML 与案件总卷 prefab XML 均解析通过；自动部署后复核二十五个
  可部署源文件，实机缺失或哈希不一致均为零。客户端与编辑器 DLL 均为
  `530944` 字节，SHA-256 为
  `72B3F795E3CE71F8E4640645B422908AE3C158F6274D6D546BE776BCCDA398D4`；仓库与
  实机中文 README SHA-256 同为
  `1C6397837B0CAF5B9398BEE51D93530F4997E14B9A6CFDEE892618FD2391F4D8`。


## 2026-07-20 玩家案件与普通案件路径复核

- 单承办人规则已经覆盖玩家通缉的专门分配入口：声望达到重度通缉条件时，
  `EnsureNearestPoliceForWantedPlayer` 不再选择最近的任意灰袍领主，而只返回当前
  灰袍家族族长部队。因此玩家案件会由族长收到，其他领主仍只参与协力和收养。
- 玩家案件进入 `PoliceTask` 后，与普通案件共用 `GreyWardenPartyDesireBehavior`：
  宣战前是 `Approach` 定点候选，宣战后是 `Pursue/GoAroundParty`，固定任务分数和
  仅压制普通巡逻的规则相同；非巡逻的原版补给、招兵、疗伤、安全等欲望评分
  没有被删除，宣战后也继续由原版短期 AI 判断是否接战。
- 但当前玩家流程并非完全等同普通案件。专门分配入口会调用
  `PoliceResourceManager.CancelResupply` 并可通过 `TryAssignPlayerCrimeToPolice` 挤掉
  族长已有的普通案件；普通案件只在承办人 `IsReady` 且无任务时领取。玩家接触
  后不会自动宣战，而是先进入缴款/赎罪/拒捕对话，只有玩家选择拒捕才宣战。
- 玩家目标还被明确排除在无领主追截支援与领主军团协力之外：
  `GetEligibleDelaySupportTasks` 跳过 `offender.IsMainParty`，
  `GetValidAssistanceOffender` 也拒绝玩家目标。因此现状满足“不强制 AI 直接打
  玩家”，但若后续目标是把玩家案当作真正同级普通案件，还需单独决定是否取消
  分配时的补给中断/强制顶案，并明确玩家是否永远不获得协力支援。
- 本轮只复核并记录现状，没有修改玩家案件行为、构建或部署。


## 2026-07-20 恢复协力军团原版攻击欲望与玩家协力

- 原版 `AiEngagePartyBehavior.AiHourlyTick` 对“本队是 Army 军团长且 ArmyType 不是
  Defender”的部队直接 return。灰袍执法军团必须保持 `Patrolling` 才不会改变
  原版军团任务语义，因此没有把 Army 永久改成 Defender；新增 Harmony 窄补丁只
  在这一个原版攻击欲望评分器执行期间临时暴露为 Defender，并在 postfix/finalizer
  中立即恢复原类型。最终加入拍卖的敌军候选、战力比、速度、距离和分数仍全部由
  原版 `AiEngagePartyBehavior` 计算，模组没有写固定攻击分数或移动命令。
- `GoAroundParty=0.99` 仍是案件长期追踪候选。执法军团现在能同时获得原版主动
  攻击欲望：军团战力足够时原版攻击候选可胜出，不足时继续包围/跟踪；原版补给、
  招兵、疗伤和安全欲望不变。
- 原版主动攻击扫描跳过敌方 Army 的非军团长成员。协力攻击授权因此把案件罪犯
  的 `BesiegerCamp.LeaderParty`、`Army.LeaderParty` 或 `AttachedTo` 解析为同案的
  合法战斗入口；案件归属和结案核验仍保存原罪犯，不会把敌方军团长改成罪犯。
- 玩家案件保留“强制顶掉族长现有普通案件”的优先级，但删除玩家案分配/续期时
  的 `CancelResupply`。宣战前仍以 `Approach=0.99` 接近并进入缴款/赎罪/拒捕对话；
  玩家拒捕宣战后与普通案件相同使用 `GoAroundParty=0.99`，原版后勤和短期强弱
  判断继续介入。
- `GetValidAssistanceOffender` 删除玩家目标排除。玩家拒捕且正式进入
  `WarPursuit` 后，族长在近距离连续由原版逃避时可建立同一套无王国原版军团并
  逐步召集其他灰袍领主。无领主追截支援仍排除玩家，避免对玩家使用“无欲望、
  直接攻击”的临时队伍逻辑；玩家协力只有保留原版判断的领主参与。
- 最终 Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。ILSpy 反编译实机 DLL 确认 `AiEngagePartyBehavior.AiHourlyTick` 前缀只对
  当前合法协力军团暂时写入 `ArmyTypes.Defender`，postfix/finalizer 均恢复保存的
  原 ArmyType。自动部署后复核二十五个可部署源文件，实机缺失或哈希不一致均为
  零；客户端与编辑器 DLL 均为 `531968` 字节，SHA-256 为
  `A28BC3799E8433BF53FA7DFD81BA8B83D9CB641CF808C4B041C594ADE2285C06`。仓库与实机
  中文 README SHA-256 同为
  `2BFF217D592E1E606E800C59F6285F18F0FB20CB1DC2688FED90F4B4F8A78B16`。


## 2026-07-20 新档实测：原版接战与连续无领主支援

- 用户实测观察到灰袍军团在认为战力足够后参与战斗。日志对应案件为族长梵蒂
  承办的 `lord_1_52`，处于 `WarPursuit`，长期案件候选始终保持
  `GoAroundParty@lord_1_52=0.99`；最高可见原版进城欲望约 `0.38`，未覆盖案件。
- 战役小时 `624745.56`，梵蒂建立无王国原版军团并召集约珥：
  `armyMemberCount=2`、`members=1`。但直到案件结束始终是 `attached=0`，约珥仍在
  远方赶来，因此最终战斗没有约珥参战。原版 Army 总人数显示与实际立即可参战
  队伍需要继续区分；下一次完整集结测试应确认 `attached=1` 后是否由军团自身
  发起战斗。
- 同一案件先后只存在一支活跃无领主支援，而不是并发批量生成：
  `gwp_enf_delay_73116` 于 `624729.20` 出发并在 `624746.74` 战败；
  `gwp_enf_delay_86856` 于 `624777.19` 出发并在 `624799.83` 战败；案件和目标仍
  有效后，`gwp_enf_delay_63725` 于 `624825.21` 再次出发。该序列符合“每案一支，
  被消灭后经过下一轮两日检查再补发”的设计，没有发现同一时刻超额刷队。
- 三支临时队在任务有效期间全部保持 `directAttackLock=True`、
  `default=EngageParty`、`short=EngageParty`，没有产生战略欲望或中途改向。前两支
  被包含多支帝国领主的敌方集团击败；第三支在 `624839.62` 左右触发最终
  FieldBattle，梵蒂随后从 `GoAroundParty + FleeToPoint` 切到
  `GoAroundParty + EngageParty` 并加入同一地图事件。
- 最终地图事件参战方明确为
  `gwp_enf_delay_63725, gw_leader_0_party_1, lord_1_52_party_1`，结果为
  `DefenderVictory`。案件目标健康兵员在战斗中逐步降至零；梵蒂从约一百九十名
  健康兵下降到一百六十五名并产生二十四名伤员，临时支援队剩三十名健康、
  十六名伤员，证明两支灰袍确实共同参加并完成战斗，而不是只在地图旁观。
- 战斗结束后案件被正常结案，下一次协力清理记录
  `taskExists=False/caseOpen=False`，随后释放约珥并解散军团。这次
  `ASSISTANCE_VALIDATION_FAILED` 是任务已成功结束后的预期清理，不是此前攻城
  切换中“案件仍在却瞬时无效”的异常。支援队则解除直攻并转为前往城镇收尾。
- 本轮只读取并归档实测证据，没有修改代码、构建或部署。结论是当前“持续锁案、
  无领主支援逐次消耗、附近领主加入最终战斗、胜利后结案解散”的闭环已经成立；
  尚待独立验证的是所有协力领主完成 AttachedTo 后，完整军团在没有无领主支援
  先开战的情况下能否自行产生原版攻击并发起战斗。


## 2026-07-20 外交和平回退与全领主恢复接案

- 确认存在真实软卡死路径：`PoliceTask.WarDeclared` 是独立保存字段，原版外交或
  其他系统恢复和平时不会自动清零。欲望层仍会按该字段产生
  `Pursue/GoAroundParty=0.99`，但原版攻击欲望不会攻击中立目标；协力和无领主
  支援又要求实际战争，因此案件可能永久停在目标周围。
- 按用户要求只增加小时一致性检查，没有添加 MakePeace 事件监听。每小时在更新
  支援与协力前检查所有 `WarDeclared=true` 且具有正式 `WarTarget` 的案件；若灰袍
  与罪犯当前实际势力已非战争状态，则把 `WarDeclared=false`、`WarTarget=null`，
  标记该案无领主支援返程，解散该案协力军团，并要求承办领主下一次重新拍卖。
  野怪案件没有正式 WarTarget，不参与该检查，仍保留其原有直接敌对语义。
- 回退不删除案件。随后同一个小时的既有 `UpdateTasks` 自然接管：距离外恢复
  `Approach=0.99`；普通罪犯仍在宣战距离内时可按原规则重新宣战；玩家案件重新
  接近并走缴款、赎罪或拒捕对话。日志新增 `CASE_WAR_STATE_RESET`，记录旧战争
  目标、罪犯当前势力和回退原因。
- 删除单承办人测试限制。`AssignTasks` 再次遍历灰袍家族全部有效领主，排除已有
  案件、协力、收养善后、失效首领和资源未就绪者后，为每人领取距离最近的无人
  承办案件。玩家重度通缉也恢复为从全部可用领主中选择离玩家最近的一支，并仍
  保留强制顶掉该领主普通案件的玩家优先级。
- 村庄收养善后候选不再排除族长；所有有效灰袍领主与未来成长领主重新处于同一
  普通案件、协力和收养调度体系。诊断字段 `ordinaryCaseEligible` 现在对全部符合
  条件的灰袍领主为 true，而不再只标记族长。
- 最终 Release `--no-restore` 构建通过，零错误；增量构建只报告离线 NuGet 漏洞
  数据源警告。中文语言 XML 与案件总卷 prefab XML 均解析通过；自动部署后复核
  二十五个可部署源文件，实机缺失或哈希不一致均为零。客户端与编辑器 DLL 均为
  `532480` 字节，SHA-256 为
  `4C1DF820D92E535A2A807D232D9BA3521593616E32615A2E96AEA9D9E97B7FDE`；仓库与实机
  中文 README SHA-256 同为
  `D61B4B3EBC07F46C4282447F62B19DFF56BB7CF18A60D484421602AF0DC6F81B`。


## 2026-07-20 协力任务改为完全绑定主办案件生命周期

- 明确拆分“协力创建资格”和“协力成立后的存续资格”。创建前仍要求主办领主处于
  合法战时追捕、目标可战、实际外交为战争，并连续出现原版近距离避战；这些条件
  只负责证明此刻需要新增协办人，不再重复用于判断已接下的协力任务是否有效。
- 已成立协力组现在只核验三项稳定身份：主办人的 `PoliceTask` 仍存在、任务仍归属
  同一主办部队、`TargetCrimeId` 仍等于协力组保存的 `CrimeId`。暂时和平、
  `WarDeclared=false`、流程退回接近、罪犯短暂失活、健康兵员瞬时归零、势力变化或
  地图事件结算均不会再强制释放协办人或解散军团。
- `UpdateLordAssistance` 先维护已经存在的协力组，再按严格条件解析当前可追捕目标。
  目标暂时不可用于追捕时只清零本轮受阻计时并保留原版 Army；案件重新宣战并回到
  `WarPursuit` 后，既有成员直接继续随主办人行动。只有尚未建立协力组的领主才走
  原有严格触发判断。
- 小时外交一致性检查仍会把不一致案件的 `WarDeclared` 与 `WarTarget` 清零，并让
  无领主追截支援返程，但不再调用 `ReleaseAssistanceGroup`。因此同一案件可以在
  原版外交恢复和平后保留领主军团，等待现有案件流程重新宣战或重新完成玩家执法
  对话。
- 原版 Army 因其自身规则暂时消失时，不再把“Army 当前不可用”解释为协力任务
  失败；只要主办案件仍是同一案件，系统会保留协力记录，并在后续小时重新恢复该
  无王国原版军团。真正 `EndTask`、任务换案或任务归属变化仍会输出
  `ASSISTANCE_CASE_ENDED`，随后释放全部协办人并解散属于该组的军团。
- Release `--no-restore` 最终构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。十八个仓库 XML 全部解析通过；自动部署后核对二十五个可部署文件，实机
  缺失与哈希不一致均为零。客户端与编辑器 DLL 字节一致，大小 `533504`，
  SHA-256 为
  `73A8D5975EA3B93C8492A6C8BF0A6CD1DE53FFAF89BF2A1B443123F47F1FABD5`。
  仓库与实机中文 README SHA-256 均为
  `A022BFB323354A3869FC63EAB791936EDA395E3E27CDC657F10A6B023CFEDDEE`；英文
  README SHA-256 均为
  `FF5BAF7D70D3F1FA4C2405A87A5EDAA4B9620D198173BADFBD3ACC3D49689BBB`。


## 2026-07-20 梵蒂在攻城结束后的城外对峙诊断

- 本轮只读取 `GreyWarden-AI-Diagnostics.log`，没有修改运行代码。梵蒂承办
  `lord_5_17`，案件一直保持 `WarPursuit/war=True`，长期候选为
  `GoAroundParty@lord_5_17_party_1=0.99`。目标在攻城期间属于
  `lord_5_20_party_1` 军团并附着于军团长；梵蒂的短期行为长期是以敌方军团长或
  邻近成员为目标的 `FleeToPoint/FleeToParty`，与玩家看到的城外折返一致。
- 梵蒂在战役小时 `624868.54` 召集约珥，在 `624947.50` 召集晨曦。第二次召集
  发生在攻城仍未结束时，而不是城池攻下之后。攻城于 `624962.52` 以进攻方胜利
  结束；之后案件罪犯的 `armyLeader/attachedTo` 从 `lord_5_20_party_1` 变为 `-`，
  证明敌方原军团已经解散。
- 最新状态约 `625080.62`：梵蒂的协力摘要为
  `members=2,attached=1,blocked=0`，Army 内显示三支部队；约珥已真正附着于梵蒂，
  晨曦仍距军团长约 `51.78`，正在以 `EscortParty` 赶来。因此地图上看见的增援是
  已经下达的第二个协力任务仍在执行，并非当前每小时继续新增协办人。
- 梵蒂当前仍在目标约 `8.8～9.1` 距离外反复于 `FleeToPoint`、`GoToPoint` 和短暂
  `None` 之间切换。最新欲望拍卖只有案件的 `GoAroundParty=0.99` 与若干原版进城
  候选，没有生成 `EngageParty` 候选。这证明卡点不是案件欲望被其他欲望覆盖，而是
  原版当前没有给城内/受聚落保护的目标生成可执行的野战攻击；灰袍逻辑本身也没有
  攻城任务，因此只能继续在城外跟踪。
- 现有诊断没有记录罪犯的 `CurrentSettlement`，也不记录每支敌方领主自己的欲望
  拍卖，所以不能仅凭日志严格证明“所有敌方领主均因灰袍更强而拒绝出城”。但攻城
  胜利、敌军团解散、目标仍在同一聚落区域、灰袍无攻击候选且持续折返的组合，与
  玩家观察的双方城内外互相威慑完全一致。后续若要精确区分，应为罪犯状态增加
  `currentSettlement`，并记录目标聚落内敌方领主数、健康兵员合计和驻军/民兵战力。
- 当前升级机制还有一层自然闸门：只有所有已召协办人均真正附着后，梵蒂再次满足
  近距离 `GoAroundParty + Flee` 才会累计 `BlockedHours`。目前晨曦未集合，所以
  `blocked=0`；并且现有 `IsCasePursuitBlocked` 在罪犯处于 `CurrentSettlement` 时
  直接返回 false。若罪犯确实仍在城内，即使晨曦抵达，当前代码也不会继续召集第三
  名协办人；只有罪犯离城后再次形成近距离连续避战，才会继续升级。
- 继续反编译 Bannerlord `v1.4.7` 的 `DefaultMobilePartyAIModel` 后，已确认城内领主
  可以导致城外军团长逃跑。原版局部主动性会以军团长及其 `AttachedParties` 的
  `Army.EstimatedStrength` 作为灰袍本方战力，并把扫描半径内的其他敌对领主部队
  合并进敌方局部战力；敌方领主处于 `CurrentSettlement` 并不会从避战计算中排除。
  因此梵蒂追捕的罪犯虽然本人只有八十余名健康兵，但同城其他领主仍可共同抬高
  `FleeToPoint` 分数。驻军本身被普通领主主动性扫描排除，日志所见逃避更可能来自
  城内多支领主部队，而不是单独把驻军与民兵全部直接相加。
- 原版同时明确把处于定居点且未参加地图事件的目标 `attackScore` 设为零，
  `AiEngagePartyBehavior` 也跳过位于要塞中的敌军。因此即使灰袍军团已经明显占优，
  它也不会隔着城墙生成野战 `EngageParty`；占优的直接效果首先是让避战分数下降，
  军团长回到案件的 `GoAroundParty`，通常落实为靠近目标周边的 `GoToPoint`。
- `Army.EstimatedStrength` 只累加军团长和已经进入 `LeaderParty.AttachedParties` 的
  成员。仅显示在 `Army.Parties`、仍在路上的晨曦不计入梵蒂当前短期强弱判断；她
  真正到达并 `AttachedTo=gw_leader_0_party_1` 后才会使本方局部战力立即增加。若增加
  后超过城内敌方领主的合计局部战力，梵蒂应停止逃跑并靠近，但这不是按人数必然
  触发：兵种战力、伤员、附近城外敌军、距离和原版进攻/避战主动性都会参与结果。
- 灰袍已有城内目标保底流程。案件目标仍在城内时，主办人若能靠到城门三格内并
  连续一小时移动不超过 `0.35`，`HandleShelteredCriminal` 会通过原版
  `LeaveSettlementAction` 令罪犯离城；若目标仍属于同城军团则先赶军团长出城。
  目标离城后才重新允许原版野战攻击判断。当前僵局的关键因此不是缺少后续流程，
  而是梵蒂的原版避战尚未允许她进入这段城门触发距离。


## 2026-07-20 无领主追截支援改为协力军团增援

- 按用户要求改变同案已有领主协力军团后的支援用途。没有合法协力军团时，
  无领主追截支援仍沿用既有“关闭其他欲望、零逃避主动性、一次直接攻击”路径；
  同案一旦存在有效 `LordAssistanceGroup`，新生成及地图上已有的对应支援队都会解除
  直攻锁，改为 `EscortParty` 前往主办领主并加入同一个原版 `Army`，不再逐队撞向
  强敌送死。
- 支援绑定优先使用保存的 `SourceTaskPolicePartyId` 查找主办协力组；旧存档状态若
  缺少该 ID，则按同一 `TargetPartyId` 回退匹配。只有协力组仍绑定主办人的同一
  `TargetCrimeId`、军团长仍为有效灰袍领主时才允许加入，避免把支援吸入其他案件
  的军团。
- 支援设置 `MobileParty.Army` 后使用原版 Army 集合和附着流程；到达原版军团接触
  距离后调用既有 `AddPartyToMergedParties`。因此仅在路上的五十人支援不计入
  `Army.EstimatedStrength`，真正 `AttachedTo` 军团长以后才作为同一军团的可参战
  战力参与原版进攻/逃避判断。
- Bannerlord 的 `Army.OnAddPartyInternal` 会对每个加入者调用
  `CalculatePartyInfluenceCost`，而无领主支援没有 `LeaderHero`，直接走原版公式会
  空引用。新增窄 Harmony 前缀只在“无英雄追截支援正在加入当前合法灰袍协力军团”
  时返回零影响力费用；普通领主、其他无英雄部队和所有非灰袍协力军团继续使用
  原版公式。
- 案件失效或协力结束后，支援先从 Army 脱离，再使用既有返程欲望和到达销毁流程。
  原版军团因断粮等规则暂时解散、但主办案件仍未结束时，支援记录不会丢失；后续
  小时会随协力军团恢复而重新加入。旧版“卡进城即清理”兼容逻辑现在跳过仍属于
  合法协力军团的支援，避免军团长进城时误删已经附着的增援。
- AI 诊断中，军团长的协力摘要新增 `supports/attachedSupports`；无领主增援显示
  `armySupport:leader/attached/distance/target`，并在首次加入时输出
  `DELAY_SUPPORT_ARMY_JOINED`。这能直接区分支援仅在军团名单中赶路，还是已经
  真正并入并计入战力。
- 最终 Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet
  警告。十八个 XML 全部解析通过；自动部署后核对二十五个可部署文件，实机缺失
  与哈希不一致均为零。客户端与编辑器 DLL 均为 `535552` 字节，SHA-256 为
  `2D53D8A77A585C811A0323690CBD8DF2899217C8A036D60D7888299E5CB41170`。
  仓库与实机中文 README SHA-256 均为
  `4AB58279082167806F36B664BFFC66BA4A6459489EF0E0D7C27DC47EAA96D6A4`；英文
  README SHA-256 均为
  `0B2671B91FE51DA3D587F53E55426598B63692A2EFE793CAD33AF80836DA46BB`。


## 2026-07-20 城外围堵驱逐范围扩大

- 用户选择沿用现有城内目标驱逐流程，只扩大触发范围，不新增强制靠近城门的
  高权重欲望。已撤销本轮尚未构建的 `GateApproach` 临时代码，案件追踪仍只有
  `Approach/Pursue/Escort/Visit` 四种职责，继续由原版欲望拍卖决定实际移动。
- `GwpTuning.Enforcement.ShelteredGateDistance` 从 `3f` 提高到 `12f`。最新实测中
  梵蒂停在案件目标约 `5.95` 距离，原三格触发线无法覆盖，而十二格与现有协力接触
  观察范围一致，可覆盖原版 `GoAroundParty` 常用的城外围堵位置。
- 这里只扩大距离，不放宽案件资格。驱逐仍要求非玩家案件、目标当前躲在定居点、
  本案件已经宣战，并且承办部队在外围停稳达到既有时长；目标离城后仍交回原版
  野战攻击与强弱判断。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `535552` 字节，SHA-256 为
  `E63CD33BC54FAFA82893173F83F21C0A886E8944FDB2BCEE41F52EE7077D3B89`。
  仓库与实机中文 README SHA-256 均为
  `D2C9160813F314BF573F2A2879E4926BC7CB0C99C7367597B1CD8BF2EFEF843D`；英文
  README SHA-256 均为
  `2AD438CD9CCFF5E7B48240AD809E1E5EB4D36A8CC9C6D21F2507B71D567DCA5D`。
- `git diff --check` 仍报告工作树既有的 `GwpAiDeterrenceState.cs:379` 文件末尾空行；
  本轮未修改该文件，也没有为处理无关用户改动而重写它。


## 2026-07-20 驱逐范围扩大后的实测诊断

- 用户进入同一存档继续等待约一至两天，确认无领主支援已经真正附着于梵蒂的
  原版军团，但地图上没有观察到案件目标被驱逐出城。本轮只读取日志、反编译实机
  DLL 并对比 Git 历史，没有修改运行行为、构建或部署。
- 最新诊断从战役小时 `625132.34` 持续到 `625159.62`：梵蒂案件始终为
  `lord_5_17/WarPursuit/war=True`，军团成员由 `attached=2` 保持不变，无领主支援
  最终从 `attachedSupports=0` 变为 `attachedSupports=1`。梵蒂位置始终为
  `(272.7485,409.1314)`，目标距离始终为 `5.95`，证明至少连续二十七小时没有发生
  超过 `0.35` 的位移，既有一小时停稳条件应已满足。
- 欲望拍卖不是阻断点。梵蒂每轮都是案件 `GoAroundParty=0.99` 胜出，原版最高进城
  候选仅约 `0.42`，短期行为在 `GoToPoint/None` 间切换；没有其他欲望夺走案件，
  也没有重新补给、逃跑、进入地图事件或进入定居点。
- 实机 DLL 反编译确认当前常量确实为 `ShelteredGateDistance=12f`，驱逐条件仍编译为
  `WarDeclared && distToGate <= 12f && stoppedHours >= 1`。根据 SandBox 的
  `settlements.xml`，梵蒂当前围堵的是 `castle_EW5`，其城门坐标为
  `(267.33,406.6704)`；梵蒂到城门的实际距离为 `5.95119`，明确处于十二格内。
- 当前诊断存在盲区：`HandleShelteredCriminal` 没有记录条件值或驱逐结果，
  `TryForceExpelShelteredCriminal` 又会吞掉全部异常。因此仅凭现有日志不能严格区分
  “`LeaveSettlementAction` 回调异常”与“成功离城后立刻重新进城”，但可以排除距离、
  停稳、案件战争状态和灰袍欲望拍卖这四类原因。
- Git 对比发现更直接的回归点：`v1.4.7-r4` 在
  `LeaveSettlementAction.ApplyForParty(expelParty)` 后会对军团长、附着成员和案件
  目标各执行一次 `SetMoveModeHold()`，注释明确说明是为了防止刚被逐出的目标立即
  钻回定居点。AI 欲望整理时，为满足“不直接写地图移动命令”，这一组一次性 Hold
  与其他长期强制追击代码一起被删除；当前版本只执行离城动作，然后立刻把目标交回
  原版 AI。目标仍处于城门坐标且面对更强灰袍军团时，原版安全/进城决策可在玩家
  可见之前重新进入同一座城。这是当前最符合代码历史与实测现象的根因。
- 后续修复应保持范围狭窄：恢复“驱逐成功后的短时防回城窗口”，或使用等价的一次性
  状态阻止同小时重新进入，而不恢复围堵期间的 AI 冻结、无限主动性或持续强制追击。
  同时应新增 `SHELTERED_EXPULSION_CHECK/ATTEMPT/RESULT` 诊断，记录聚落、城门距离、
  每小时位移、停稳小时、实际被逐出的军团长、调用结果及异常类型，以便下一次实测
  能严格确认离城动作是否成功以及何时重新进城。


## 2026-07-20 城内目标驱逐诊断增强

- 按用户要求本轮只增强后台侦查，不修改驱逐距离、停稳条件、原版欲望或目标离城
  后的行为。`HandleShelteredCriminal` 现在每个目标躲城小时输出
  `SHELTERED_EXPULSION_CHECK`，记录案件、目标、聚落、灰袍/聚落/城门坐标、到聚落
  与城门距离、阈值、目标躲城小时、本小时灰袍移动距离、停稳容差、连续停稳小时，
  以及战争、距离和停稳三项条件分别是否通过。
- 条件全部通过时先输出 `SHELTERED_EXPULSION_ATTEMPT`，明确原版离城动作实际作用于
  案件目标还是同城敌方军团长，并记录该部队进入前所在城和附着成员数。调用后输出
  `SHELTERED_EXPULSION_SUCCEEDED` 或 `SHELTERED_EXPULSION_FAILED`，同时记录目标与
  实际驱逐部队调用后的 `CurrentSettlement`；异常不再无声吞掉，而是写入异常类型和
  消息后仍沿用原有失败返回，不影响游戏继续运行。
- 普通 `HOURLY_STATE/AUCTION/RESOLVED` 的 `offenderState` 新增罪犯当前聚落和地图坐标。
  因此若驱逐当小时成功、下一小时又返回同一座城，日志可以直接用
  `SUCCEEDED` 后的 `currentSettlement=-` 与后续状态的
  `currentSettlement=原聚落` 串起完整证据。
- `tools/Watch-GreyWardenAI.ps1` 新增 `-Sheltered` 过滤器，只显示上述驱逐检查、尝试、
  结果和跳过事件；同时把领主类型过滤值从旧的 `founder_lord` 修正为运行日志实际
  使用的 `grey_warden_lord`，并把说明范围改为全部当前及未来灰袍领主。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `538624` 字节，SHA-256 为
  `5FA519949EDFD2C26C7B16B9A64F89A258379B54403C883CA93177E120A602A2`。
- `Watch-GreyWardenAI.ps1 -Once -Tail 1 -Sheltered` 参数解析与过滤器启动检查通过；
  当前旧会话日志尚无新增事件是预期结果，重新载入使用本 DLL 的战役后才会开始
  产生 `SHELTERED_EXPULSION_*` 行。


## 2026-07-20 驱逐成功后即时回城的实测定论与正式版诊断清理

- 用户使用诊断增强版本快进数小时后，日志给出确定闭环。梵蒂到
  `castle_EW5` 城门距离始终为 `5.951/12`，每小时位移为 `0.000`；第二个连续小时
  `warPassed/distancePassed/holdPassed` 全为 true，驱逐资格正常成立，不存在欲望、
  距离或停稳条件未通过。
- 战役小时 `625162.14`、`625164.13`、`625166.18`、`625168.18` 均出现完整的
  `CHECK -> ATTEMPT -> SUCCEEDED`。实际驱逐部队就是案件目标
  `lord_5_17_party_1`，不是敌军团长；调用前 `CurrentSettlement=castle_EW5`，调用后
  立即变为 `-`，没有异常，证明 `LeaveSettlementAction.ApplyForParty` 每次都成功。
- 每次成功后约 `0.45～0.49` 个战役小时，普通状态日志已经重新显示
  `currentSettlement=castle_EW5`，目标坐标仍为城门 `(267.33,406.6704)`。下一小时
  驱逐跟踪又从 `shelteredHours=1/stoppedHours=0` 重新开始。因此现象已严格证明为
  “目标成功离城后不到半小时立即进入同一座城”，而不是驱逐未触发或调用失败。
- 修复方向应只处理驱逐后的防回城窗口。历史版本的一次性 `SetMoveModeHold` 正是为
  此问题存在；若恢复，应限定在驱逐成功后的目标/同城军团成员，不恢复围堵期间的
  长期 AI 冻结、强制攻击或持续地图命令。
- 正式发行清理要求：仓库目前只有一个外部 PowerShell 查看器
  `tools/Watch-GreyWardenAI.ps1`，它不位于 `_Module`、当前也没有任何 `.ps1` 或测试
  文件被部署到游戏模组，因此可留在仓库开发工具区而不进入发行包。真正需要在正式
  构建前处理的是编入 DLL 的 `GwpAiDiagnostics.cs` 及八个源码文件中的调用：当前它
  会自动创建并持续写入玩家 Documents 下的 `GreyWarden-AI-Diagnostics.log`。
- 在发布候选阶段增加硬性清理项：关闭或条件编译掉 `StartSession/WriteState/
  WriteAuction/WriteResolved/WriteAction/WriteMapEvent` 全部运行时写盘路径，并验证正式
  DLL 不再包含诊断日志文件名或 `SHELTERED_EXPULSION_*` 字符串；保留仓库 `tools`
  脚本和维护历史即可。若未来仍需玩家协助排障，应另做默认关闭、明确手动启用的诊断
  开关，而不是正式版每小时全量写盘。


## 2026-07-20 恢复驱逐后短暂停留并保留通用 AI 观测

- 按用户要求修复已证明的“成功离城后约半小时立即回城”。恢复 `v1.4.7-r4` 已验证
  方案：`LeaveSettlementAction.ApplyForParty` 成功后，对实际被逐出的部队、其附着
  成员及案件目标各执行一次 `SetMoveModeHold()`。该命令只清掉刚出城时立即选择的
  回城移动；下一次原版 AI 思考、移动或接战会自然接管，不保存禁城状态，也不会让
  目标永久无法进入任何聚落。
- 驱逐资格保持不变：非玩家案件、目标在城内、本案件已宣战、灰袍距城门不超过
  十二格并在外围连续停稳一小时。围堵期间没有恢复旧版 AI 冻结、无限主动性、自动
  补粮或持续强制攻击；只有驱逐成功后的目标侧使用一次 Hold。
- 用户澄清诊断需求：开发期仍需持续观察全部当前及未来灰袍领主、所有无领主队伍、
  任务、欲望拍卖、军团/协力、兵力和经济字段。因此保留并恢复通用
  `GwpAiDiagnostics`、军团/地图事件动作记录和 `tools/Watch-GreyWardenAI.ps1`。
- 已删除刚才为城墙问题新增的全部 `SHELTERED_EXPULSION_*` 专项检查、尝试、结果和
  异常日志，也删除脚本的 `-Sheltered` 过滤器；通用日志不再逐小时输出城门距离、
  停稳计数等已解决问题的噪声。查看脚本继续支持按领主/无领主类型及具体 Party ID
  过滤，普通状态仍含任务、欲望、军团、附着、粮食、粮耗、可用天数、领主/家族资金、
  工资、欠薪、兵力、伤员和俘虏。
- 正式发布原则不变：PowerShell 查看器留在仓库 `tools`，不进入 `_Module`；当前
  DLL 内通用诊断只用于开发测试，正式发布候选时再统一关闭运行时日志写盘，而不是
  在当前仍需经济和 AI 测试的阶段提前删除。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `535552` 字节，SHA-256 为
  `928D31CEB6F556E9B16A1F7BB40A8501D9E5C9E25058E6DCBE6B7E9E8B6DDB3B`。
- ILSpy 反编译实机 DLL 确认 `TryForceExpelShelteredCriminal` 在原版
  `LeaveSettlementAction.ApplyForParty` 后依次调用实际驱逐部队、附着成员和案件
  目标的 `SetMoveModeHold`；通用诊断仍包含 `GreyWarden-AI-Diagnostics.log`，但 DLL
  与源码均不再包含任何 `SHELTERED_EXPULSION_*` 事件。查看脚本参数启动检查通过。
- 仓库与实机中文 README SHA-256 均为
  `FE5AE7ACAE36083CF294BFB630ADBB4557489E48D1B06C8ADD5064147180AB21`；英文 README
  SHA-256 均为
  `046AF2A4C5C176755A96DD6B3F66CEE884931905A4FF72DBEA590134FB7D6E54`。


## 2026-07-20 已宣战案件目标禁止重新进入聚落

- 用户复测确认一次性 `SetMoveModeHold` 仍不足：它只清空当前移动，原版下一次短期
  AI 决策仍会立即再次调用进城。因此保留驱逐后的 Hold 作为第一帧清理，并新增真正
  的案件期进城拦截，不再依赖下一次 AI 是否重新选择安全聚落。
- 反编译 Bannerlord `v1.4.7` 确认所有普通部队进城最终统一经过
  `EnterSettlementAction.ApplyForParty(MobileParty, Settlement)`，该方法会直接设置
  `CurrentSettlement` 并触发聚落进入事件。新增 Harmony 前缀只在进入部队属于一宗
  当前 `WarDeclared=true` 且目标仍有效的非玩家案件时返回 false，并清掉本次进城
  移动；其他所有部队和所有非案件进城继续执行原版方法。
- 拦截对象包括案件罪犯本人，以及罪犯当前原版 `Army` 中的军团长和成员。这样无论
  是领主协力军团还是同案无领主支援先把目标拖入野战，同一个承办案件都会阻止目标
  或同军团成员先钻回聚落再把其他成员吸回去。无领主支援不需要另建第二套禁城状态。
- 禁城生命周期完全绑定案件现有数据，不保存永久名单：案件结束、换案、目标失效、
  `WarDeclared` 因外交和平检查重置，或目标退出该军团后，前缀下一次检查自然放行。
  玩家案件明确排除，仍保留原有玩家对话、补给和进城规则。
- 通用 AI/经济诊断保持不变，仍观察全部灰袍领主和无领主队伍；没有重新加入任何
  `SHELTERED_EXPULSION_*` 城墙专项日志。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `536064` 字节，SHA-256 为
  `8EC2F78CB8777B841308B8003089370E835ACF07B2586576728E39E771C77468`。
- ILSpy 反编译实机 DLL 确认 `GwpCaseSettlementEntryPatch` 精确 Harmony patch 原版
  `EnterSettlementAction.ApplyForParty`，案件目标命中时执行一次 Hold 并返回 false；
  `PoliceEnforcementBehavior.IsSettlementEntryBlockedByActiveCase` 实机方法包含
  `WarDeclared`、目标有效、非玩家、罪犯本人及同 Army 成员判断。源码中的
  `SHELTERED_EXPULSION_*` 计数为零。


## 2026-07-20 驱逐目标改为案件期间持续 Hold

- 用户进一步明确目标不是“阻止进城但仍允许原版在野外移动”，而是驱逐成功后始终
  `Hold`，直到案件生命周期结束。此前一次性 Hold 和仅拦截进城入口都不足以满足该
  语义，因此新增每帧维护的案件级 Hold 集合。
- 驱逐前记录实际被逐出的原版军团长、当时全部附着成员和案件罪犯的 Party ID；
  `LeaveSettlementAction` 成功后立即 Hold，并把这批 ID 绑定到承办任务 ID。之后
  `CampaignEvents.TickEvent` 每帧对仍活跃、未处于地图事件且不在聚落内的记录部队
  重复 `SetMoveModeHold()`，原版小时/短期 AI 即使重新选择移动也会被持续覆盖。
- 进入战斗时 `MapEvent != null`，持续 Hold 暂停，避免干扰地图事件建立和战斗结算；
  原版 `EnterSettlementAction.ApplyForParty` 前缀仍作为保险，只对当前 Hold 集合中的
  部队拒绝进城。
- Hold 集合不写入存档，也不永久绑定英雄。每帧先检查原承办任务是否仍存在、
  `WarDeclared=true`、目标有效且不是玩家；案件结束、换案、和平回退、目标失效或
  读档重建时集合被删除/清空，相关部队随即恢复原版移动和进城。
- 该机制由承办案件建立，因此领主协力军团和同案无领主支援共享同一个被 Hold 的
  目标集合；无需按攻击者类型重复实现。通用 AI/经济诊断继续保留，城墙专项日志
  仍为零。
- Release `--no-restore` 构建通过，零错误、四十四条既有可空/离线 NuGet 警告；
  十八个 XML 全部解析通过。自动部署后核对二十五个可部署文件，实机缺失与哈希
  不一致均为零。客户端与编辑器 DLL 均为 `538112` 字节，SHA-256 为
  `8A52ACC0A6E52EE9067A636BE7DD2B6B7227FF2EB5A5CCC7EEC6BAA53CFFB39A`。
- ILSpy 反编译实机 DLL 确认 `OnTick` 在任何玩家俘虏早退之前调用
  `MaintainShelteredCaseHolds`，并逐帧遍历任务绑定 ID 调用 `SetMoveModeHold`；
  `TryForceExpelShelteredCriminal` 在离城前保存军团长、附着成员与罪犯 ID，随后建立
  Hold 集合。通用诊断保留，源码中的 `SHELTERED_EXPULSION_*` 计数仍为零。
- 仓库与实机中文 README SHA-256 均为
  `2D4D1D0F2645316EF45F325750D0EA8DCBB9DACE04C05A2DE28A190A18BF0B51`；英文 README
  SHA-256 均为
  `86310457F97CD6CD3F6019D81C7ABE633A78E2B6F2E7F152367A3875B71828EF`。


## 2026-07-20 v1.4.7-r5 正式发行分层、安装包与验证

- 正式版本号定为 `v1.4.7-r5`。玩家 README 的当前条目只写相较 `v1.4.7-r4` 的玩法结果：
  案件总卷、原版欲望办案、灰袍真实经济、协力军团、无领主支援及本轮关键卡死修复；
  同时按用户新增规则原样保留上一正式版 `v1.4.7-r4` 的玩家日志。以后每次正式发布均
  只保留最新两个版本，新版本在前，第三旧版本移除。诊断字段、内部权重、测试过程和
  构建细节不进入玩家发布说明。
- 明确分离开发测试构建和玩家发行构建。`GwpDiagnosticsEnabled` 默认保持 `true`，普通
  Release 构建继续把完整 `GwpAiDiagnostics` 编入 DLL，并自动同步到
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden`，因此
  工作目录源码、查看脚本和本地实机测试模块仍可持续输出并读取 AI/任务/军团/经济日志。
- 正式包使用
  `-p:GwpDiagnosticsEnabled=false -p:DeployToLiveModule=false` 单独编译到
  `C:\Users\lucif\source\repos\GreyWardenPolicePurity\build-check\release-player`。
  该分支把 `GwpAiDiagnostics` 编译为空壳，全部写入方法为空、`ShouldTraceParty=false`、
  `LogPath=string.Empty`；同时禁止自动覆盖本地实机测试模块。ILSpy 反编译已确认发行 DLL
  不创建目录、不创建文件、也不写入任何测试日志。
- 本地实机测试 DLL 为 `537600` 字节，SHA-256
  `99A9FE9781AD62DB62038FC806F176D0D3D3049218BE67DF144FDA18DBEC25BD`；玩家发行 DLL
  为 `526848` 字节，SHA-256
  `4E3EF8BA10061EBF00D1FD35B2BB7165EB1B55344EBC8520F7720950DCB9A154`。二者分开保存，
  玩家包的制作没有替换实机测试 DLL。
- 下述 ZIP 哈希是加入“双版本玩家日志”规则前的首轮候选值，已废弃，不能上传；最终
  README、ZIP 和校验值见本节后续的最终验证补记。首轮候选安装包位于
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r5.zip`，
  大小 `349767453` 字节，SHA-256
  `5275559FB95B3979F7B9D742670EA1C937801E0D8E329C9526878C893B9E89A9`；校验文件为同目录
  `GreyWarden-v1.4.7-r5.zip.sha256`。ZIP 顶层唯一目录为 `GreyWarden/`，发行 DLL 从包内
  解出后与独立发行构建哈希完全一致。
- 安装包沿用 `v1.4.7-r4` 的正常客户端结构，并加入本版案件总卷界面。内容检查确认不含
  `tools`、PowerShell、测试日志、诊断目录、PDB、编辑器二进制、`Assets`、`AssetSources`、
  `RuntimeDataCache` 或任何嵌套压缩包；仓库中的 `tools/Watch-GreyWardenAI.ps1` 只供本地
  开发观测，不进入 ZIP 或 GitHub Release 资产内部。
- 开发构建与独立发行构建均零错误；现有四十三至四十四条可空性/离线 NuGet 警告未新增
  运行阻断。十八个 XML 全部解析通过。仓库 `_Module` 的二十五个可部署文件与实机测试
  模块逐文件核对，缺失与哈希不一致均为零。
- 正式发布沿用仓库既有流程：提交到 `main`，打注释标签 `v1.4.7-r5`，并将上述 ZIP 与
  `.sha256` 作为 GitHub Release 资产发布；不为本次正式版本额外创建中间 PR。

### 双版本玩家日志规则后的最终候选

- 用户在正式发布前补充长期规则：中英文玩家 README 永远保留最近两个正式版本的
  玩家日志。本次已从 `v1.4.7-r4` 标签恢复 `2026-07-19 v1.4.7-r4` 原始中英文条目，
  放在精简后的 `2026-07-20 v1.4.7-r5` 条目之后；当前两份文件均只含 `r5`、`r4`，
  顺序正确。根目录 `AGENTS.md`、本维护文档的发布日志规则和正式发布清单均已同步，
  并一并固化“开发测试诊断可保留、玩家包必须使用独立无诊断 DLL”的边界。
- 双版本日志更新后重新执行普通 Release 构建，实机测试模块继续得到诊断启用 DLL；
  `_Module` 二十五个可部署文件与实机逐文件一致。最终中文 README SHA-256 为
  `FB0D2723CEE63EFE995970BA6078DDAED50353550FB67044D18B57C0194374B2`，英文为
  `B691FFEE5A5864D26C7DD52F862B4E382E9FFD59E3D54A51999AD6975E802B07`。
- 最终安装包仍位于
  `D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden-v1.4.7-r5.zip`，
  已覆盖首轮候选，最终大小 `349768562` 字节，SHA-256
  `FA4C3157415D5B67431DF1C0269CD5882D013A1613D1B811D033FD46331CCEC2`；对应
  `.sha256` 已同步重写。最终 ZIP 继续使用哈希为
  `4E3EF8BA10061EBF00D1FD35B2BB7165EB1B55344EBC8520F7720950DCB9A154` 的独立无诊断
  玩家 DLL；首轮候选哈希 `5275559F...` 作废，禁止上传。
