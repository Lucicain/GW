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

## Release packaging notes

- Keep the complete editable module locally and in the source repository, but
  never include `Assets`, `Assets_EditorBackup`, `AssetSources`, or
  `RuntimeDataCache` in a public player archive. Those directories contain the
  Modding Kit work tree and generated cache rather than runtime deliverables.
- A formal player archive must have one top-level `GreyWarden` directory and
  contain only the published `AssetPackages`, client binaries, `GUI`,
  `ModuleData`, `ModuleSounds`, `Shaders`, `README.md`, and `SubModule.xml`.
  Exclude `Win64_Shipping_wEditor`, `.pdb`, and source FBX files. Validate the
  ZIP entry list before upload instead of relying only on copy exclusions.
- The first formal GitHub release uses module/tag version `v1.4.7` and artifact
  name `GreyWarden-v1.4.7.zip`; publish a separate SHA-256 checksum file beside
  it.
- The current isolated client retest keeps the complete package inherited from
  the old GreyWarden module as
  `AssetPackages/gwp_inherited_legacy_assets.tpac`. It retains all original
  models and the `bo_cap_wlarge_shield` / `bo_wlarge_shield` physics shapes. Its
  verified size is `332,944,246` bytes and its SHA-256 is
  `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`.
- The newly published `AssetPackages/pack0.tpac` contains the corrected static
  `wlarge_shield_black_static` metamesh, `gwp_black` material, and three
  black-and-gold textures. Its final verified size is `39,073,074` bytes and its
  SHA-256 is
  `D14AE4B3F8576F963C9BF3B0A829402206F2EDDCE464202A24340612CFA1C287`.
- The former material-only `gwp_black_assets_published.tpac` is not deployed in
  this retest because the new `pack0.tpac` already contains those resources. A
  verified backup remains on the desktop.
- The Modding Kit always writes client assets to `AssetPackages/pack0.tpac` and
  offers no package-name field. Before publishing, preserve the inherited TPAC
  under its `gwp_inherited_legacy_assets.tpac` name and restore it beside the
  newly generated `pack0.tpac` if the editor removes it.
- The rejected Z-up static-mesh publication is preserved only for analysis at
  `C:\Users\lucif\Desktop\gwp_static_mesh_zup_experimental_2026-07-15.tpac`.
  It is `39,073,136` bytes, SHA-256
  `443AF02C5BB92F5AD17A8B453810C0E7C96548EFBAC5B8E5981F7252F9598DCA`, package GUID
  `807e3d8d-e501-499b-a1d3-22ae3f4e64f3`, with one metamesh, one material, and
  three textures. Do not deploy it in the stable client.
- Both current TPAC files are kept as verified local repository copies but
  ignored by Git. A fresh checkout must restore them from the documented
  recovery/desktop backups and verify both SHA-256 values before building a
  distributable module.
- Multiple TPAC files with different names are valid in Bannerlord. Native and
  NavalDLC both ship many differently named TPACs in one `AssetPackages`
  directory, so the two filenames alone are not evidence of a packaging error.
- Preserve `Assets` and `AssetSources`; they are the Modding Kit's editable
  resource data and are required to reopen, inspect, and republish the project.
  The 2026-07-15 client log proves that when a folder named `Assets` exists in
  the live module, this engine build logs `Loading packages
  $BASE/Modules/GreyWarden/Assets...` and does not load that module's
  `AssetPackages`. An incomplete editor tree therefore makes all inherited
  equipment resources disappear even when both published TPAC files are valid.
- Never delete `Assets`, `AssetSources`, or `RuntimeDataCache` as part of a
  build, deployment, rollback, or client test. Use a reversible folder-name
  switch for `Assets` (or maintain separate editor and runtime module copies):
  restore the exact `Assets` name before opening the Modding Kit, and temporarily
  use a non-reserved name before normal-client testing so `AssetPackages` loads.
  The ordinary build excludes editor data from its copy operation only so it
  cannot overwrite newer editor-side work. Preserve repository copies as a
  recovery backup and update them intentionally after editor changes.
- The resulting two-file layout is two legitimate resource packages, not a
  binary merge. Do not concatenate them. The community TpacTool can
  inspect/export but cannot write or merge TPAC packages.

## Bannerlord 1.4.7 native shutdown crash

