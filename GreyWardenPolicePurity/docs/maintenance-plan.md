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

## Release packaging and asset layout

### Player archive

- Public artifact name: `GreyWarden-v1.4.7.zip` with a sibling SHA-256 file.
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
6. Create `GreyWarden-v1.4.7.zip` and its `.sha256` file.
7. Inspect ZIP paths and extract-test it.
8. Run at least one battle that renders the black shield, exit the client, and
   check delayed Windows Application/WER/dump state.
9. Commit source and documentation changes, then push GitHub.
