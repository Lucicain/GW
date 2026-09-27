# 资产管线：打包布局、盾牌 LOD 流程、失败恢复表、旧包回收

> 从 `maintenance-plan.md` 原样迁出的参考资料，正文未改。

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

Since 2026-09-27 the module ships one package, published by the Modding Kit from
the rebuilt editor sources:

- `AssetPackages/pack0.tpac` — 371,237,229 bytes. 146 assets: 21 metameshes
  (the 20 inherited armour/harness/shield models plus `wlarge_shield_black_static`),
  19 materials, 36 textures, 2 physics shapes (`bo_cap_wlarge_shield`,
  `bo_wlarge_shield`), 4 skeletal animations and 64 animation clips (dual wield).
  Its asset names match the three packages it replaces one for one (texture
  names are now lower case).
- It is gitignored (over GitHub's file-size limit) and lives in
  `_Module/AssetPackages/` for deployment. Do **not** put
  `gwp_inherited_legacy_assets.tpac`, `gwp_black_gold_shield.tpac` or
  `gwp_dual_wield_animations.tpac` back beside it: same asset names.
  (依据：设计——同名资源冲突)
- The three old packages are kept as the pre-rebuild rollback point in
  `.codex_tmp/legacy-packages-before-rebuild-20260927/` (SHA-256 prefixes
  `957DD525945E3B18`, `2A572A2FD5914EF7`, `652634753C25CEFB`); the inherited one
  is the only original of the July 2024 armour and must not be deleted.

### Local layout: the live module stays in development layout

The live `GreyWarden` module on this machine always holds `Assets`,
`AssetSources` and `RuntimeDataCache` beside `AssetPackages`, so the user can
keep working in the Modding Kit at any time. Do not move them out after a publish
or before a client test. (依据：用户裁定 2026-09-27)

- With those directories present, the editor and the client load
  `Modules/GreyWarden/Assets` and skip `AssetPackages` (rgl log:
  `Loading packages $BASE/Modules/GreyWarden/Assets...`). Since the full rebuild,
  `Assets` holds every mod asset, so in-game tests see what the Kit last saved.
- `AssetPackages/pack0.tpac` is what the Kit last published. It is what players
  load, so a Kit change that should ship needs a publish.
- The release layout (no editor directories) exists only inside the release
  ZIP, which already excludes them (`../state/build-and-deploy.md`). It is built
  in staging when the user asks for a release, never by rearranging live.
- `Verify-LiveModule.ps1` ignores the three editor directories, so it passes in
  this layout.

On 2026-09-27 the agent parked the three directories in
`Modules/_GreyWardenEditorWorkspace` after a publish, which left the user unable to
edit; they were moved back the same day and that folder no longer exists.

### After a Modding Kit publish

The Kit clears the live `AssetPackages` directory and writes only `pack0.tpac`
(it publishes every loaded module, see below). After a publish:

1. Wait until the Kit has fully exited.
2. Copy `pack0.tpac` to repository `_Module/AssetPackages/` (git-ignored) and
   compare hashes with live.
3. Do not put back the three pre-rebuild packages beside it: they hold the same
   asset and clip names. They stay only as rollback in
   `.codex_tmp/legacy-packages-before-rebuild-20260927/`, and
   `gwp_inherited_legacy_assets.tpac` there is never deleted.

### Material and cloth notes from the rebuild

A first default-settings import (2026-09-27) created the 22 `_geo.tpac` and 33
`_tex.tpac` correctly (LODs, per-material split, skinning, BC5 normal maps all
right) but no materials, and the editor build then raised
`CONTENT WARNING: Unable to find material for mesh ...` once per mesh (about 200
popups, each with a ~700 MB dump). TpacTool cannot write materials
(`AssetItem.WriteMetadata` throws `NotImplementedException`), so the materials
have to be made in the Kit. Once the 18 materials existed, re-importing the 22 FBX let the Kit bind each
submesh to the material whose name matches the FBX material slot (19 of 20 models
exact; `winfarmor.lod1.2` came out empty and is assigned by hand). Cloth pieces must
share positions across UV seams as the original does, or the simulation tears
them apart; `modkit-fbx.py` welds exactly-coincident vertices on `*clo`
materials only (see `tools/tpac/README.md`). Cloth
simulation is not carried by the FBX: the eight LOD0 cloth submeshes listed in
the journal (2026-09-27) were flagged again in the Cloth Editor. On the user's
later request, the live cloth presets, maximum distances, and collision bodies
were compared with the inherited package and the differences restored to its
values. The current comparison, rollback, and remaining game check are in
[`../state/cloth-assets.md`](../state/cloth-assets.md). Kit must publish again
before its `pack0.tpac` carries these live edits. The material recipe is `gwp_black`'s:
same shader (`328d3572-…`), flags `use_specular`, `do_not_use_vertex_color_as_occlusion`,
slots 0/2/4 = `_d`/`_n`/`_s`, plus the `skinning` vertex layout for every
material except `wlargeshieldmat`.

Skinned FBX must list their bones depth-first in the vanilla skeleton's bone order: the Kit
numbers skin bones by walking the FBX's own hierarchy and never maps names onto the vanilla
skeleton, and there is no skeleton option in the import dialog. `horse_skeleton` needs the
re-parenting that `modkit-fbx.py` does (see `tools/tpac/README.md`); the first rebuild of
`wharness` / `wharnesscom` shipped without it and the neck plate followed the left hind leg in
game. (依据：C++反汇编 2026-09-27)

The user performs all Modding Kit/editor interaction. Do not control the editor
for them.

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
| Some mod assets are missing in game | Client loads live `GreyWarden/Assets` (development layout), and that asset has not been imported/saved in the Kit | Import or save it in the Kit; do not move the editor directories out of live |
| Publishing leaves only `pack0.tpac` | Modding Kit clears `AssetPackages` on publish | Expected since the full rebuild: `pack0.tpac` holds every mod asset; copy it to the repo and compare hashes |
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

- Package GUID: `cec987dc-80fc-47dd-9865-6fe9e9274db3`.
- Inventory: 20 metameshes, 18 materials, 33 textures, and 2 physics shapes.
- The July 2026 export directory `Documents\GreyWarden旧资源恢复\pack0_2026-07-15`
  no longer exists (checked 2026-09-27). It is not needed: the TPAC below can be
  re-exported at any time with `tools/tpac/tpac-export`.

## Full rebuild from TPAC

Tools and commands: `tools/tpac/README.md`. Output (both under the gitignored
`_Module/AssetSources/`, so ordinary builds never copy them to the live module):

- `GreyWardenRebuild/` holds the FBX and PNG side by side and the FBX reference
  their textures by bare file name. Nested subfolders under `AssetSources` are
  also fine for the Kit (user, 2026-09-27); the flat layout is just what the
  rebuild happens to use. `Assets/GreyWardenRebuild/.gitkeep` was created to
  mirror the shield's `Assets/GreyWardenRecovery/`; it is not known to be required.
- The 20 models are in Modding Kit layout: one object per LOD named `<name>`,
  `<name>.lod1` ... `<name>.lod5`, each carrying its material slots (the Kit
  itself splits by material into `<name>.0`, `.1` ...). These are the names
  `items.xml` references. Armour is skinned to vanilla `human_skeleton`, the two
  horse harnesses to `horse_skeleton`; `wlarge_shield` is static. The raw
  one-object-per-submesh export is kept in `_reference/raw-fbx`.
- 33 PNG at 4096×4096 (22 were DXT1, 11 were BC5 normal maps). BC5 stores only X
  and Y, so the exporter rebuilds the blue (Z) channel. The DXT1 ones are written
  as plain RGB: an all-255 alpha made the Kit import them as DXT5 /
  `B8G8R8A8_UNORM`, and putting such a texture into a material slot crashed the
  material editor (see journal 2026-09-27). The shield's textures are DXT1 / `B8G8R8`.
  Texture file names are written in lower case, since the Kit names the texture
  after the file. Picking a mixed-case texture (`WInfMatObsh_d`) into a material
  slot crashed the material editor; after renaming to lower case and re-importing,
  the same action works (user, 2026-09-27). The alpha channel alone was not it.
  Do not give Kit-imported textures or materials upper-case names. (依据：实机观察 2026-09-27；C++反汇编：wEditor `TaleWorlds.Native.dll` 0x907b50 按名字逐字节查表)
- `bo_cap_wlarge_shield` (21 vertices / 36 faces, physics material `metal`) and
  `bo_wlarge_shield` (11 / 8, `metal_shield`) as FBX. These are the names
  `items.xml` uses for `body_name` / `shield_body_name`.
- The 4 dual-wield skeletal animations are in `_reference/animations-fbx`, kept
  out of `AssetSources` for now.
- `_reference/<package>/manifest.json`: per material the shader flags, texture
  slots (0 diffuse, 2 normal, 4 specular) and extra settings; per submesh the
  material, flags, factor colours and cloth-simulation material (`heavy_leather`,
  `medium_leather`, `horse_scale_armor`); the 64 dual-wield animation clips with
  all their parameters. `_reference` also holds Collada copies and the raw
  collision OBJ files.

Checked: re-reading every FBX gives the same mesh count, face count, and material
names as the package. Not carried over: the second UV set and tangents (the
Modding Kit regenerates tangents), and the shader itself, which is a vanilla asset
referenced by GUID; set it by hand from the flags in the manifest.

Keep the inherited TPAC as the authoritative backup until the re-imported
resources have been checked in game.
- The inherited package is irreplaceable; the black-and-gold shield package is
  reproducible. Never delete or overwrite the inherited package during shield
  publication.