- Windows dumps from processes `78556` and `2300` show the same native
  allocator cleanup stack after `Managed Interface deleted`, with invalid
  pointer reads at `TaleWorlds.Native.dll+0x74B34A` and `+0x74B1F0`.
- The later dump proves the explicit `OnGameEnd` material release did not fix
  the crash; it only removed the separate non-zero device-reference warning.
- Process `84476` reproduced the same failure bucket and allocator-cleanup
  thread after the managed bridge had already been deleted, this time at
  `TaleWorlds.Native.dll+0x74B3F1`. RTTI identifies the queued object as an
  engine `ftlObject`, which points to deferred resource/object destruction
  rather than the removed managed wrapper cache.
- The live module had the inherited complete `pack0.tpac` (332,944,246 bytes)
  and the newly published `gwp_black_assets_published.tpac` (46,231,793
  bytes). The package headers have different package UUIDs, and the new package
  contains five assets: four shield textures and the `gwp_black` material.
- The current diagnostic client temporarily omits the new package and uses
  factor tinting on private mission meshes. The published package remains in
  the repository but is excluded from the build copy target, so it is not
  destroyed and cannot be accidentally reintroduced before the exit test.
- This A/B test can determine whether the crash belongs to the custom-resource
  path as a whole. It does not prove that having two TPACs or having different
  filenames is the cause. If the crash disappears, test the new package loaded
  but unused next to distinguish package loading from runtime material swap.
- Process `92968` crashed with the new material package absent and only the
  inherited `pack0.tpac` present. Windows reported the already observed
  `TaleWorlds.Native.dll+0x74B1F0` invalid-pointer read after `Managed Interface
  deleted`. This rules out the second TPAC, its external filename, and a
  two-package collision as necessary causes of the crash.
- The next diagnostic client removes the entire
  `MissionWeapon.GetWeaponData` lord-shield visual postfix. It performs no
  `MetaMesh` or `Mesh` retrieval, material replacement, or factor-color write.
  If shutdown succeeds, the remaining suspect is runtime mutation/wrapper
  creation on the weapon mesh. If it still fails, shield visuals and both TPAC
  naming theories are excluded together.
- The no-visual-postfix test exited without the native error while only the
  inherited `pack0.tpac` was loaded. Compared with process `92968`, this
  isolates the required trigger to the runtime lord-shield mesh path rather
  than the inherited package name.
- Final confirmation restores `gwp_black_assets_published.tpac` beside the
  inherited `pack0.tpac` but keeps the visual postfix disabled. The shield uses
  its original appearance, so this test loads the new package without creating
  mesh wrappers, swapping materials, or writing factor colors.
- The final confirmation also exited normally. The two TPAC packages and their
  names are therefore cleared; the native shutdown crash requires the runtime
  `MissionWeapon.GetWeaponData` mesh-access/mutation path. Keep that patch
  disabled and restore the black-and-gold appearance through a statically
  authored duplicate mesh instead.
- The static replacement was published on 2026-07-15. Binary inspection of the
  new `pack0.tpac` confirms `wlarge_shield_black_static`, `gwp_black`, and all
  three black-and-gold texture resources. Binary inspection of the renamed
  inherited package confirms both original shield physics shapes remain
  available. The lord-only item now directly references the static metamesh.
- The first in-game static-mesh test did not show a Windows crash dialog, but
  its log still ended with `Non-Zero Device Reference Count (ERC2524)`. After
  the asset was republished with both `Import meshes` and `Convert to Z-up`, the
  next in-game test reproduced the original native shutdown failure at
  `TaleWorlds.Native.dll+0x74B34A`. The imported static metamesh therefore is
  not a stable replacement and must remain unused in the release client.
- The first static visual was rotated because the recovered source FBX was
  Z-up while the Blender export was tagged Y-up. `Convert to Z-up` corrected
  that axis but left the shield reversed relative to the inherited physics
  shapes. The six LOD transforms and bounds otherwise matched exactly; the
  empty parent node was identity and was not the cause. Preserve the experiment
  for analysis, but do not compensate through item XML because that would also
  rotate the reused collision bodies.
- The stable release rollback keeps the inherited package as `pack0.tpac`,
  restores the former material-only `gwp_black_assets_published.tpac`, and
  points the lord shield back to `wlarge_shield`. Both runtime recoloring and
  `wlarge_shield_black_static` remain disabled.
