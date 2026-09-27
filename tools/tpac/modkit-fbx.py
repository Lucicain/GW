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


# Bone order of the vanilla skeletons (index = bone index in the game).
# horse_skeleton: Native/AssetPackages/skeletons.tpac. human_skeleton needs no entry: its
# order already is the depth-first order of its hierarchy.
SKELETON_ORDER = {
    "horse_skeleton": [
        "horsepelvis", "horsespine1", "horsespine2", "horsespine3",
        "horselleg1", "horselleg2", "horselleg3", "horselleg4", "horsellegankle", "horselfronthoof",
        "horserleg1", "horserleg2", "horserleg3", "horserleg4", "horserlegankle", "horserfronthoof",
        "horselfemur", "horseltibia", "horsellargecannon", "horselphalanx", "horselrearhoof",
        "horserfemur", "horsertibia", "horserlargecannon", "horserphalanx", "horserrearthoof",
        "horsetail1", "horsetail2", "horsetail3",
        "horseneck1", "horseneck2", "horse_head",
    ],
}


def match_skeleton_order(arm):
    """Rebuild the armature so its depth-first bone order is the vanilla skeleton's order.

    The Kit numbers skin bones by walking the FBX's own bone hierarchy depth-first
    (rglFBX importer, wEditor TaleWorlds.Native.dll, 2026-09-27); it does not look the
    names up in the vanilla skeleton. horse_skeleton lists horseneck1 (child of
    horsespine3) after the tail, which no depth-first walk of the true hierarchy
    produces, so every harness vertex on bones 16+ landed on the wrong bone in game
    (the neck plate followed the left hind leg). Bones are re-created in the vanilla
    order, each parented to its nearest ancestor still open on the depth-first path
    (horseneck1 ends up under horsepelvis). Head, tail and roll are kept, and the mesh
    stores only bone indices and weights, so nothing else changes.
    """
    order = SKELETON_ORDER.get(arm.name)
    if not order:
        return
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    if sorted(b.name for b in eb) != sorted(order):
        bpy.ops.object.mode_set(mode="OBJECT")
        raise SystemExit(f"{arm.name}: bones differ from the vanilla list")
    saved = {b.name: (b.head.copy(), b.tail.copy(), b.roll, b.parent.name if b.parent else None) for b in eb}
    for b in list(eb):
        eb.remove(b)

    def ancestors(name):
        p = saved[name][3]
        while p:
            yield p
            p = saved[p][3]

    stack = []
    for name in order:
        anc = set(ancestors(name))
        while stack and stack[-1] not in anc:
            stack.pop()
        nb = eb.new(name)
        nb.head, nb.tail, nb.roll = saved[name][0], saved[name][1], saved[name][2]
        nb.use_connect = False
        if stack:
            nb.parent = eb[stack[-1]]
        if stack and stack[-1] != saved[name][3]:
            print(f"  {name}: parent {saved[name][3]} -> {stack[-1]} (keeps vanilla bone order)")
        stack.append(name)
    bpy.ops.object.mode_set(mode="OBJECT")


def convert_model(path):
    reset()
    bpy.ops.import_scene.fbx(filepath=path)
    for a in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
        match_skeleton_order(a)
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
        # tpac-export writes every vertex with its own position; the original cloth meshes
        # share one position between the vertices on either side of a UV seam, and cloth
        # simulation connects the mesh through those shared positions. Without the weld a
        # cloth piece falls apart along its seams (2026-09-27). Only faces whose material
        # ends in "clo" are welded: welding everything also merged hard edges and skin seams
        # the original keeps apart, and only cloth depends on it.
        before = len(joined.data.vertices)
        cloth = {i for i, slot in enumerate(joined.material_slots) if slot.material and slot.material.name.endswith("clo")}
        if cloth:
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="DESELECT")
            bpy.ops.object.mode_set(mode="OBJECT")
            for poly in joined.data.polygons:
                poly.select = poly.material_index in cloth
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.remove_doubles(threshold=1e-6, use_unselected=False, use_sharp_edge_from_normals=True)
            bpy.ops.object.mode_set(mode="OBJECT")
        welded = before - len(joined.data.vertices)
        joined.name = base if lod == 0 else f"{base}.lod{lod}"
        joined.data.name = joined.name
        result.append(joined)
        mats = [s.material.name if s.material else "-" for s in joined.material_slots]
        per_mat = {}
        for poly in joined.data.polygons:
            per_mat.setdefault(poly.material_index, set()).update(poly.vertices)
        print(f"  {joined.name}: faces={len(joined.data.polygons)} welded={welded} materials={mats} "
              f"positions={[len(per_mat.get(i, ())) for i in range(len(mats))]}")
    dims = max((o.dimensions for o in result), key=lambda d: d.z)
    print(f"  size {dims.x:.3f} x {dims.y:.3f} x {dims.z:.3f}")
    out = os.path.join(out_dir, os.path.basename(path))
    export(out, ([arm] if arm else []) + result)


def physics_materials(in_dir):
    """Physics material per shape from tpac-export's manifest (../manifest.json next to physics/)."""
    import json
    path = os.path.join(in_dir, os.pardir, "manifest.json")
    if not os.path.exists(path):
        return {}
    shapes = json.load(open(path, encoding="utf-8")).get("physicsShapes", [])
    return {s["Name"]: (s["manifolds"][0]["materials"] or [None])[0] for s in shapes if s.get("manifolds")}


def convert_collision(path):
    # The Kit turns an FBX node into a physics shape when its name starts with "bo_" and the
    # import has "Import physics shapes" ticked; the physics material is the name of the FBX
    # material on the mesh (rglFBX_body_importer, wEditor TaleWorlds.Native.dll, 2026-09-27).
    # The collision OBJ carries no material, so give it one named after the original physics material.
    reset()
    bpy.ops.wm.obj_import(filepath=path, forward_axis="Y", up_axis="Z")
    obj = next(o for o in bpy.data.objects if o.type == "MESH")
    name = os.path.splitext(os.path.basename(path))[0].replace("_manifold0", "")
    obj.name = obj.data.name = name
    phys = physics_materials(os.path.dirname(path)).get(name)
    obj.data.materials.clear()
    if phys:
        obj.data.materials.append(bpy.data.materials.new(phys))
    d = obj.dimensions
    print(f"  {name}: faces={len(obj.data.polygons)} physics_material={phys} size {d.x:.3f} x {d.y:.3f} x {d.z:.3f}")
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
