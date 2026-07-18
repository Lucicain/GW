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
  change must update `_Module/README.md` in the same change.
- Write the `最近更新` section from the player's point of view: state what the
  player will see, what can affect their character or units, and the exact
  chances, ranges, damage, cooldowns, or other values that matter in play.
- Clearly separate ordinary behavior, exceptional behavior such as an item
  breaking, and unchanged behavior. Do not hide player-visible consequences
  behind implementation terms such as callbacks, patches, or synthetic blows.
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

1. Build `Release` against Bannerlord `1.4.7`.
2. Confirm the live module has no `Assets`, `AssetSources`, or
   `RuntimeDataCache` directory.
3. Confirm both runtime TPAC hashes above.
4. Confirm the player README describes functions/results only and matches the
   live copy.
5. Stage one top-level `GreyWarden` directory without editor binaries, PDBs, or
   source assets.
6. Commit and push the release source and documentation to GitHub as part of
   the same formal release task.
7. Create `GreyWarden-v1.4.7.zip` and its `.sha256` file directly under the
   game's `Modules` directory, never under `Modules\GreyWarden` or `_Module`.
8. Inspect ZIP paths and extract-test it, then create/update the GitHub release
   and upload the matching ZIP and checksum.
9. Run at least one battle that renders the black shield, exit the client, and
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