- The 2026-07-15 rollback retest exited without a Windows crash event and the
  original shield orientation was correct. The custom black material was not
  visible because this baseline deliberately references the inherited
  `wlarge_shield` metamesh, which remains bound to its inherited material. Its
  `rgl_log` still ended with `Non-Zero Device Reference Count (ERC2527)`, so
  that line by itself is not evidence that the newly imported mesh is faulty;
  only a matching Windows application error/native crash should fail a test.
- The next 2026-07-15 editor publication is a new isolated retest, not the
  rejected package above. The live runtime layout uses the verified inherited
  package as `gwp_inherited_legacy_assets.tpac` and the newly published static
  shield package as `pack0.tpac`. The lord-only item directly references
  `wlarge_shield_black_static`; the ordinary shield remains on
  `wlarge_shield`, and both still reuse the inherited collision bodies.
- Before this client test, the editable `Assets` directory is preserved by
  renaming it to `Assets_EditorBackup`. This is required because the client log
  proved that an exact `Assets` directory makes this engine build bypass the
  module's `AssetPackages`. Restore the exact `Assets` name before reopening
  the Modding Kit; never delete the editor work tree.
- Read-only Blender 5.2 comparison on 2026-07-15 rules out extracted geometry
  corruption as the cause of the reversed shield. The recovered binary FBX and
  current `dun.fbx` have identical six-LOD vertex/face counts, world bounds,
  object transforms, sampled vertices, polygon normals, and determinant `+1`.
  Only their FBX global axis declarations differ: the recovered file declares
  Z-up, front `-Y`, coordinate `+X`, while `dun.fbx` declares Z-up, front `+Y`,
  coordinate `-X`. Reversing both horizontal signs is the observed 180-degree
  turn around Z.
- A controlled Blender 5.2 export matrix confirms that `Forward: Y` (positive
  Y) and `Up: Z` reproduces the recovered file's front/coordinate signs;
  `Forward: -Y` produces the reversed signs found in the failed build. `Apply Transform` does
  not change those declarations. The corrective re-export must therefore leave
  the mesh transforms untouched, use positive Y forward and Z up, and the
  Bannerlord import must not apply `Convert to Z-up` again. Do not compensate
  through item rotation because the inherited collision bodies are already in
  the correct orientation.
- Final user testing on 2026-07-15 confirmed the positive-Y/Z-up publication:
  all inherited equipment rendered, the lord shield used the corrected
  black-and-gold material and orientation, and all shield defense features
  behaved normally. The 18:35-18:39 normal-client run loaded
  `GreyWarden/AssetPackages`, rendered both shield variants, and exited without
  a matching Windows Application Error event. Its log still printed
  `Non-Zero Device Reference Count (ERC2852)`, confirming again that this line
  alone is not equivalent to the former native exit crash.

## Legacy pack0 recovery

- The inherited package was fully inventoried and recovered to
  `C:\Users\lucif\Documents\GreyWarden旧资源恢复\pack0_2026-07-15`.
- Package GUID: `cec987dc-80fc-47dd-9865-6fe9e9274db3`; SHA-256:
  `957DD525945E3B18545242D44AC1B0C55F180060A2F917261286CB1D0CCEDE40`.
- Recovered inventory: 20 metameshes, 18 materials, 33 textures, and 2 physics
  shapes. All 20 models were exported as both FBX and DAE; all 33 textures were
  exported as both PNG and DDS; material parameters/dependencies were saved as
  JSON. Every external segment was also preserved in stored and decompressed
  form. The batch report records 599 successful operations and zero failures.
- The original TPAC was copied into the recovery directory and its hash was
  verified. `working_sets\wlarge_shield` contains the shield FBX/DAE, original
  texture set, original material metadata, and the current black-and-gold
  sources for the static replacement-mesh workflow.
- Exported FBX/DAE files reconstruct the packed game data; they cannot restore
  the deleted DCC project history or never-published helper objects. Physics
  shapes have no common-format exporter, so their metadata and raw segments
  are preserved and the black shield should continue referencing the existing
  `bo_cap_wlarge_shield` and `bo_wlarge_shield` resources.
- Do not cache or manually invalidate `Material`, `Texture`, or `Mesh` wrappers
  for shield recoloring. Do not call `Mesh.SetMaterial` for an optional resource
  that lives in a separately published partial package.
