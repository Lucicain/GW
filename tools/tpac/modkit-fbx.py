"""Turn tpac-export's FBX into the layout the Bannerlord Modding Kit imports.

tpac-export writes one object per TPAC submesh (winfarmor.0, winfarmor.lod1.0 ...).
Those suffixes are what the Modding Kit itself generates when it splits an object by
material, so the source it expects is one object per LOD, carrying several material
slots, named <base> and <base>.lod1 ... <base>.lod5 -- the same rule the black-gold
shield import followed (docs/reference/asset-pipeline.md).

Run headless with Blender:
  blender -b --factory-startup --python tools/tpac/modkit-fbx.py -- <in_dir> <out_dir> [--tex <dir>] [name ...]
Collision OBJ files (bo_*.obj) in <in_dir> are converted too.

The Modding Kit wants one flat folder under AssetSources holding the FBX and their
PNG side by side, mirrored by an existing folder of the same name under Assets
(that is how GreyWardenRecovery/dun.fbx was imported). So textures are re-pointed
at --tex and the FBX stores bare file names.
"""
import bpy
import os
import re
import sys

args = sys.argv[sys.argv.index("--") + 1:]
in_dir, out_dir = args[0], args[1]
rest = args[2:]
tex_dir = None
if "--tex" in rest:
    i = rest.index("--tex")
    tex_dir = rest[i + 1]
    del rest[i:i + 2]
only = set(rest)
os.makedirs(out_dir, exist_ok=True)

SUB = re.compile(r"^(?P<base>.+?)(?:\.lod(?P<lod>\d+))?(?:\.(?P<idx>\d+))?$")


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="Y",
        axis_up="Z",
        add_leaf_bones=False,
        bake_anim=False,
        mesh_smooth_type="OFF",
        path_mode="STRIP",
    )


def convert_model(path):
    reset()
    bpy.ops.import_scene.fbx(filepath=path)
    for img in bpy.data.images:
        if tex_dir and img.filepath:
            img.filepath = os.path.join(tex_dir, os.path.basename(img.filepath.replace("\\", "/")))
    missing = [i.name for i in bpy.data.images if i.filepath and not os.path.exists(bpy.path.abspath(i.filepath))]
    if missing:
        print(f"  missing textures: {missing}")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    groups = {}
    for o in meshes:
        m = SUB.match(o.name)
        if not m:
            print(f"  unexpected object name {o.name}")
            continue
        key = (m["base"], int(m["lod"] or 0))
        groups.setdefault(key, []).append(o)
    result = []
    for (base, lod), objs in sorted(groups.items(), key=lambda kv: kv[0][1]):
        objs.sort(key=lambda o: o.name)
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        joined = bpy.context.view_layer.objects.active
        joined.name = base if lod == 0 else f"{base}.lod{lod}"
        joined.data.name = joined.name
        result.append(joined)
        mats = [s.material.name if s.material else "-" for s in joined.material_slots]
        print(f"  {joined.name}: faces={len(joined.data.polygons)} materials={mats}")
    dims = max((o.dimensions for o in result), key=lambda d: d.z)
    print(f"  size {dims.x:.3f} x {dims.y:.3f} x {dims.z:.3f}")
    out = os.path.join(out_dir, os.path.basename(path))
    export(out, ([arm] if arm else []) + result)


def convert_collision(path):
    reset()
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Y", up_axis="Z")
    obj = next(o for o in bpy.data.objects if o.type == "MESH")
    name = os.path.splitext(os.path.basename(path))[0].replace("_manifold0", "")
    obj.name = obj.data.name = name
    d = obj.dimensions
    print(f"  {name}: faces={len(obj.data.polygons)} size {d.x:.3f} x {d.y:.3f} x {d.z:.3f}")
    export(os.path.join(out_dir, name + ".fbx"), [obj])


for f in sorted(os.listdir(in_dir)):
    stem, ext = os.path.splitext(f)
    if only and stem not in only and stem.replace("_manifold0", "") not in only:
        continue
    print(f"== {f}")
    if ext.lower() == ".fbx":
        convert_model(os.path.join(in_dir, f))
    elif ext.lower() == ".obj" and f.startswith("bo_"):
        convert_collision(os.path.join(in_dir, f))
